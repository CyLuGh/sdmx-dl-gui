using Avalonia.Controls;
using Jot;
using Splat;
using Ursa.Controls;

namespace SdmxDl.Browser;

public partial class MainWindow : UrsaWindow
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
}
