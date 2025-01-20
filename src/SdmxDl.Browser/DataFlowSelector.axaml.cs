using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SdmxDl.Browser.ViewModels;
using Ursa.ReactiveUIExtension;

namespace SdmxDl.Browser;

public partial class DataFlowSelector : ReactiveUrsaView<DataFlowSelectorViewModel>
{
    public DataFlowSelector()
    {
        InitializeComponent();
    }
}
