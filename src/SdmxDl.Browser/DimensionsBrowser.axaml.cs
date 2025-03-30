using Avalonia.Controls;
using Avalonia.ReactiveUI;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class DimensionsBrowser : ReactiveUserControl<DimensionsSelectorViewModel>
{
    public DimensionsBrowser()
    {
        InitializeComponent();
    }
}
