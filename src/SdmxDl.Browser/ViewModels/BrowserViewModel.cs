using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public partial class BrowserViewModel : BaseViewModel
{
    [ObservableAsProperty]
    public partial bool ServerIsRunning { get; }

    [ObservableAsProperty]
    public partial BrowserStatus Status { get; }

    [ObservableAsProperty]
    public partial Settings Settings { get; }

    [ObservableAsProperty]
    public partial string? Version { get; }

    public ReactiveCommand<RxUnit, Settings> LaunchServer { get; }
    public Interaction<RxUnit, Settings> LaunchServerInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<RxUnit, string> RetrieveVersion { get; }

    public Interaction<Exception, RxUnit> DisplayErrorMessageInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public BrowserViewModel(
        ClientFactory clientFactory,
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        DimensionsSelectorViewModel dimensionsSelectorViewModel,
        ExceptionHandler exceptionHandler
    )
    {
        LaunchServer = CreateCommandLaunchServer();
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

        RetrieveVersion = CreateCommandRetrieveVersion(clientFactory);

        this.WhenActivated(disposables =>
        {
            Observable
                .Return(RxUnit.Default)
                .Delay(TimeSpan.FromSeconds(1))
                .InvokeCommand(LaunchServer)
                .DisposeWith(disposables);

            exceptionHandler.Alerts.Subscribe(async ex =>
            {
                await DisplayErrorMessageInteraction.Handle(ex);
            });

            this.WhenAnyValue(x => x.ServerIsRunning)
                .Where(x => x)
                .Select(_ => RxUnit.Default)
                .InvokeCommand(sourceSelectorViewModel, x => x.RetrieveData)
                .DisposeWith(disposables);

            sourceSelectorViewModel
                .WhenAnyValue(x => x.Selection)
                .Do(async _ => await dataFlowSelectorViewModel.Reset())
                .Select(o => o.Some(Observable.Return).None(Observable.Empty<SdmxWebSource>))
                .Switch()
                .InvokeCommand(dataFlowSelectorViewModel, x => x.RetrieveData)
                .DisposeWith(disposables);

            sourceSelectorViewModel
                .WhenAnyValue(x => x.Selection)
                .Do(_ =>
                    Observable
                        .Return(RxUnit.Default)
                        .InvokeCommand(dimensionsSelectorViewModel, x => x.Clear)
                )
                .CombineLatest(
                    dataFlowSelectorViewModel
                        .WhenAnyValue(x => x.Selection)
                        .Do(_ =>
                            Observable
                                .Return(RxUnit.Default)
                                .InvokeCommand(dimensionsSelectorViewModel, x => x.Clear)
                        )
                )
                .Throttle(TimeSpan.FromMilliseconds(50))
                .Select(t =>
                {
                    var (source, flow) = t;
                    var o = from s in source from f in flow select (s, f);
                    return o.Some(Observable.Return)
                        .None(Observable.Empty<(SdmxWebSource, DataFlow)>);
                })
                .Switch()
                .InvokeCommand(dimensionsSelectorViewModel, x => x.RetrieveDimensions)
                .DisposeWith(disposables);
        });
    }

    private ReactiveCommand<RxUnit, Settings> CreateCommandLaunchServer()
    {
        return ReactiveCommand.CreateFromObservable(
            () => LaunchServerInteraction.Handle(RxUnit.Default)
        );
    }

    private ReactiveCommand<RxUnit, string> CreateCommandRetrieveVersion(
        ClientFactory clientFactory
    )
    {
        var command = ReactiveCommand.CreateRunInBackground(() =>
        {
            var about = clientFactory.GetClient().GetAbout(new Empty());
            return $"{about.Name} {about.Version}";
        });

        command.ToProperty(
            this,
            x => x.Version,
            out _versionHelper,
            initialValue: string.Empty,
            scheduler: RxApp.MainThreadScheduler
        );

        Observable
            .Interval(TimeSpan.FromSeconds(30))
            .Where(_ => ServerIsRunning)
            .Select(_ => RxUnit.Default)
            .Merge(
                this.WhenAnyValue(x => x.ServerIsRunning).Where(x => x).Select(_ => RxUnit.Default)
            )
            .InvokeCommand(command);

        return command;
    }
}
