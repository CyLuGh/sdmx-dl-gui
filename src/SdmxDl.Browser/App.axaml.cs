using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SdmxDl.Client;
using Splat;
using SukiUI;
using SukiUI.Models;

namespace SdmxDl.Browser;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public static SukiColorTheme AppTheme =>
        new("CustomTheme", Color.FromArgb(255, 0, 146, 182), Color.FromArgb(255, 226, 16, 115));

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var theme = SukiTheme
                .GetInstance()
                .ColorThemes.Find(c => c.DisplayName.Equals("CustomTheme"))
                .Match(t => t, () => AppTheme);
            SukiTheme.GetInstance().ChangeColorTheme(theme);

            desktop.MainWindow = new MainWindow();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

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
