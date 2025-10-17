using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI.Avalonia;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class PositionedDimensionView : ReactiveUserControl<PositionedDimensionViewModel>
{
    public PositionedDimensionView()
    {
        InitializeComponent();
    }
}
