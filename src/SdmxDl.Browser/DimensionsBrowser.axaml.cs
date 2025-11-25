using Avalonia.Controls;
using ReactiveUI.Avalonia;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class DimensionsBrowser : ReactiveUserControl<DimensionsSelectorViewModel>
{
    public DimensionsBrowser()
    {
        InitializeComponent();
    }
}
