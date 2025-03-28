using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SdmxDl.Client;
using Splat;

namespace SdmxDl.Browser;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, __) =>
            {
                Locator.Current.GetService<ClientFactory>()?.StopServer();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
