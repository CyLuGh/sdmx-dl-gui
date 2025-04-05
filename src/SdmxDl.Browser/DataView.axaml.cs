using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class DataView : ReactiveUserControl<DataViewModel>
{
    public DataView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Do(vm => PopulateFromViewModel(this, vm, disposables))
                .Subscribe()
                .DisposeWith(disposables);
        });
    }

    private static void PopulateFromViewModel(
        DataView view,
        DataViewModel viewModel,
        CompositeDisposable disposables
    )
    {
        viewModel
            .CopyToClipboardInteraction.RegisterHandler(async ctx =>
            {
                var clipboard = TopLevel.GetTopLevel(view)?.Clipboard;
                await clipboard?.SetTextAsync(ctx.Input);
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
