using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using HierarchyGrid.Definitions;
using LanguageExt;
using Polly;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ScottPlot.Plottables;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public partial class DataViewModel : BaseViewModel
{
    [Reactive]
    public partial string Title { get; set; } = string.Empty;

    [Reactive]
    public partial SeriesRequest SeriesRequest { get; set; } = SeriesRequest.Empty;

    [Reactive]
    public partial DateTimeOffset StartDate { get; set; }

    [Reactive]
    public partial DateTimeOffset EndDate { get; set; }

    [Reactive]
    public partial bool Initialized { get; set; }

    [Reactive]
    public partial bool UseLogarithmicAxis { get; set; }

    [ObservableAsProperty(ReadOnly = false)]
    private Option<DataSet> _dataSet;

    [ObservableAsProperty(ReadOnly = false)]
    private bool _isBusy;

    [ObservableAsProperty(ReadOnly = false)]
    private string? _busyMessage;

    [ObservableAsProperty(ReadOnly = false)]
    private Seq<ChartSeries> _chartSeries;

    [ObservableAsProperty(ReadOnly = false)]
    private bool _hasNoData;

    [ObservableAsProperty]
    private bool _isSplitView;

    [Reactive]
    public partial Option<(DateTime, Scatter)> HighlightedPoint { get; set; }

    [ObservableAsProperty(ReadOnly = false)]
    private HierarchyDefinitions? _standAloneHierarchyDefinitions;

    [ObservableAsProperty(ReadOnly = false)]
    private HierarchyDefinitions? _linkedHierarchyDefinitions;

    private readonly SourceCache<SeriesRequest, KeyRequest> _requestsCache =
        new(sr => (KeyRequest)sr);

    private ReadOnlyObservableCollection<SeriesRequestRootMenuItem>? _requests;
    public ReadOnlyObservableCollection<SeriesRequestRootMenuItem> Requests => _requests!;

    [ObservableAsProperty]
    private bool _hasMultipleRequests;

    public HierarchyGridViewModel StandAloneHierarchyGridViewModel { get; } =
        new()
        {
            Theme = LightGridTheme.Instance,
            DefaultColumnWidth = 250d,
            DefaultHeaderWidth = 250d,
        };

    public HierarchyGridViewModel LinkedHierarchyGridViewModel { get; } =
        new()
        {
            Theme = LightGridTheme.Instance,
            DefaultColumnWidth = 150d,
            DefaultHeaderWidth = 250d,
        };

    private ReactiveCommand<SeriesRequest, Option<DataSet>> RetrieveData { get; }
    private ReactiveCommand<DataSet, Seq<ChartSeries>> TransformData { get; }
    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        HierarchyDefinitions
    > BuildStandAloneGrid { get; }
    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        HierarchyDefinitions
    > BuildLinkedGrid { get; }

    [ObservableAsProperty(ReadOnly = false)]
    private Seq<Scatter> _linkedSeries;

    public ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        Seq<Scatter>
    > DrawCharts { get; }
    public Interaction<Seq<PlotSeries>, RxUnit> DrawStandAloneChartInteraction { get; } =
        new(RxApp.MainThreadScheduler);
    public Interaction<Seq<PlotSeries>, Seq<Scatter>> DrawLinkedChartInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<string, RxUnit> CopyToClipboard { get; }
    public Interaction<string, RxUnit> CopyToClipboardInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<Option<(DateTime, Scatter)>, RxUnit> HighlightChart { get; }
    public Interaction<Option<(DateTime, Scatter)>, RxUnit> HighlightChartInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<Option<SeriesRequest>, Seq<ChartSeries>> AddRequest { get; }

    public ReactiveCommand<string, string> Rename { get; }
    public Interaction<string, string> RenameInteraction { get; } = new(RxApp.MainThreadScheduler);

    public DataViewModel(
        ClientFactory clientFactory,
        ResiliencePipeline pipeline,
        SettingsViewModel settingsViewModel
    )
    {
        StartDate = new DateTimeOffset(new DateTime(1900, 1, 1));
        EndDate = new DateTimeOffset(new DateTime(9999, 12, 31));

        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);
        AddRequest = CreateCommandAddRequest(clientFactory, pipeline);
        TransformData = CreateCommandTransformData(AddRequest);
        BuildStandAloneGrid = CreateCommandBuildStandAloneGrid();
        BuildLinkedGrid = CreateCommandLinkedAloneGrid();
        CopyToClipboard = ReactiveCommand.CreateFromObservable(
            (string s) => CopyToClipboardInteraction.Handle(s)
        );

        DrawCharts = CreateCommandDrawCharts();

        HighlightGrid = ReactiveCommand.Create(
            (Option<(DateTime, Scatter)> o) => DoHighlightGrid(o)
        );
        HighlightChartInteraction.RegisterHandler(ctx => ctx.SetOutput(RxUnit.Default));
        HighlightChart = ReactiveCommand.CreateFromObservable(
            (Option<(DateTime, Scatter)> o) => HighlightChartInteraction.Handle(o)
        );
        Rename = CreateCommandRename();

        _isSplitViewHelper = settingsViewModel
            .WhenAnyValue(x => x.IsSplitView)
            .ToProperty(this, x => x.IsSplitView, scheduler: RxApp.MainThreadScheduler);

        _requestsCache
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Transform(r => new SeriesRequestRootMenuItem(r) { CopyToClipboard = CopyToClipboard })
            .Bind(out _requests)
            .DisposeMany()
            .Subscribe();

        _hasMultipleRequestsHelper = _requestsCache
            .Connect()
            .DisposeMany()
            .Select(_ => _requestsCache.Count > 1)
            .ToProperty(this, x => x.HasMultipleRequests);

        this.WhenAnyValue(x => x.ChartSeries)
            .Where(x => !x.IsEmpty)
            .Do(_ => Initialized = false)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(seq =>
            {
                var minDate = seq.Where(cs => !cs.Values.IsEmpty)
                    .Map(cs => cs.Values.Keys.Min())
                    .Min();
                var maxDate = seq.Where(cs => !cs.Values.IsEmpty)
                    .Map(cs => cs.Values.Keys.Max())
                    .Max();

                StartDate = minDate;
                EndDate = maxDate;

                Initialized = true;
            });

        this.WhenActivated(disposables =>
        {
            ManageBusyState(disposables);

            _hasNoDataHelper = this.WhenAnyValue(x => x.DataSet, x => x.ChartSeries)
                .Select(t =>
                {
                    var (ods, css) = t;
                    return ods.IsSome && css.IsEmpty;
                })
                .ToProperty(this, x => x.HasNoData, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.HighlightedPoint)
                .DistinctUntilChanged()
                .Throttle(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .InvokeCommand(HighlightGrid)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.HighlightedPoint)
                .DistinctUntilChanged()
                .CombineLatest(HighlightGrid.Where(x => x))
                .Select(t => t.First)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .InvokeCommand(HighlightGrid)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.HighlightedPoint)
                .DistinctUntilChanged()
                .Throttle(TimeSpan.FromMilliseconds(50))
                .InvokeCommand(HighlightChart)
                .DisposeWith(disposables);

            LinkedHierarchyGridViewModel
                .WhenAnyValue(x => x.HoveredCell)
                .Throttle(TimeSpan.FromMilliseconds(50)) // Add some delay so we don't stack refreshes
                .DistinctUntilChanged()
                .Subscribe(o =>
                    HighlightedPoint = o.Match(
                        pc =>
                            pc.ConsumerDefinition.Tag is DateTime period
                            && pc.ProducerDefinition.Content is string title
                                ? from x in LinkedSeries.Find(s => s.LegendText.Equals(title))
                                select (period, x)
                                : Option<(DateTime, Scatter)>.None,
                        () => Option<(DateTime, Scatter)>.None
                    )
                )
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.StandAloneHierarchyDefinitions)
                .WhereNotNull()
                .Subscribe(defs => StandAloneHierarchyGridViewModel.Set(defs))
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.LinkedHierarchyDefinitions)
                .WhereNotNull()
                .Subscribe(defs => LinkedHierarchyGridViewModel.Set(defs))
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ChartSeries)
                .Where(seq => !seq.IsEmpty)
                .CombineLatest(
                    this.WhenAnyValue(x => x.StartDate),
                    this.WhenAnyValue(x => x.EndDate),
                    this.WhenAnyValue(x => x.Initialized).Where(x => x)
                )
                .Throttle(TimeSpan.FromMilliseconds(150))
                .DistinctUntilChanged()
                .Select(t => (t.First, t.Second, t.Third))
                .InvokeCommand(DrawCharts)
                .DisposeWith(disposables);

            settingsViewModel
                .WhenAnyValue(x => x.IsLightTheme)
                .Subscribe(isLight =>
                {
                    ITheme theme = isLight ? LightGridTheme.Instance : DarkGridTheme.Instance;
                    StandAloneHierarchyGridViewModel.Theme = theme;
                    LinkedHierarchyGridViewModel.Theme = theme;
                })
                .DisposeWith(disposables);
        });
    }

    private ReactiveCommand<string, string> CreateCommandRename()
    {
        RenameInteraction.RegisterHandler(ctx => ctx.SetOutput(string.Empty));
        var cmd = ReactiveCommand.CreateFromObservable(
            (string title) => RenameInteraction.Handle(title)
        );

        cmd.Where(r => !string.IsNullOrWhiteSpace(r)).Subscribe(r => Title = r);

        return cmd;
    }

    private ReactiveCommand<Option<SeriesRequest>, Seq<ChartSeries>> CreateCommandAddRequest(
        ClientFactory clientFactory,
        ResiliencePipeline pipeline
    )
    {
        var cmd = ReactiveCommand.CreateFromTask(
            async (Option<SeriesRequest> oReq) =>
            {
                return await oReq.MatchAsync(
                        async req =>
                        {
                            _requestsCache.AddOrUpdate(req);
                            return await DoRetrieveDataSet(req, clientFactory, pipeline);
                        },
                        () => Option<DataSet>.None
                    )
                    .Match(
                        dataSet => dataSet.Data.Map(s => new ChartSeries(s)),
                        () => Seq<ChartSeries>.Empty
                    );
            }
        );
        return cmd;
    }

    public ReactiveCommand<Option<(DateTime, Scatter)>, bool> HighlightGrid { get; }

    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        HierarchyDefinitions
    > CreateCommandBuildStandAloneGrid()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            ((Seq<ChartSeries>, DateTimeOffset, DateTimeOffset) t) =>
            {
                var (series, start, end) = t;
                return BuildStandAloneGridImpl(series, start, end);
            }
        );

        this.WhenAnyValue(x => x.ChartSeries)
            .Where(seq => !seq.IsEmpty)
            .CombineLatest(this.WhenAnyValue(x => x.StartDate), this.WhenAnyValue(x => x.EndDate))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .InvokeCommand(cmd);

        _standAloneHierarchyDefinitionsHelper = cmd.ToProperty(
            this,
            x => x.StandAloneHierarchyDefinitions,
            scheduler: RxApp.MainThreadScheduler
        );

        return cmd;
    }

    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        Seq<Scatter>
    > CreateCommandDrawCharts()
    {
        DrawStandAloneChartInteraction.RegisterHandler(ctx => ctx.SetOutput(RxUnit.Default));
        DrawLinkedChartInteraction.RegisterHandler(ctx => ctx.SetOutput(Seq<Scatter>.Empty));

        var cmd = ReactiveCommand.CreateFromTask(
            async ((Seq<ChartSeries>, DateTimeOffset, DateTimeOffset) t) =>
            {
                var (series, start, end) = t;
                var plotSeries = series.ToPlotSeries(start, end);
                await DrawStandAloneChartInteraction.Handle(plotSeries);
                var scatters = await DrawLinkedChartInteraction.Handle(plotSeries);
                return scatters;
            }
        );

        _linkedSeriesHelper = cmd.ToProperty(this, x => x.LinkedSeries);

        return cmd;
    }

    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        HierarchyDefinitions
    > CreateCommandLinkedAloneGrid()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            ((Seq<ChartSeries>, DateTimeOffset, DateTimeOffset) t) =>
            {
                var (series, start, end) = t;
                return BuildLinkedGridImpl(series, start, end);
            }
        );

        this.WhenAnyValue(x => x.ChartSeries)
            .Where(seq => !seq.IsEmpty)
            .CombineLatest(this.WhenAnyValue(x => x.StartDate), this.WhenAnyValue(x => x.EndDate))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .InvokeCommand(cmd);

        _linkedHierarchyDefinitionsHelper = cmd.ToProperty(
            this,
            x => x.LinkedHierarchyDefinitions,
            scheduler: RxApp.MainThreadScheduler
        );

        return cmd;
    }

    private HierarchyDefinitions BuildStandAloneGridImpl(
        Seq<ChartSeries> series,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        var formatter = series.GetHighestFreq().GetFormatter();
        var data = series.Map(s => (s.Key, s.Values)).ToHashMap();

        var producerDefinitions = series
            .GetDates()
            .Where(x => x >= start && x <= end)
            .OrderBy(x => x)
            .Select(x => new ProducerDefinition() { Content = formatter(x), Producer = () => x })
            .ToSeq();

        var consumerDefinitions = series.Map(s => new ConsumerDefinition()
        {
            Content = s.Title,
            Consumer = o =>
                o switch
                {
                    DateTime d => data.Find(s.Key, d, v => v, () => Option<double>.None),
                    _ => Option<double>.None,
                },
            Formatter = o =>
                o switch
                {
                    Option<double> d => d.Match(x => x.ToString(s.Format), () => string.Empty),
                    _ => string.Empty,
                },
            Qualify = o =>
                o switch
                {
                    Option<double> d
                        => d.Match(x => Qualification.Normal, () => Qualification.Empty),
                    _ => Qualification.Empty,
                },
        });

        return new HierarchyDefinitions(producerDefinitions, consumerDefinitions);
    }

    private HierarchyDefinitions BuildLinkedGridImpl(
        Seq<ChartSeries> series,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        var formatter = series.GetHighestFreq().GetFormatter();
        var data = series.Map(s => (s.Key, s.Values)).ToHashMap();

        var producerDefinitions = series
            .Map(s => new ProducerDefinition() { Content = s.Title, Producer = () => s })
            .ToSeq();

        var consumerDefinitions = series
            .GetDates()
            .Where(x => x >= start && x <= end)
            .OrderBy(x => x)
            .Map(d => new ConsumerDefinition()
            {
                Content = formatter(d),
                Consumer = o =>
                    o switch
                    {
                        ChartSeries s
                            => from x in data.Find(s.Key, d)
                            from v in x
                            select Tuple.Create(v, s.Format),
                        _ => Option<Tuple<double, string>>.None,
                    },
                Formatter = o =>
                    o switch
                    {
                        Option<Tuple<double, string>> t
                            => t.Match(x => x.Item1.ToString(x.Item2), () => string.Empty),
                        _ => string.Empty,
                    },
                Qualify = o =>
                    o switch
                    {
                        Option<Tuple<double, string>> d
                            => d.Match(x => Qualification.Normal, () => Qualification.Empty),
                        _ => Qualification.Empty,
                    },
                Tag = d,
            });

        return new HierarchyDefinitions(producerDefinitions, consumerDefinitions);
    }

    private void ManageBusyState(CompositeDisposable disposables)
    {
        _isBusyHelper = RetrieveData
            .IsExecuting.CombineLatest(
                TransformData.IsExecuting,
                BuildStandAloneGrid.IsExecuting,
                BuildLinkedGrid.IsExecuting,
                DrawCharts.IsExecuting
            )
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(t => t.First || t.Second || t.Third || t.Fourth || t.Fifth)
            .DistinctUntilChanged()
            .ToProperty(this, x => x.IsBusy, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);

        _busyMessageHelper = RetrieveData
            .IsExecuting.Where(x => x)
            .Select(_ => "Retrieving data")
            .Merge(TransformData.IsExecuting.Where(x => x).Select(_ => "Parsing results"))
            .ToProperty(this, x => x.BusyMessage, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    private ReactiveCommand<DataSet, Seq<ChartSeries>> CreateCommandTransformData(
        ReactiveCommand<Option<SeriesRequest>, Seq<ChartSeries>> addRequest
    )
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            (DataSet ds) => ds.Data.Map(s => new ChartSeries(s))
        );

        _chartSeriesHelper = cmd.Merge(addRequest)
            .Scan(Seq<ChartSeries>.Empty, (o, p) => o.Append(p))
            .ToProperty(this, x => x.ChartSeries, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.DataSet)
            .Select(d => d.Match(Observable.Return, Observable.Empty<DataSet>))
            .Switch()
            .InvokeCommand(cmd);

        return cmd;
    }

    /// <summary>
    /// Creates a ReactiveCommand to retrieve data asynchronously using the specified client factory and resilience pipeline.
    /// The command operates on a tuple of <see cref="SdmxWebSource"/>, <see cref="DataFlow"/>, and a key string,
    /// and returns an optional <see cref="DataSet"/>.
    /// </summary>
    /// <param name="clientFactory">An instance of <see cref="ClientFactory"/> used to create a client for fetching data.</param>
    /// <param name="pipeline">An instance of <see cref="ResiliencePipeline"/> providing resilience strategies for the data retrieval operation.</param>
    /// <returns>A ReactiveCommand that retrieves data from a source asynchronously returning an optional DataSet.</returns>
    private ReactiveCommand<SeriesRequest, Option<DataSet>> CreateCommandRetrieveData(
        ClientFactory clientFactory,
        ResiliencePipeline pipeline
    )
    {
        var cmd = ReactiveCommand.CreateFromTask<SeriesRequest, Option<DataSet>>(sr =>
            DoRetrieveDataSet(sr, clientFactory, pipeline)
        );

        _dataSetHelper = cmd.ToProperty(this, x => x.DataSet, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.SeriesRequest)
            .Where(x => x != SeriesRequest.Empty)
            .Do(sr => _requestsCache.AddOrUpdate(sr))
            .InvokeCommand(cmd);

        return cmd;
    }

    private bool DoHighlightGrid(Option<(DateTime, Scatter)> highlightOpt)
    {
        var cell = highlightOpt.Match(
            o =>
            {
                var (period, scatter) = o;
                return from pCell in LinkedHierarchyGridViewModel.DrawnCells.Find(pc =>
                        pc.ConsumerDefinition.Tag!.Equals(period)
                        && pc.ProducerDefinition.Content!.Equals(scatter.LegendText)
                    )
                    select pCell;
            },
            () => Option<PositionedCell>.None
        );

        if (cell.IsNone) /* Cell is not drawn */
        {
            var hOffset = highlightOpt.Match(
                o =>
                {
                    var (period, scatter) = o;

                    if (
                        !LinkedHierarchyGridViewModel.DrawnCells.Exists(pc =>
                            pc.ConsumerDefinition.Tag!.Equals(period)
                        )
                    )
                    {
                        return LinkedHierarchyGridViewModel
                            .ColumnsDefinitions.OfType<ConsumerDefinition>()
                            .Find(x => x.Tag!.Equals(period))
                            .Match(p => p.Position, () => -1);
                    }

                    return -1;
                },
                () => -1
            );

            if (hOffset != -1)
                LinkedHierarchyGridViewModel.HorizontalOffset = hOffset;

            var vOffset = highlightOpt.Match(
                o =>
                {
                    var (_, scatter) = o;

                    if (
                        !LinkedHierarchyGridViewModel.DrawnCells.Exists(pc =>
                            pc.ProducerDefinition.Content!.Equals(scatter.LegendText)
                        )
                    )
                    {
                        return LinkedHierarchyGridViewModel
                            .RowsDefinitions.OfType<ProducerDefinition>()
                            .Find(x => x.Content!.Equals(scatter.LegendText))
                            .Match(p => p.Position, () => -1);
                    }

                    return -1;
                },
                () => -1
            );

            if (vOffset != -1)
                LinkedHierarchyGridViewModel.VerticalOffset = vOffset;

            return hOffset == -1 || vOffset == -1; /* Trigger the method again with elements that should have been drawn */
        }

        LinkedHierarchyGridViewModel.HoveredCell = cell;

        LinkedHierarchyGridViewModel.HoveredColumn = cell.Match(
            c => c.ConsumerDefinition!.Position,
            () => -1
        );

        LinkedHierarchyGridViewModel.HoveredRow = cell.Match(
            c => c.ProducerDefinition!.Position,
            () => -1
        );

        Observable
            .Return(false)
            .InvokeCommand(LinkedHierarchyGridViewModel, x => x.DrawGridCommand);

        return false;
    }

    private static async Task<Option<DataSet>> DoRetrieveDataSet(
        SeriesRequest seriesRequest,
        ClientFactory clientFactory,
        ResiliencePipeline pipeline
    )
    {
        var dataSet = await pipeline.ExecuteAsync(
            async token =>
                await clientFactory
                    .GetClient()
                    .GetDataAsync((KeyRequest)seriesRequest, cancellationToken: token),
            CancellationToken.None
        );

        return dataSet.ToModel();
    }
}
