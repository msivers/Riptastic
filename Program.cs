using Avalonia;

namespace Riptastic;

internal static class Program
{
    // Don't touch Avalonia or SynchronizationContext-reliant code before AppMain runs.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
