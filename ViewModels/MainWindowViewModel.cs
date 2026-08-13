using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Riptastic.Models;
using Riptastic.Services;

namespace Riptastic.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HandBrakeService? _handbrake;
    private readonly FfmpegService? _ffmpeg;
    private readonly RipService? _rip;

    private CancellationTokenSource? _cts;

    public MainWindowViewModel()
    {
        var hb = DependencyChecker.FindExecutable("HandBrakeCLI");
        var ff = DependencyChecker.FindExecutable("ffmpeg");
        if (hb is not null) _handbrake = new HandBrakeService(hb);
        if (ff is not null) _ffmpeg = new FfmpegService(ff);
        if (_handbrake is not null && _ffmpeg is not null)
            _rip = new RipService(_handbrake, _ffmpeg);

        Qualities = QualityOption.All;
        SelectedQuality = QualityOption.All[0];
        OutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Movies");
    }

    // ---- Source ----------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? _sourcePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isSourceValid;

    [ObservableProperty] private string _sourceSummary = "";

    public ObservableCollection<DvdTitle> Titles { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyPropertyChangedFor(nameof(ContentSummary))]
    [NotifyPropertyChangedFor(nameof(ShowShapeOptions))]
    private DvdTitle? _selectedTitle;

    /// <summary>Reflects the chosen output ratio: the native shape, or 16:9 when "fill screen" is picked.</summary>
    public string ContentSummary
    {
        get
        {
            if (SelectedTitle is null) return "";
            return SelectedShape?.Shape == OutputShape.Fill16x9
                ? "Picture will be 16:9 - fills the screen (sides cropped off)"
                : SelectedTitle.ContentSummary;
        }
    }

    /// <summary>Output-ratio picker only appears for scope (wider-than-16:9) titles.</summary>
    public bool ShowShapeOptions => SelectedTitle?.IsScope ?? false;

    partial void OnSelectedTitleChanged(DvdTitle? value)
    {
        ShapeOptions.Clear();
        if (value is not null)
        {
            ShapeOptions.Add(new OutputShapeOption($"{value.ContentAspectText} - original", OutputShape.Native));
            if (value.IsScope)
                ShapeOptions.Add(new OutputShapeOption("16:9 - fill screen (crops sides)", OutputShape.Fill16x9));
        }
        SelectedShape = ShapeOptions.FirstOrDefault();
    }

    // ---- Options ---------------------------------------------------------

    [ObservableProperty] private string _outputFolder = "";
    [ObservableProperty] private string _outputBaseName = "Riptastic_Rip";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _exportMkv = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _exportMp4 = true;

    public QualityOption[] Qualities { get; }
    [ObservableProperty] private QualityOption _selectedQuality;

    public ObservableCollection<OutputShapeOption> ShapeOptions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContentSummary))]
    private OutputShapeOption? _selectedShape;

    // ---- Status ----------------------------------------------------------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isScanning;

    [ObservableProperty] private string _statusText = "Drop a VIDEO_TS folder to begin.";
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isIndeterminate;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _finishedOk;

    public bool ToolsAvailable => _rip is not null;

    // ---- Loading a source folder ----------------------------------------

    public async Task LoadSourceAsync(string droppedPath)
    {
        var videoTs = ResolveVideoTs(droppedPath);
        if (videoTs is null)
        {
            IsSourceValid = false;
            Titles.Clear();
            SourceSummary = "";
            StatusText = "That folder doesn't contain a VIDEO_TS with a VIDEO_TS.IFO. Pick the DVD folder.";
            return;
        }

        SourcePath = videoTs;
        OutputBaseName = SuggestBaseName(videoTs);
        StatusText = "Scanning DVD titles…";
        IsScanning = true;
        IsIndeterminate = true;
        Titles.Clear();
        SelectedTitle = null;

        try
        {
            var titles = await _handbrake!.ScanAsync(videoTs, CancellationToken.None);
            foreach (var t in titles) Titles.Add(t);

            SelectedTitle = Titles.FirstOrDefault(t => t.IsMainFeature)
                            ?? Titles.OrderByDescending(t => t.Duration).FirstOrDefault();

            IsSourceValid = SelectedTitle is not null;
            SourceSummary = IsSourceValid
                ? $"{Titles.Count} title(s) found · {SourcePath}"
                : "No playable titles found on this disc.";
            StatusText = IsSourceValid
                ? "Ready. Review the options and click Rip."
                : "No playable titles found.";
        }
        catch (Exception ex)
        {
            IsSourceValid = false;
            StatusText = "Scan failed: " + ex.Message;
        }
        finally
        {
            IsScanning = false;
            IsIndeterminate = false;
        }
    }

    private static string? ResolveVideoTs(string path)
    {
        if (Directory.Exists(path))
        {
            if (File.Exists(Path.Combine(path, "VIDEO_TS.IFO")))
                return path;

            var nested = Path.Combine(path, "VIDEO_TS");
            if (File.Exists(Path.Combine(nested, "VIDEO_TS.IFO")))
                return nested;

            // A folder that directly holds .IFO/.VOB files but not the named marker.
            if (Directory.EnumerateFiles(path, "*.IFO").Any())
                return path;
        }
        else if (File.Exists(path))
        {
            // Dropped a file inside VIDEO_TS - use its directory.
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && File.Exists(Path.Combine(dir, "VIDEO_TS.IFO")))
                return dir;
        }

        return null;
    }

    private static string SuggestBaseName(string videoTsPath)
    {
        var parent = Directory.GetParent(videoTsPath)?.Name ?? "Riptastic_Rip";
        string[] generic = ["downloads", "desktop", "documents", "movies", "video_ts", "michael"];
        return generic.Contains(parent.ToLowerInvariant()) ? "Riptastic_Rip" : parent;
    }

    // ---- Rip -------------------------------------------------------------

    private bool CanStart() =>
        ToolsAvailable && IsSourceValid && !IsBusy && !IsScanning &&
        SelectedTitle is not null && (ExportMkv || ExportMp4) &&
        !string.IsNullOrWhiteSpace(OutputFolder);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (_rip is null || SelectedTitle is null) return;

        _cts = new CancellationTokenSource();
        IsBusy = true;
        FinishedOk = false;
        LogText = "";
        ProgressValue = 0;

        var req = new RipRequest
        {
            Source = SourcePath!,
            Title = SelectedTitle,
            OutputFolder = OutputFolder,
            BaseName = SanitizeFileName(OutputBaseName),
            Rf = SelectedQuality.Rf,
            Shape = SelectedShape?.Shape ?? OutputShape.Native,
            ExportMkv = ExportMkv,
            ExportMp4 = ExportMp4,
        };

        var progress = new Progress<RipProgress>(p =>
        {
            StatusText = p.Message;
            IsIndeterminate = p.Indeterminate;
            ProgressValue = p.Fraction * 100;
            if (p.Stage is RipStage.Encoding or RipStage.Muxing)
                Append(p.Message);
        });

        Append($"Ripping “{SelectedTitle.Display}”");
        Append($"Output → {OutputFolder}");
        Append($"Formats: {(ExportMkv ? "MKV " : "")}{(ExportMp4 ? "MP4" : "")}  ·  Quality: {SelectedQuality.Label}");
        Append("");

        try
        {
            await _rip.RunAsync(req, progress, _cts.Token);
            FinishedOk = true;
            ProgressValue = 100;
            StatusText = "Done - " + string.Join(", ", _rip.LastOutputs.Select(Path.GetFileName));
            Append("");
            foreach (var o in _rip.LastOutputs) Append("✓ " + o);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
            Append("Cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusText = "Failed: " + ex.Message;
            Append("ERROR: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsIndeterminate = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    // ---- helpers ---------------------------------------------------------

    private void Append(string line)
    {
        var sb = new StringBuilder(LogText);
        sb.Append(line).Append('\n');
        LogText = sb.ToString();
    }

    private static string SanitizeFileName(string name)
    {
        var cleaned = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Riptastic_Rip" : cleaned;
    }
}
