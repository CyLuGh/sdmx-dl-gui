using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Engine;

namespace SdmxDl.Browser.ViewModels;

public partial class BrowserViewModel : BaseViewModel
{
    [ObservableAsProperty]
    public partial bool ServerIsRunning { get; }

    [ObservableAsProperty]
    public partial BrowserStatus Status { get; }

    [ObservableAsProperty]
    public partial Settings Settings { get; }

    public ReactiveCommand<RxUnit, Settings> LaunchServer { get; }
    public Interaction<RxUnit, Settings> LaunchServerInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<RxUnit, string> RetrieveVersion { get; }

    public BrowserViewModel(ClientFactory clientFactory)
    {
        LaunchServer = ReactiveCommand.CreateFromObservable(
            () => LaunchServerInteraction.Handle(RxUnit.Default)
        );

        RetrieveVersion = CreateCommandRetrieveVersion(clientFactory);

        LaunchServer.ToProperty(
            this,
            x => x.Settings,
            out _settingsHelper,
            initialValue: Settings.None,
            scheduler: RxApp.MainThreadScheduler
        );

        this.WhenAnyValue(x => x.Settings)
            .WhereNotNull()
            .Select(settings =>
            {
                if (settings == Settings.None)
                    return BrowserStatus.Offline;

                return settings.IsHosting ? BrowserStatus.Hosting : BrowserStatus.Connected;
            })
            .ToProperty(
                this,
                x => x.Status,
                out _statusHelper,
                initialValue: BrowserStatus.Offline,
                scheduler: RxApp.MainThreadScheduler
            );

        this.WhenAnyValue(x => x.Status)
            .Select(s => s != BrowserStatus.Offline)
            .ToProperty(
                this,
                x => x.ServerIsRunning,
                out _serverIsRunningHelper,
                initialValue: false,
                scheduler: RxApp.MainThreadScheduler
            );

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.Status)
                .Throttle(TimeSpan.FromSeconds(1))
                .Where(status => status == BrowserStatus.Offline)
                .Select(_ => RxUnit.Default)
                .InvokeCommand(LaunchServer)
                .DisposeWith(disposables);
        });
    }

    private static ReactiveCommand<RxUnit, string> CreateCommandRetrieveVersion(
        ClientFactory clientFactory
    )
    {
        var command = ReactiveCommand.CreateRunInBackground(() =>
        {
            return "?";
        });

        return command;
    }
}
