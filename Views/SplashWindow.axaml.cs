using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Riptastic.ViewModels;

namespace Riptastic.Views;

public partial class SplashWindow : Window
{
    // Compact while checking; grows to this when missing dependencies must be shown.
    private const double ResultsWidth = 500;
    private const double ResultsHeight = 640;

    public SplashWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is SplashViewModel vm)
        {
            vm.ProblemsFound += () => _ = AnimateToAsync(ResultsWidth, ResultsHeight);
            _ = vm.RunChecksAsync();
        }
    }

    /// <summary>Smoothly grows the (borderless) window while keeping it centred on its current centre.</summary>
    private async Task AnimateToAsync(double targetW, double targetH)
    {
        double startW = Width, startH = Height;
        if (Math.Abs(startW - targetW) < 1 && Math.Abs(startH - targetH) < 1) return;

        double centreX = Position.X + startW / 2.0;
        double centreY = Position.Y + startH / 2.0;

        const int steps = 14;
        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            double e = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            double w = startW + (targetW - startW) * e;
            double h = startH + (targetH - startH) * e;
            Width = w;
            Height = h;
            Position = new PixelPoint((int)(centreX - w / 2.0), (int)(centreY - h / 2.0));
            await Task.Delay(16);
        }

        Width = targetW;
        Height = targetH;
        Position = new PixelPoint((int)(centreX - targetW / 2.0), (int)(centreY - targetH / 2.0));
    }
}
