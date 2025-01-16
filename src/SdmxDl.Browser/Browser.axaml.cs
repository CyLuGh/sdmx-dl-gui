using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;
using Ursa.Controls;
using Ursa.ReactiveUIExtension;

namespace SdmxDl.Browser;

public partial class Browser : ReactiveUrsaView<BrowserViewModel>
{
    public Browser()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Do(vm => PopulateFromViewModel(this, vm, disposables))
                .Subscribe()
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel)
                .BindTo(this, x => x.DataContext)
                .DisposeWith(disposables);
        });
    }

    private static void PopulateFromViewModel(
        Browser view,
        BrowserViewModel viewModel,
        CompositeDisposable disposables
    )
    {
        viewModel.LaunchServerInteraction.RegisterHandler(async ctx =>
        {
            await OverlayDialog.ShowCustomModal<ServerStartup, SettingsViewModel, RxUnit>(
                ViewModelLocator.SettingsViewModel,
                options: new OverlayDialogOptions()
                {
                    Buttons = DialogButton.None,
                    Mode = DialogMode.None,
                    IsCloseButtonVisible = false,
                }
            );
            ctx.SetOutput(RxUnit.Default);
        });
    }
}
