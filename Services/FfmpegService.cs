using Riptastic.Models;

namespace Riptastic.Services;

/// <summary>Remuxes a finished MKV into an Apple-friendly MP4 using ffmpeg.</summary>
public sealed class FfmpegService
{
    private readonly string _exe;

    public FfmpegService(string ffmpegPath) => _exe = ffmpegPath;

    /// <summary>Copies the H.264 untouched, keeps AC3 5.1, adds a default AAC stereo track. No subtitles - MP4 can't carry DVD bitmap subs.</summary>
    public async Task RemuxToMp4Async(
        string mkvPath, string mp4Path, TimeSpan totalDuration,
        IProgress<double> progress, CancellationToken ct)
    {
        string[] args =
        [
            "-y",
            "-i", mkvPath,
            "-map", "0:v:0",
            "-map", "0:a:0",
            "-map", "0:a:0",
            "-c:v", "copy",
            "-c:a:0", "aac", "-b:a:0", "256k", "-ac:a:0", "2",
            "-c:a:1", "copy",
            "-metadata:s:a:0", "title=Stereo (AAC)", "-metadata:s:a:0", "language=eng",
            "-metadata:s:a:1", "title=Surround 5.1 (AC3)", "-metadata:s:a:1", "language=eng",
            "-disposition:a:0", "default",
            "-disposition:a:1", "0",
            "-movflags", "+faststart",
            "-progress", "pipe:1",
            "-nostats",
            "-loglevel", "error",
            mp4Path,
        ];

        using var proc = ProcessRunner.Create(_exe, args);
        proc.Start();

        await using var kill = ct.Register(() => TryKill(proc));

        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        double totalSeconds = Math.Max(1, totalDuration.TotalSeconds);

        using var reader = proc.StandardOutput;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            // ffmpeg -progress emits key=value lines; out_time_us is microseconds.
            if (line.StartsWith("out_time_us=", StringComparison.Ordinal) &&
                long.TryParse(line.AsSpan("out_time_us=".Length), out var us) && us >= 0)
            {
                progress.Report(Math.Clamp(us / 1_000_000.0 / totalSeconds, 0, 1));
            }
            else if (line.StartsWith("progress=end", StringComparison.Ordinal))
            {
                progress.Report(1.0);
            }
        }

        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;
        ct.ThrowIfCancellationRequested();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed (exit {proc.ExitCode}).\n{stderr}");
    }

    private static void TryKill(System.Diagnostics.Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch { /* ignore */ }
    }
}
