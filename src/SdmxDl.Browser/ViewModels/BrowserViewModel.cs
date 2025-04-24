using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Jot;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Polly;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;
using Splat;

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

    [Reactive]
    public string SelectionKey { get; set; }

    [Reactive]
    public string? Argument { get; set; }

    public RxCommand ConfigureServer { get; }
    public RxInteraction ConfigureServerInteraction { get; } = new(RxApp.MainThreadScheduler);

    public RxCommand LookupSeries { get; }
    public RxInteraction LookupSeriesInteraction { get; } = new(RxApp.MainThreadScheduler);

    /// <summary>
    /// Ask server to provide its version.
    /// </summary>
    public ReactiveCommand<RxUnit, string> RetrieveVersion { get; }

    public ReactiveCommand<
        (Seq<PositionedDimensionViewModel>, HierarchicalDimensionViewModel?),
        string
    > BuildSelectionKey { get; }

    public Interaction<Exception, RxUnit> DisplayErrorMessageInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<Settings, RxUnit> HostServer { get; }

    public RxCommand ShowResults { get; }
    public ReactiveCommand<(SdmxWebSource, DataFlow, string), RxUnit> SendResults { get; }
    public Interaction<(SdmxWebSource, DataFlow, string), RxUnit> ShowResultsInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<KeyEventArgs, RxUnit> CheckKeyTextBox { get; }

    public ReactiveCommand<string, RxUnit> Close { get; }
    public Interaction<string, RxUnit> CloseInteraction { get; } = new(RxApp.MainThreadScheduler);

    public BrowserViewModel(
        ClientFactory clientFactory,
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        DimensionsSelectorViewModel dimensionsSelectorViewModel,
        ExceptionHandler exceptionHandler,
        ResiliencePipeline pipeline
    )
    {
        ConfigureServer = CreateCommandConfigureServer();
        HostServer = CreateCommandHostServer(clientFactory);
        RetrieveVersion = CreateCommandRetrieveVersion(clientFactory, pipeline);

        LookupSeries = ReactiveCommand.CreateFromObservable(
            () => LookupSeriesInteraction.Handle(RxUnit.Default),
            this.WhenAnyValue(x => x.ServerIsRunning).ObserveOn(RxApp.MainThreadScheduler)
        );

        BuildSelectionKey = CreateCommandBuildSelectionKey();

        ViewModelLocator
            .SettingsViewModel.WhenAnyValue(x => x.CurrentSettings)
            .Where(x => x.IsHosting)
            .InvokeCommand(HostServer);

        UpdateServerHostingStatus();
        UpdateServerRunningStatus();

        ShowResults = CreateCommandShowResults(
            sourceSelectorViewModel,
            dataFlowSelectorViewModel,
            dimensionsSelectorViewModel
        );
        SendResults = ReactiveCommand.CreateFromObservable(
            ((SdmxWebSource, DataFlow, string) t) => ShowResultsInteraction.Handle(t)
        );
        CheckKeyTextBox = CreateCommandCheckKeyTextBoxInput();

        Close = ReactiveCommand.CreateFromObservable((string s) => CloseInteraction.Handle(s));

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.Status)
                .Where(s => s == BrowserStatus.Failed)
                .Select(_ => RxUnit.Default)
                .InvokeCommand(ConfigureServer)
                .DisposeWith(disposables);

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

            ManageBusyStatus(
                sourceSelectorViewModel,
                dataFlowSelectorViewModel,
                dimensionsSelectorViewModel,
                disposables
            );

            dimensionsSelectorViewModel
                .WhenAnyValue(x => x.PositionedDimensions, x => x.SelectedDimension)
                .InvokeCommand(BuildSelectionKey)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Argument)
                .Where(s => !string.IsNullOrEmpty(s))
                .CombineLatest(
                    this.WhenAnyValue(x => x.Version).Where(s => !string.IsNullOrEmpty(s))
                )
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(t => t.First)
                .Select(async s =>
                {
                    var elements = await SeriesFinderViewModel.ParseQueryImpl(s, clientFactory);
                    return elements.Match(
                        Observable.Return,
                        Observable.Empty<(SdmxWebSource, DataFlow, string)>
                    );
                })
                .Switch()
                .Switch()
                .InvokeCommand(SendResults)
                .DisposeWith(disposables);
        });

        SelectionKey = string.Empty;
    }

    private RxCommand CreateCommandShowResults(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        DimensionsSelectorViewModel dimensionsSelectorViewModel
    )
    {
        var canShowResults = sourceSelectorViewModel
            .WhenAnyValue(x => x.Selection)
            .Select(x => x.IsSome)
            .CombineLatest(
                dataFlowSelectorViewModel.WhenAnyValue(x => x.Selection).Select(x => x.IsSome),
                dimensionsSelectorViewModel.WhenAnyValue(x => x.DataStructure),
                this.WhenAnyValue(x => x.SelectionKey)
            )
            .Select(t =>
            {
                var (source, flow, dataStructure, key) = t;

                var dimensions = dataStructure.Match(
                    ds => ds.Dimensions,
                    () => Seq<Dimension>.Empty
                );

                return source
                    && flow
                    && !string.IsNullOrWhiteSpace(key)
                    && key.Split('.').Length == dimensions.Length
                    && dimensions.CheckComponents(key);
            })
            .ObserveOn(RxApp.MainThreadScheduler);

        var showResults = ReactiveCommand.CreateFromObservable(
            () =>
                ShowResultsInteraction.Handle(
                    (
                        sourceSelectorViewModel.Selection.ValueUnsafe(),
                        dataFlowSelectorViewModel.Selection.ValueUnsafe(),
                        SelectionKey
                    )
                ),
            canShowResults
        );

        return showResults;
    }

    private void ManageBusyStatus(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        DimensionsSelectorViewModel dimensionsSelectorViewModel,
        CompositeDisposable disposables
    )
    {
        sourceSelectorViewModel
            .RetrieveData.IsExecuting.CombineLatest(
                dataFlowSelectorViewModel.RetrieveData.IsExecuting,
                dimensionsSelectorViewModel.RetrieveDimensions.IsExecuting
            )
            .Select(t =>
            {
                var (isRetrievingSources, isRetrievingFlows, isRetrievingDimensions) = t;
                return isRetrievingSources || isRetrievingFlows || isRetrievingDimensions;
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
            .Merge(
                dimensionsSelectorViewModel
                    .RetrieveDimensions.IsExecuting.Where(x => x)
                    .Select(_ => "Retrieving dimensions...")
            )
            .ToPropertyEx(this, x => x.BusyMessage, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    private void PromptServerSettings(CompositeDisposable disposables)
    {
        var oldSettings = Observable
            .Return(RxUnit.Default)
            .Delay(TimeSpan.FromMilliseconds(400))
            .Select(_ =>
            {
                var tryGetConfig = Prelude.Try(() =>
                {
                    var tracker = Locator.Current.GetService<Tracker>();
                    var store = tracker.Store.GetData("SettingsViewModel");
                    var settings = new Settings()
                    {
                        IsHosting = (bool)store["x.UseRunningServer"],
                        JarPath = (string)store["x.JarPath"],
                        JavaPath = (string)store["x.JavaPath"],
                        ServerUri = (string)store["x.ServerUri"],
                    };
                    return settings;
                });

                return tryGetConfig().Match(s => s, _ => Option<Settings>.None);
            });

        oldSettings
            .Where(x => x.IsSome)
            .Select(_ => RxUnit.Default)
            .InvokeCommand(ViewModelLocator.SettingsViewModel, x => x.ReloadSettings)
            .DisposeWith(disposables);

        oldSettings
            .Where(x => x.IsNone)
            .Select(_ => RxUnit.Default)
            .InvokeCommand(ConfigureServer)
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
            .Throttle(TimeSpan.FromMilliseconds(200))
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
            .Do(async _ =>
            {
                dataFlowSelectorViewModel.Selection = Option<DataFlow>.None;
                await dataFlowSelectorViewModel.Reset();
            })
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
        var isConnected = this.WhenAnyValue(x => x.Status)
            .Where(x => x == BrowserStatus.Connecting)
            .CombineLatest(this.WhenAnyValue(x => x.Version).Where(s => !string.IsNullOrEmpty(s)))
            .Select(_ => BrowserStatus.Connected);
        var isOffline = HostServer.ThrownExceptions.Select(_ => BrowserStatus.Offline);
        var isFailed = RetrieveVersion.ThrownExceptions.Select(_ => BrowserStatus.Failed);
        var startingUp = ViewModelLocator
            .SettingsViewModel.WhenAnyValue(x => x.CurrentSettings)
            .WhereNotNull()
            .Select(settings =>
            {
                if (settings == Settings.None)
                    return BrowserStatus.Offline;

                return settings.IsHosting ? BrowserStatus.Hosting : BrowserStatus.Connecting;
            });

        Observable
            .Merge(startingUp, isConnected, isOffline, isFailed)
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
            .Select(s => s is BrowserStatus.Connected or BrowserStatus.Hosting)
            .ToPropertyEx(
                this,
                x => x.ServerIsRunning,
                initialValue: false,
                scheduler: RxApp.MainThreadScheduler
            );
    }

    private RxCommand CreateCommandConfigureServer()
    {
        var cmd = ReactiveCommand.CreateFromObservable(
            () => ConfigureServerInteraction.Handle(RxUnit.Default)
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

        this.WhenAnyValue(x => x.Status)
            .Throttle(TimeSpan.FromMilliseconds(10))
            .Select(status =>
            {
                if (
                    status
                    is not (
                        BrowserStatus.Connected
                        or BrowserStatus.Hosting
                        or BrowserStatus.Connecting
                    )
                )
                {
                    return Observable.Empty<RxUnit>();
                }

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
        cmd.ThrownExceptions.Select(_ => RxUnit.Default).InvokeCommand(ConfigureServer);
        return cmd;
    }

    private ReactiveCommand<
        (Seq<PositionedDimensionViewModel>, HierarchicalDimensionViewModel?),
        string
    > CreateCommandBuildSelectionKey()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            ((Seq<PositionedDimensionViewModel>, HierarchicalDimensionViewModel?) t) =>
            {
                var (dimensions, selection) = t;

                if (dimensions.IsEmpty || selection is null)
                    return string.Empty;

                return string.Join(
                    ".",
                    dimensions
                        .OrderBy(d => d.Dimension.Position)
                        .Select(d =>
                            selection.Keys.Find(d.Dimension.Position, k => k, () => string.Empty)
                        )
                );
            }
        );

        cmd.Subscribe(s => SelectionKey = s);

        return cmd;
    }

    private ReactiveCommand<KeyEventArgs, RxUnit> CreateCommandCheckKeyTextBoxInput()
    {
        return ReactiveCommand.Create(
            (KeyEventArgs args) =>
            {
                switch (args.Key)
                {
                    case Key.Return:
                        Observable.Return(RxUnit.Default).InvokeCommand(ShowResults);
                        break;
                }
            }
        );
    }
}
