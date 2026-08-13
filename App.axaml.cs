using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Riptastic.ViewModels;
using Riptastic.Views;

namespace Riptastic;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splashVm = new SplashViewModel();
            var splash = new SplashWindow { DataContext = splashVm };

            splashVm.Proceed += () =>
            {
                var main = new MainWindow { DataContext = new MainWindowViewModel() };
                desktop.MainWindow = main;
                main.Show();
                splash.Close();
            };

            desktop.MainWindow = splash;
            splash.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
