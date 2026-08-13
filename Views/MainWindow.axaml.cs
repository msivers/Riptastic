using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Riptastic.ViewModels;

namespace Riptastic.Views;

public partial class MainWindow : Window
{
    // Activity-log tail-follow: keep pinned to the bottom while the user is near it,
    // but stop following the moment they scroll up to read earlier output.
    private ScrollViewer? _logScroll;
    private bool _logFollow = true;
    private const double FollowThresholdPx = 96; // ~6 lines at a ~16px line height

    public MainWindow()
    {
        InitializeComponent();

        var dropZone = this.FindControl<Border>("DropZone");
        if (dropZone is not null)
        {
            dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            dropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        _logScroll = this.FindControl<ScrollViewer>("LogScroll");
        if (_logScroll is not null)
            _logScroll.ScrollChanged += OnLogScrollChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnLogScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScroll is null) return;

        if (e.ExtentDelta.Y > 0)
        {
            // New log content was appended — follow it only if we were near the bottom.
            if (_logFollow)
                _logScroll.ScrollToEnd();
        }
        else if (e.OffsetDelta.Y != 0)
        {
            // The user (or a programmatic scroll) moved without content changing —
            // re-evaluate whether we're close enough to the end to keep following.
            var distanceFromBottom =
                _logScroll.Extent.Height - _logScroll.Offset.Y - _logScroll.Viewport.Height;
            _logFollow = distanceFromBottom <= FollowThresholdPx;
        }
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    // ---- Drag & drop -----------------------------------------------------

    private static IStorageItem? FirstFile(DragEventArgs e)
        => e.DataTransfer?.Items.Select(i => i.TryGetFile()).FirstOrDefault(f => f is not null);

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = FirstFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null) return;

        var path = FirstFile(e)?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            await Vm.LoadSourceAsync(path);
    }

    // ---- Pickers ---------------------------------------------------------

    private async void OnBrowseSource(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the VIDEO_TS folder (or the DVD folder that contains it)",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            await Vm.LoadSourceAsync(path);
    }

    private async void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose an output folder",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            Vm.OutputFolder = path;
    }

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow { DataContext = new SettingsViewModel() };
        await dialog.ShowDialog(this);
    }

    private async void OnFormatInfo(object? sender, RoutedEventArgs e)
    {
        var dialog = new FormatInfoWindow();
        await dialog.ShowDialog(this);
    }

    private void OnOpenOutput(object? sender, RoutedEventArgs e)
    {
        var folder = Vm?.OutputFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        try
        {
            Process.Start(new ProcessStartInfo("open", $"\"{folder}\"") { UseShellExecute = false });
        }
        catch { /* ignore */ }
    }
}
