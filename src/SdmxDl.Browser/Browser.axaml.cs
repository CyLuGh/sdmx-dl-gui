using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;
using Splat;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace SdmxDl.Browser;

public partial class Browser : ReactiveUserControl<BrowserViewModel>
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
        viewModel
            .LaunchServerInteraction.RegisterHandler(ctx =>
            {
                ViewModelLocator
                    .DialogManager.CreateDialog()
                    .WithContent(
                        new ServerStartup() { ViewModel = ViewModelLocator.SettingsViewModel }
                    )
                    .TryShow();

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        viewModel
            .DisplayErrorMessageInteraction.RegisterHandler(ctx =>
            {
                ViewModelLocator
                    .ToastManager.CreateToast()
                    .WithTitle(ctx.Input.Source ?? string.Empty)
                    .WithContent(ctx.Input.Message)
                    .OfType(NotificationType.Error)
                    .Dismiss()
                    .After(TimeSpan.FromSeconds(15))
                    .Dismiss()
                    .ByClicking()
                    .Queue();

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        viewModel
            .ShowResultsInteraction.RegisterHandler(ctx =>
            {
                var (source, flow, key) = ctx.Input;

                var dvm = Locator.Current.GetService<DataViewModel>();
                dvm.Source = source;
                dvm.Flow = flow;
                dvm.Key = key;

                view.TabControlResults.Items.Add(
                    new TabItem()
                    {
                        Header = DataViewModel.BuildTitle(source, flow, key),
                        Content = new DataView() { ViewModel = dvm },
                    }
                );
                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
