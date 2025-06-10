using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using SdmxDl.Browser.ViewModels;
using Splat;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using Velopack;

namespace SdmxDl.Browser;

public partial class SeriesTabContainer : ReactiveUserControl<BrowserViewModel>
{
    public SeriesTabContainer()
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
        SeriesTabContainer view,
        BrowserViewModel viewModel,
        CompositeDisposable disposables
    )
    {
        viewModel
            .ConfigureServerInteraction.RegisterHandler(ctx =>
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
            .LookupSeriesInteraction.RegisterHandler(ctx =>
            {
                ViewModelLocator
                    .DialogManager.CreateDialog()
                    .WithContent(
                        new SeriesFinderView()
                        {
                            ViewModel = Locator.Current.GetService<SeriesFinderViewModel>(),
                        }
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
                var (source, flow, key) = ctx.Input[0];
                var title = DataViewModel.BuildTitle(source, flow, (string)key);

                var existingTab = view
                    .TabControlResults.Items.OfType<TabItem>()
                    .FirstOrDefault(x => x.Header?.ToString()?.Equals(title) == true);

                if (existingTab is not null)
                {
                    view.TabControlResults.SelectedItem = existingTab;
                }
                else
                {
                    var dvm = Locator.Current.GetService<DataViewModel>()!;
                    dvm.Source = source;
                    dvm.Flow = flow;
                    dvm.Key = (string)key;

                    var tabItem = new TabItem()
                    {
                        Header = title,
                        Content = new DataView() { ViewModel = dvm },
                    };
                    view.TabControlResults.Items.Add(tabItem);
                    view.TabControlResults.SelectedItem = tabItem;
                }

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        viewModel
            .CloseInteraction.RegisterHandler(ctx =>
            {
                var existingTab = view
                    .TabControlResults.Items.OfType<TabItem>()
                    .FirstOrDefault(x => x.Header?.ToString()?.Equals(ctx.Input) == true);

                if (existingTab is not null)
                {
                    view.TabControlResults.Items.Remove(existingTab);
                }

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);

        viewModel
            .UpdateApplicationInteraction.RegisterHandler(async ctx =>
            {
                var mgr = new UpdateManager(
                    viewModel.Configuration.GetValue("UpdateUrl", string.Empty)
                );

                if (mgr.IsInstalled)
                {
                    var info = await mgr.CheckForUpdatesAsync();

                    if (info is not null)
                    {
                        await mgr.DownloadUpdatesAsync(info);
                    }

                    mgr.ApplyUpdatesAndRestart(info);
                }

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
