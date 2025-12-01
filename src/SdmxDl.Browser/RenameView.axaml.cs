using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;
using SdmxDl.Browser.ViewModels;

namespace SdmxDl.Browser;

public partial class RenameView : ReactiveUserControl<RenameViewModel>
{
    public RenameView()
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
        RenameView view,
        RenameViewModel viewModel,
        CompositeDisposable disposables
    )
    {
        viewModel
            .CloseInteraction.RegisterHandler(ctx =>
            {
                ViewModelLocator.DialogManager.DismissDialog();
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
