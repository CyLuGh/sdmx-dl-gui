using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Polly;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class BrowserViewModel : BaseViewModel
{
    /// <summary>
    /// Indicates if a server is available.
    /// </summary>
    public bool ServerIsRunning
    {
        [ObservableAsProperty]
        get;
    }

    /// <summary>
    /// Indicates server status.
    /// </summary>
    public BrowserStatus Status
    {
        [ObservableAsProperty]
        get;
    }

    /// <summary>
    /// Running SDMX-DL server version.
    /// </summary>
    public string? Version
    {
        [ObservableAsProperty]
        get;
    }

    public bool IsBusy
    {
        [ObservableAsProperty]
        get;
    }

    public string? BusyMessage
    {
        [ObservableAsProperty]
        get;
    }

    public RxCommand LaunchServer { get; }
    public RxInteraction LaunchServerInteraction { get; } = new(RxApp.MainThreadScheduler);

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

        ViewModelLocator
            .SettingsViewModel.WhenAnyValue(x => x.CurrentSettings)
            .Where(x => x.IsHosting)
            .InvokeCommand(HostServer);

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

            ManageBusyStatus(sourceSelectorViewModel, dataFlowSelectorViewModel, disposables);
        });
    }

    private void ManageBusyStatus(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        CompositeDisposable disposables
    )
    {
        sourceSelectorViewModel
            .RetrieveData.IsExecuting.CombineLatest(
                dataFlowSelectorViewModel.RetrieveData.IsExecuting
            )
            .Select(t =>
            {
                var (isRetrievingSources, isRetrievingFlows) = t;
                return isRetrievingSources || isRetrievingFlows;
            })
            .ToPropertyEx(this, x => x.IsBusy, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);

        sourceSelectorViewModel
            .RetrieveData.IsExecuting.Where(x => x)
            .Select(_ => "Retrieving sources...")
            .Merge(
                dataFlowSelectorViewModel
                    .RetrieveData.IsExecuting.Where(x => x)
                    .Select(_ => "Retrieving data flows...")
            )
            .ToPropertyEx(this, x => x.BusyMessage, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
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
        ViewModelLocator
            .SettingsViewModel.WhenAnyValue(x => x.CurrentSettings)
            .WhereNotNull()
            .Select(settings =>
            {
                if (settings == Settings.None)
                    return BrowserStatus.Offline;

                return settings.IsHosting ? BrowserStatus.Hosting : BrowserStatus.Connected;
            })
            .Merge(HostServer.ThrownExceptions.Select(_ => BrowserStatus.Offline))
            .ToPropertyEx(
                this,
                x => x.Status,
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
            .ToPropertyEx(
                this,
                x => x.ServerIsRunning,
                initialValue: false,
                scheduler: RxApp.MainThreadScheduler
            );
    }

    private RxCommand CreateCommandLaunchServer()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => LaunchServerInteraction.Handle(RxUnit.Default)
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

        command.ToPropertyEx(
            this,
            x => x.Version,
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
