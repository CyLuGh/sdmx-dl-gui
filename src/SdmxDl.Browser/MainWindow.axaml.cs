using Avalonia.Controls;
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
}
