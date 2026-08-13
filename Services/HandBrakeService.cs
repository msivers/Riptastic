using System.Text;
using System.Text.Json;
using Riptastic.Models;

namespace Riptastic.Services;

/// <summary>Drives HandBrakeCLI for scanning titles and encoding to MKV.</summary>
public sealed class HandBrakeService
{
    private readonly string _exe;

    public HandBrakeService(string handBrakeCliPath) => _exe = handBrakeCliPath;

    // ---- Scan ------------------------------------------------------------

    public async Task<IReadOnlyList<DvdTitle>> ScanAsync(string source, CancellationToken ct)
    {
        string[] args = ["--json", "-i", source, "-t", "0", "--min-duration", "10", "--scan"];
        using var proc = ProcessRunner.Create(_exe, args);
        proc.Start();

        // Read stdout as raw bytes: libdvdnav injects non-UTF8 noise, so decode leniently.
        using var ms = new MemoryStream();
        await proc.StandardOutput.BaseStream.CopyToAsync(ms, ct);
        _ = proc.StandardError.ReadToEndAsync(ct); // drain
        await proc.WaitForExitAsync(ct);

        var raw = Encoding.UTF8.GetString(ms.ToArray());
        return ParseTitleSet(raw);
    }

    private static List<DvdTitle> ParseTitleSet(string raw)
    {
        var titles = new List<DvdTitle>();

        var marker = raw.IndexOf("JSON Title Set:", StringComparison.Ordinal);
        if (marker < 0) return titles;

        var json = ExtractJsonObject(raw, marker);
        if (json is null) return titles;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("TitleList", out var list) || list.ValueKind != JsonValueKind.Array)
            return titles;

        foreach (var t in list.EnumerateArray())
        {
            var dur = t.GetProperty("Duration");
            var geo = t.GetProperty("Geometry");
            var par = geo.TryGetProperty("PAR", out var parEl) ? parEl : default;
            var crop = ReadCrop(t);   // [top, bottom, left, right]
            titles.Add(new DvdTitle
            {
                Index = t.GetProperty("Index").GetInt32(),
                Duration = new TimeSpan(
                    dur.GetProperty("Hours").GetInt32(),
                    dur.GetProperty("Minutes").GetInt32(),
                    dur.GetProperty("Seconds").GetInt32()),
                Width = geo.GetProperty("Width").GetInt32(),
                Height = geo.GetProperty("Height").GetInt32(),
                ParNum = par.ValueKind == JsonValueKind.Object ? par.GetProperty("Num").GetInt32() : 1,
                ParDen = par.ValueKind == JsonValueKind.Object ? par.GetProperty("Den").GetInt32() : 1,
                CropTop = crop[0],
                CropBottom = crop[1],
                CropLeft = crop[2],
                CropRight = crop[3],
                AudioCount = CountArray(t, "AudioList"),
                SubtitleCount = CountArray(t, "SubtitleList"),
                ChapterCount = CountArray(t, "ChapterList"),
            });
        }

        // HandBrake's own "MainFeature" flag is unreliable on DVDs (it can point at a
        // short ident clip), so we mark the longest title as the main feature ourselves.
        var longest = titles.OrderByDescending(t => t.Duration).FirstOrDefault();
        if (longest is not null)
            longest.IsMainFeature = true;

        return titles;
    }

    private static int CountArray(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? a.GetArrayLength() : 0;

    /// <summary>Reads HandBrake's auto-detected crop [top, bottom, left, right]; zeros if absent.</summary>
    private static int[] ReadCrop(JsonElement title)
    {
        var result = new int[4];
        if (title.TryGetProperty("Crop", out var c) && c.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var v in c.EnumerateArray())
            {
                if (i >= 4) break;
                result[i++] = v.GetInt32();
            }
        }
        return result;
    }

    // ---- Encode ----------------------------------------------------------

    public async Task EncodeToMkvAsync(
        string source, DvdTitle title, int rf, OutputShape shape, string outputPath,
        IProgress<double> progress, CancellationToken ct)
    {
        var args = new List<string>
        {
            "--json",
            "-i", source,
            "-t", title.Index.ToString(),
            "-o", outputPath,
            "--format", "av_mkv",
            "-e", "x264",
            "-q", rf.ToString(),
            "--encoder-preset", "slow",
            "--aencoder", "copy:ac3",
            "--audio-fallback", "av_aac",
            "--markers",           // chapter markers
            "--comb-detect",       // detect interlacing (PAL DVDs are often interlaced)
            "--decomb",            // deinterlace only frames that need it
        };

        // Always output square pixels at the display size (e.g. 720×576 @ PAR 64:45 → 1024 wide,
        // SAR 1:1), so the shape is correct in every player and editor without relying on a flag.
        args.AddRange(["--non-anamorphic", "--width", title.DisplayWidth.ToString()]);

        if (shape == OutputShape.Fill16x9)
        {
            // Trim the letterbox bars AND crop the sides so the scope picture fills 16:9.
            var (t, b, l, r) = title.Fill16x9Crop();
            args.AddRange(["--crop", $"{t}:{b}:{l}:{r}"]);
        }
        // Native shape uses HandBrake's default auto-crop (removes black bars, keeps the real ratio).

        if (title.SubtitleCount > 0)
            args.AddRange(["--subtitle", string.Join(',', Enumerable.Range(1, title.SubtitleCount))]);

        using var proc = ProcessRunner.Create(_exe, args);
        proc.Start();

        await using var kill = ct.Register(() => TryKill(proc));

        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await ReadJsonBlocksAsync(proc.StandardOutput.BaseStream, block =>
        {
            if (!block.StartsWith("Progress:", StringComparison.Ordinal)) return;
            var json = block[(block.IndexOf('{'))..];
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("State", out var st) &&
                    st.GetString() == "WORKING" &&
                    doc.RootElement.TryGetProperty("Working", out var w) &&
                    w.TryGetProperty("Progress", out var pr))
                {
                    progress.Report(pr.GetDouble());
                }
            }
            catch (JsonException) { /* ignore partial/garbage */ }
        }, ct);

        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;
        ct.ThrowIfCancellationRequested();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"HandBrake failed (exit {proc.ExitCode}).\n{Tail(stderr)}");
    }

    // ---- Streaming JSON block reader ------------------------------------
    // HandBrake --json emits marker-prefixed blocks (e.g. `Progress: {` … `}`);
    // track brace depth to yield one complete block at a time.

    private static async Task ReadJsonBlocksAsync(Stream stdout, Action<string> onBlock, CancellationToken ct)
    {
        using var reader = new StreamReader(stdout, Encoding.UTF8, false, 8192, leaveOpen: true);
        var block = new StringBuilder();
        int depth = 0;
        bool capturing = false;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!capturing)
            {
                var brace = line.IndexOf('{');
                if (brace < 0) continue;                 // marker-less noise
                capturing = true;
                depth = 0;
                block.Clear();
            }

            block.Append(line).Append('\n');
            foreach (var c in line)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }

            if (capturing && depth <= 0)
            {
                onBlock(block.ToString());
                capturing = false;
            }
        }
    }

    // ---- helpers ---------------------------------------------------------

    private static string? ExtractJsonObject(string text, int fromIndex)
    {
        var start = text.IndexOf('{', fromIndex);
        if (start < 0) return null;

        int depth = 0;
        for (int j = start; j < text.Length; j++)
        {
            if (text[j] == '{') depth++;
            else if (text[j] == '}' && --depth == 0)
                return text[start..(j + 1)];
        }
        return null;
    }

    private static void TryKill(System.Diagnostics.Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch { /* ignore */ }
    }

    private static string Tail(string s, int lines = 12)
    {
        var arr = s.Split('\n');
        return string.Join('\n', arr[Math.Max(0, arr.Length - lines)..]);
    }
}
