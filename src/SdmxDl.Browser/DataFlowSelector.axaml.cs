using Avalonia.ReactiveUI;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class DataFlowSelector : ReactiveUserControl<DataFlowSelectorViewModel>
{
    public DataFlowSelector()
    {
        InitializeComponent();
    }
}
