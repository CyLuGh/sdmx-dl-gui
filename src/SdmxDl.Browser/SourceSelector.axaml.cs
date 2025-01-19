using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;
using Ursa.ReactiveUIExtension;

namespace SdmxDl.Browser;

public partial class SourceSelector : ReactiveUrsaView<SourceSelectorViewModel>
{
    public SourceSelector()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .BindTo(this, x => x.DataContext)
                .DisposeWith(disposables);
        });
    }
}
