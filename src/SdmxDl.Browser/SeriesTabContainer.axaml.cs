using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using ReactiveUI.Avalonia;
using SdmxDl.Browser.ViewModels;
using Splat;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Toasts;
using Velopack;

namespace SdmxDl.Browser;

public partial class SeriesTabContainer : ReactiveUserControl<BrowserViewModel>
{
    public SeriesTabContainer()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.ViewModel)
            .WhereNotNull()
            .Subscribe(vm =>
            {
                vm.UpdateApplicationInteraction.RegisterHandler(async ctx =>
                {
                    var updateUrl = vm.Configuration.GetValue("UpdateUrl", string.Empty);

                    if (!string.IsNullOrWhiteSpace(updateUrl))
                    {
                        var mgr = new UpdateManager(updateUrl);
                        if (mgr.IsInstalled)
                        {
                            var info = await mgr.CheckForUpdatesAsync();

                            if (info is not null)
                            {
                                await mgr.DownloadUpdatesAsync(info);
                                mgr.ApplyUpdatesAndRestart(info);
                            }
                        }
                    }

                    ctx.SetOutput(RxUnit.Default);
                });
            });

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

        SukiWindow? browserWindow = null;
        viewModel
            .OpenBrowserInteraction.RegisterHandler(ctx =>
            {
                if (browserWindow is null)
                {
                    browserWindow = new SukiWindow()
                    {
                        Title = "Series Explorer",
                        Content = new SideBrowser() { ViewModel = viewModel },
                        CanMaximize = false,
                        CanMinimize = false,
                        CanFullScreen = false,
                        ShowBottomBorder = true,
                        ShowTitlebarBackground = true,
                        BackgroundStyle = SukiBackgroundStyle.Bubble,
                        MinHeight = 700,
                        MinWidth = 350,
                        Width = 450,
                        MaxWidthScreenRatio = .75,
                    };
                    browserWindow.Closed += (_, _) => browserWindow = null;
                }

                browserWindow.Show();
                browserWindow.Activate();

                ctx.SetOutput(RxUnit.Default);
            })
            .DisposeWith(disposables);
    }
}
