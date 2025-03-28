using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Polly;
using ReactiveUI;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public partial class BrowserViewModel : BaseViewModel
{
    /// <summary>
    /// Indicates if a server is available.
    /// </summary>
    [ObservableAsProperty]
    public partial bool ServerIsRunning { get; }

    /// <summary>
    /// Indicates server status.
    /// </summary>
    [ObservableAsProperty]
    public partial BrowserStatus Status { get; }

    [ObservableAsProperty]
    public partial Settings Settings { get; }

    /// <summary>
    /// Running SDMX-DL server version.
    /// </summary>
    [ObservableAsProperty]
    public partial string? Version { get; }

    public ReactiveCommand<RxUnit, Settings> LaunchServer { get; }
    public Interaction<RxUnit, Settings> LaunchServerInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    /// <summary>
    /// Ask server to provide its version.
    /// </summary>
    public ReactiveCommand<RxUnit, string> RetrieveVersion { get; }

    public Interaction<Exception, RxUnit> DisplayErrorMessageInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<Settings, RxUnit> HostServer { get; }

    public BrowserViewModel(
        ClientFactory clientFactory,
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        DimensionsSelectorViewModel dimensionsSelectorViewModel,
        ExceptionHandler exceptionHandler,
        ResiliencePipeline pipeline
    )
    {
        HostServer = CreateCommandHostServer(clientFactory);
        LaunchServer = CreateCommandLaunchServer();

        this.WhenAnyValue(x => x.Settings).Where(x => x.IsHosting).InvokeCommand(HostServer);

        UpdateServerHostingStatus();
        UpdateServerRunningStatus();

        RetrieveVersion = CreateCommandRetrieveVersion(clientFactory, pipeline);

        this.WhenActivated(disposables =>
        {
            PromptServerSettings(disposables);

            exceptionHandler
                .Alerts.Subscribe(async ex =>
                {
                    await DisplayErrorMessageInteraction.Handle(ex);
                })
                .DisposeWith(disposables);

            FetchSourcesOnServerStartup(sourceSelectorViewModel, disposables);
            FetchDataFlowsOnSourceSelection(
                sourceSelectorViewModel,
                dataFlowSelectorViewModel,
                disposables
            );
            FetchDimensionsOnSourceAndFlowSelection(
                sourceSelectorViewModel,
                dataFlowSelectorViewModel,
                dimensionsSelectorViewModel,
                disposables
            );
        });
    }

    private void PromptServerSettings(CompositeDisposable disposables)
    {
        Observable
            .Return(RxUnit.Default)
            .Delay(TimeSpan.FromSeconds(1))
            .InvokeCommand(LaunchServer)
            .DisposeWith(disposables);
    }

    private static void FetchDimensionsOnSourceAndFlowSelection(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        DimensionsSelectorViewModel dimensionsSelectorViewModel,
        CompositeDisposable disposables
    )
    {
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
                return o.Some(Observable.Return).None(Observable.Empty<(SdmxWebSource, DataFlow)>);
            })
            .Switch()
            .InvokeCommand(dimensionsSelectorViewModel, x => x.RetrieveDimensions)
            .DisposeWith(disposables);
    }

    /// <summary>
    /// Clear dataflows on source selection and trigger retrieval if source has selection.
    /// </summary>
    private static void FetchDataFlowsOnSourceSelection(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        CompositeDisposable disposables
    )
    {
        sourceSelectorViewModel
            .WhenAnyValue(x => x.Selection)
            .Do(async _ => await dataFlowSelectorViewModel.Reset())
            .Select(o => o.Some(Observable.Return).None(Observable.Empty<SdmxWebSource>))
            .Switch()
            .InvokeCommand(dataFlowSelectorViewModel, x => x.RetrieveData)
            .DisposeWith(disposables);
    }

    /// <summary>
    /// Update hosting status according to chosen settings.
    /// Status is Offline if connection was cancelled or hosting failed.
    /// Status is Hosting if process is hosting own instance of sdmx-dl.
    /// Status is Connected if process connects to an already running instance of sdmx-dl.
    /// </summary>
    private void UpdateServerHostingStatus()
    {
        this.WhenAnyValue(x => x.Settings)
            .WhereNotNull()
            .Select(settings =>
            {
                if (settings == Settings.None)
                    return BrowserStatus.Offline;

                return settings.IsHosting ? BrowserStatus.Hosting : BrowserStatus.Connected;
            })
            .Merge(HostServer.ThrownExceptions.Select(_ => BrowserStatus.Offline))
            .ToProperty(
                this,
                x => x.Status,
                out _statusHelper,
                initialValue: BrowserStatus.Offline,
                scheduler: RxApp.MainThreadScheduler
            );
    }

    /// <summary>
    /// Trigger sources retrieval on server startup: server is flagged as running and version is defined.
    /// </summary>
    private void FetchSourcesOnServerStartup(
        SourceSelectorViewModel sourceSelectorViewModel,
        CompositeDisposable disposables
    )
    {
        this.WhenAnyValue(x => x.ServerIsRunning, x => x.Version)
            .Where(t => t.Item1 && !string.IsNullOrWhiteSpace(t.Item2))
            .DistinctUntilChanged()
            .Throttle(TimeSpan.FromSeconds(1))
            .Select(_ => RxUnit.Default)
            .InvokeCommand(sourceSelectorViewModel, x => x.RetrieveData)
            .DisposeWith(disposables);
    }

    private void UpdateServerRunningStatus()
    {
        this.WhenAnyValue(x => x.Status)
            .Select(s => s != BrowserStatus.Offline)
            .ToProperty(
                this,
                x => x.ServerIsRunning,
                out _serverIsRunningHelper,
                initialValue: false,
                scheduler: RxApp.MainThreadScheduler
            );
    }

    private ReactiveCommand<RxUnit, Settings> CreateCommandLaunchServer()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => LaunchServerInteraction.Handle(RxUnit.Default)
        );

        cmd.ToProperty(
            this,
            x => x.Settings,
            out _settingsHelper,
            initialValue: Settings.None,
            scheduler: RxApp.MainThreadScheduler
        );

        return cmd;
    }

    /// <summary>
    /// Once server is running, app will try to fetch version after 1 second then every 5 minutes.
    /// </summary>
    private ReactiveCommand<RxUnit, string> CreateCommandRetrieveVersion(
        ClientFactory clientFactory,
        ResiliencePipeline pipeline
    )
    {
        var command = ReactiveCommand.CreateRunInBackground(() =>
        {
            var about = pipeline.Execute(() => clientFactory.GetClient().GetAbout(new Empty()));
            return $"{about.Name} {about.Version}";
        });

        command.ToProperty(
            this,
            x => x.Version,
            out _versionHelper,
            initialValue: string.Empty,
            scheduler: RxApp.MainThreadScheduler
        );

        this.WhenAnyValue(x => x.ServerIsRunning)
            .Select(b =>
            {
                if (!b)
                    return Observable.Empty<RxUnit>();

                return Observable
                    .Timer(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromMinutes(5),
                        RxApp.TaskpoolScheduler
                    )
                    .Select(_ => RxUnit.Default);
            })
            .Switch()
            .InvokeCommand(command);

        return command;
    }

    private ReactiveCommand<Settings, RxUnit> CreateCommandHostServer(ClientFactory clientFactory)
    {
        var cmd = ReactiveCommand.CreateFromTask(
            async (Settings settings) =>
            {
                await clientFactory.StartServer(settings.JavaPath, settings.JarPath);
            }
        );

        cmd.ThrownExceptions.Subscribe(async ex => await DisplayErrorMessageInteraction.Handle(ex));
        return cmd;
    }
}
