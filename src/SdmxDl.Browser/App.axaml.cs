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
            if (desktop.Args?.Length > 0)
                ViewModelLocator.BrowserViewModel.Argument = desktop.Args[0];

            desktop.Exit += (_, _) =>
            {
                Locator.Current.GetService<ClientFactory>()?.StopServer();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
