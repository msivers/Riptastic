using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Riptastic.Models;
using Riptastic.Services;

namespace Riptastic.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public ObservableCollection<DependencyResult> Dependencies { get; } = [];

    [ObservableProperty] private bool _allOk;
    [ObservableProperty] private string _summary = "";

    public string AvaloniaVersion { get; }
    public string DotnetVersion { get; }
    public string AppVersion { get; }

    public SettingsViewModel()
    {
        AvaloniaVersion = typeof(Avalonia.Application).Assembly.GetName().Version?.ToString() ?? "12.x";
        DotnetVersion = RuntimeInformation.FrameworkDescription;      // e.g. ".NET 10.0.3"
        var v = typeof(SettingsViewModel).Assembly.GetName().Version;
        AppVersion = v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Dependencies.Clear();
        foreach (var d in DependencyChecker.CheckAll())
            Dependencies.Add(d);

        var missing = Dependencies.Count(d => !d.Found);
        AllOk = missing == 0;
        Summary = AllOk
            ? "All external tools are installed and found."
            : $"{missing} missing - install below, then click Re-check.";
    }
}
