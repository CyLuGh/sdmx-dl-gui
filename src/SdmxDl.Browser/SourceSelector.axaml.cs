using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SdmxDl.Browser.ViewModels;
using Ursa.ReactiveUIExtension;

namespace SdmxDl.Browser;

public partial class SourceSelector : ReactiveUrsaView<SourceSelectorViewModel>
{
    public SourceSelector()
    {
        InitializeComponent();
    }
}
