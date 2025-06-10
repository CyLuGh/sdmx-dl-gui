using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class SideBrowser : ReactiveUserControl<BrowserViewModel>
{
    public SideBrowser()
    {
        InitializeComponent();
    }
}
