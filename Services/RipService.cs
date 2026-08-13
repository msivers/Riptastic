using Riptastic.Models;

namespace Riptastic.Services;

public sealed class RipRequest
{
    public required string Source { get; init; }        // path to VIDEO_TS
    public required DvdTitle Title { get; init; }
    public required string OutputFolder { get; init; }
    public required string BaseName { get; init; }
    public required int Rf { get; init; }
    public required OutputShape Shape { get; init; }
    public required bool ExportMkv { get; init; }
    public required bool ExportMp4 { get; init; }
}

/// <summary>Orchestrates the encode → remux pipeline, mirroring the manual workflow.</summary>
public sealed class RipService
{
    private readonly HandBrakeService _handbrake;
    private readonly FfmpegService _ffmpeg;

    public RipService(HandBrakeService handbrake, FfmpegService ffmpeg)
    {
        _handbrake = handbrake;
        _ffmpeg = ffmpeg;
    }

    public IReadOnlyList<string> LastOutputs { get; private set; } = [];

    public async Task RunAsync(RipRequest req, IProgress<RipProgress> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(req.OutputFolder);

        var mkvFinal = Path.Combine(req.OutputFolder, req.BaseName + ".mkv");
        var mp4Final = Path.Combine(req.OutputFolder, req.BaseName + ".mp4");

        // If MKV isn't wanted we still need the H.264 to build the MP4 from, so
        // encode to a temp file and delete it afterwards.
        var workMkv = req.ExportMkv
            ? mkvFinal
            : Path.Combine(Path.GetTempPath(), $"riptastic_{Guid.NewGuid():N}.mkv");

        var outputs = new List<string>();

        try
        {
            // Stage 1 - encode
            progress.Report(new RipProgress(RipStage.Encoding, 0, "Encoding H.264 video…"));
            var encodeProgress = new Progress<double>(f =>
                progress.Report(new RipProgress(RipStage.Encoding, f, $"Encoding H.264 video…  {f * 100:0}%")));
            await _handbrake.EncodeToMkvAsync(
                req.Source, req.Title, req.Rf, req.Shape, workMkv, encodeProgress, ct);

            if (req.ExportMkv) outputs.Add(mkvFinal);

            // Stage 2 - MP4 (optional)
            if (req.ExportMp4)
            {
                progress.Report(new RipProgress(RipStage.Muxing, 0, "Building MP4…"));
                var muxProgress = new Progress<double>(f =>
                    progress.Report(new RipProgress(RipStage.Muxing, f, $"Building MP4…  {f * 100:0}%")));
                await _ffmpeg.RemuxToMp4Async(workMkv, mp4Final, req.Title.Duration, muxProgress, ct);
                outputs.Add(mp4Final);
            }

            LastOutputs = outputs;
            progress.Report(new RipProgress(RipStage.Done, 1, "Done."));
        }
        finally
        {
            if (!req.ExportMkv && File.Exists(workMkv))
            {
                try { File.Delete(workMkv); } catch { /* ignore */ }
            }
        }
    }
}
