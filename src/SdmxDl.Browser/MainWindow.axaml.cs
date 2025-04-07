using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Jot;
using Splat;
using SukiUI.Controls;

namespace SdmxDl.Browser;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        var tracker = Locator.Current.GetService<Tracker>();
        tracker?.PersistAll();
    }

    private void GitHubLinkClick(object? sender, RoutedEventArgs e)
    {
        var url = (e.Source as HyperlinkButton).Content as string;

        if (string.IsNullOrEmpty(url))
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
}
