using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class SourceSelector : ReactiveUserControl<SourceSelectorViewModel>
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
