using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Riptastic.Models;
using Riptastic.Services;

namespace Riptastic.ViewModels;

public partial class SplashViewModel : ViewModelBase
{
    public event Action? Proceed;

    /// <summary>Raised when a required tool is missing and the results view should be shown.</summary>
    public event Action? ProblemsFound;

    public ObservableCollection<DependencyResult> Dependencies { get; } = [];

    [ObservableProperty] private bool _isChecking = true;
    [ObservableProperty] private bool _hasProblems;
    [ObservableProperty] private string _headerText = "Checking required components…";
    [ObservableProperty] private string _subText = "";
    [ObservableProperty] private bool _homebrewMissing;

    /// <summary>A single copy-pasteable command that installs everything missing.</summary>
    [ObservableProperty] private string _installAllCommand = "";

    public async Task RunChecksAsync()
    {
        IsChecking = true;
        HasProblems = false;
        HeaderText = "Checking required components…";
        SubText = "";
        Dependencies.Clear();

        await Task.Delay(1500); // keep the splash visible even when checks finish instantly

        var results = DependencyChecker.CheckAll();
        foreach (var r in results)
            Dependencies.Add(r);

        var missing = results.Where(r => !r.Found).ToList();
        IsChecking = false;

        if (missing.Count == 0)
        {
            HeaderText = "All set";
            SubText = "Everything the ripper needs is installed.";
            await Task.Delay(500);
            Proceed?.Invoke();
            return;
        }

        HasProblems = true;
        HomebrewMissing = !DependencyChecker.HomebrewInstalled();
        HeaderText = missing.Count == 1
            ? "1 component is missing"
            : $"{missing.Count} components are missing";

        // Deduplicate install commands (ffmpeg/ffprobe share one).
        var pkgs = missing
            .Select(m => m.InstallCommand.Replace("brew install ", ""))
            .Distinct()
            .ToList();
        InstallAllCommand = "brew install " + string.Join(' ', pkgs);

        SubText = HomebrewMissing
            ? "Install Homebrew first (see below), then run the install command and re-check."
            : "Run the command below in Terminal, then click Re-check.";

        ProblemsFound?.Invoke();
    }

    [RelayCommand]
    private Task Recheck() => RunChecksAsync();

    [RelayCommand]
    private void Quit() => Environment.Exit(0);
}
