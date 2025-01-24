using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;
using SdmxDl.Client.Models;
using Ursa.Controls;
using Ursa.ReactiveUIExtension;

namespace SdmxDl.Browser;

public partial class Browser : ReactiveUrsaView<BrowserViewModel>
{
    private WindowToastManager? _toastManager;

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
        viewModel
            .LaunchServerInteraction.RegisterHandler(async ctx =>
            {
                var settings = await OverlayDialog.ShowCustomModal<
                    ServerStartup,
                    SettingsViewModel,
                    Settings
                >(
                    ViewModelLocator.SettingsViewModel,
                    options: new OverlayDialogOptions()
                    {
                        Buttons = DialogButton.None,
                        Mode = DialogMode.None,
                        IsCloseButtonVisible = false,
                        FullScreen = false,
                        CanDragMove = false,
                        Title = "SDMX-DL",
                    }
                );
                ctx.SetOutput(settings);
            })
            .DisposeWith(disposables);

        viewModel
            .DisplayErrorMessageInteraction.RegisterHandler(ctx =>
            {
                view._toastManager?.Show(
                    new Toast(ctx.Input.Message, NotificationType.Error),
                    NotificationType.Error
                );
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var topLevel = TopLevel.GetTopLevel(this);
        _toastManager = new(topLevel) { MaxItems = 3 };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _toastManager?.Uninstall();
    }
}
