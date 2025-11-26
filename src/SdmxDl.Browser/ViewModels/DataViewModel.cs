using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using HierarchyGrid.Definitions;
using LanguageExt;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Events;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Polly;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public partial class DataViewModel : BaseViewModel
{
    [Reactive]
    public partial Option<SdmxWebSource> Source { get; set; }

    [Reactive]
    public partial Option<DataFlow> Flow { get; set; }

    [Reactive]
    public partial Option<string> Key { get; set; }

    [Reactive]
    public partial bool IsSplitView { get; set; }

    [Reactive]
    public partial DateTimeOffset StartDate { get; set; }

    [Reactive]
    public partial DateTimeOffset EndDate { get; set; }

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
    private Seq<LineSeries<DateTimePoint>> _lineSeries;

    [ObservableAsProperty(ReadOnly = false)]
    private ICartesianAxis[]? _xAxes;

    [ObservableAsProperty(ReadOnly = false)]
    private ICartesianAxis[]? _yAxes;

    public LiveChartsCore.Measure.Margin Margins { get; } = new(10, 50);

    [ObservableAsProperty(ReadOnly = false)]
    private bool _hasNoData;

    [Reactive]
    public partial Option<DateTime> HighlightedPoint { get; set; }

    [ObservableAsProperty(ReadOnly = false)]
    private HierarchyDefinitions? _standAloneHierarchyDefinitions;

    [ObservableAsProperty(ReadOnly = false)]
    private HierarchyDefinitions? _linkedHierarchyDefinitions;

    public HierarchyGridViewModel StandAloneHierarchyGridViewModel { get; } =
        new()
        {
            Theme = GridTheme.Instance,
            DefaultColumnWidth = 250d,
            DefaultHeaderWidth = 250d,
        };

    public HierarchyGridViewModel LinkedHierarchyGridViewModel { get; } =
        new()
        {
            Theme = GridTheme.Instance,
            DefaultColumnWidth = 150d,
            DefaultHeaderWidth = 250d,
        };

    public static string BuildTitle(SdmxWebSource source, DataFlow flow, string key) =>
        $"{source.Id} {flow.Ref} {key}";

    public string Title => BuildTitle(Source, Flow, Key).Match(s => s, () => string.Empty);
    public string Uri => BuildUri(Source, Flow, Key).Match(s => s, () => string.Empty);
    public string SourceId => Source.Match(s => s.Id, () => string.Empty);
    public string FlowRef => Flow.Match(s => s.Ref, () => string.Empty);
    public string FullKey => Key.Match(s => s, () => string.Empty);

    public string FetchData => $"sdmx-dl fetch data \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";
    public string FetchMeta => $"sdmx-dl fetch meta \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";
    public string FetchKeys => $"sdmx-dl fetch keys \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";

    private ReactiveCommand<
        (SdmxWebSource, DataFlow, string),
        Option<DataSet>
    > RetrieveData { get; }
    private ReactiveCommand<DataSet, Seq<ChartSeries>> TransformData { get; }
    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        HierarchyDefinitions
    > BuildStandAloneGrid { get; }
    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        HierarchyDefinitions
    > BuildLinkedGrid { get; }

    public ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        RxUnit
    > DrawStandAloneChart { get; }
    public Interaction<Seq<PlotSeries>, RxUnit> DrawStandAloneChartInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<Seq<ChartSeries>, ICartesianAxis[]> SetAxes { get; }

    public ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        Seq<LineSeries<DateTimePoint>>
    > SetLinesSeries { get; }
    public ReactiveCommand<string, RxUnit> CopyToClipboard { get; }
    public Interaction<string, RxUnit> CopyToClipboardInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public ReactiveCommand<HoverCommandArgs, RxUnit> HoveredPointsChanged { get; }

    public ReactiveCommand<Option<DateTime>, RxUnit> HighlightChart { get; }
    public Interaction<Option<DateTime>, RxUnit> HighlightChartInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public DataViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        StartDate = new DateTimeOffset(new DateTime(1900, 1, 1));
        EndDate = new DateTimeOffset(new DateTime(9999, 12, 31));

        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);
        TransformData = CreateCommandTransformData();
        BuildStandAloneGrid = CreateCommandBuildStandAloneGrid();
        BuildLinkedGrid = CreateCommandLinkedAloneGrid();
        CopyToClipboard = ReactiveCommand.CreateFromObservable(
            (string s) => CopyToClipboardInteraction.Handle(s)
        );

        DrawStandAloneChart = CreateCommandDrawStandAloneChart();

        HighlightGrid = ReactiveCommand.Create((Option<DateTime> o) => HighlightGridImpl(o));

        HighlightChart = ReactiveCommand.CreateFromObservable(
            (Option<DateTime> o) => HighlightChartInteraction.Handle(o)
        );

        HoveredPointsChanged = ReactiveCommand.Create(
            (HoverCommandArgs args) =>
            {
                if (args.NewPoints?.Count() > 0)
                {
                    HighlightedPoint = new DateTime(
                        (long)args.NewPoints.ToArray()[0].Coordinate.SecondaryValue
                    );
                }
                else
                {
                    HighlightedPoint = Option<DateTime>.None;
                }
            }
        );

        SetLinesSeries = CreateCommandSetLinesSeries();
        SetAxes = CreateCommandSetAxes();

        this.WhenAnyValue(x => x.ChartSeries)
            .Where(x => !x.IsEmpty)
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
            });

        this.WhenActivated(disposables =>
        {
            ManageBusyState(disposables);

            _yAxesHelper = this.WhenAnyValue(x => x.UseLogarithmicAxis)
                .Select<bool, ICartesianAxis[]>(isLog =>
                    isLog ? [new LogarithmicAxis(10)] : [new Axis()]
                )
                .ToProperty(this, x => x.YAxes, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(disposables);

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
                .Subscribe(o =>
                {
                    HighlightedPoint = o.Match(
                        x => (DateTime)x.ConsumerDefinition.Tag!,
                        () => Option<DateTime>.None
                    );
                });

            this.WhenAnyValue(x => x.StandAloneHierarchyDefinitions)
                .WhereNotNull()
                .Subscribe(defs => StandAloneHierarchyGridViewModel.Set(defs))
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.LinkedHierarchyDefinitions)
                .WhereNotNull()
                .Subscribe(defs => LinkedHierarchyGridViewModel.Set(defs))
                .DisposeWith(disposables);
        });
    }

    public void Add(SeriesRequest request) { }

    public ReactiveCommand<Option<DateTime>, bool> HighlightGrid { get; }

    private ReactiveCommand<
        (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
        Seq<LineSeries<DateTimePoint>>
    > CreateCommandSetLinesSeries()
    {
        var cmd = ReactiveCommand.CreateRunInBackground<
            (Seq<ChartSeries>, DateTimeOffset, DateTimeOffset),
            Seq<LineSeries<DateTimePoint>>
        >(x =>
        {
            var (seq, startDate, endDate) = x;
            return seq.ToLineSeries(startDate, endDate);
        });

        _lineSeriesHelper = cmd.ToProperty(
            this,
            x => x.LineSeries,
            scheduler: RxApp.MainThreadScheduler
        );
        this.WhenAnyValue(x => x.ChartSeries, x => x.StartDate, x => x.EndDate)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .InvokeCommand(cmd);
        return cmd;
    }

    private ReactiveCommand<Seq<ChartSeries>, ICartesianAxis[]> CreateCommandSetAxes()
    {
        var cmd = ReactiveCommand.CreateRunInBackground<Seq<ChartSeries>, ICartesianAxis[]>(
            series =>
            {
                var highestFreq = series.GetHighestFreq();
                var unit = TimeSpan.FromDays(365.25 / (int)highestFreq);
                var formatter = highestFreq.GetFormatter();
                return new[] { new DateTimeAxis(unit, formatter) };
            }
        );

        _xAxesHelper = cmd.ToProperty(this, x => x.XAxes, scheduler: RxApp.MainThreadScheduler);
        this.WhenAnyValue(x => x.ChartSeries).Where(seq => !seq.IsEmpty).InvokeCommand(cmd);

        return cmd;
    }

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
        RxUnit
    > CreateCommandDrawStandAloneChart()
    {
        DrawStandAloneChartInteraction.RegisterHandler(ctx => ctx.SetOutput(RxUnit.Default));
        var cmd = ReactiveCommand.CreateFromTask(
            async ((Seq<ChartSeries>, DateTimeOffset, DateTimeOffset) t) =>
            {
                var (series, start, end) = t;
                await DrawStandAloneChartInteraction.Handle(series.ToPlotSeries(start, end));
            }
        );

        this.WhenAnyValue(x => x.ChartSeries)
            .Where(seq => !seq.IsEmpty)
            .CombineLatest(this.WhenAnyValue(x => x.StartDate), this.WhenAnyValue(x => x.EndDate))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .InvokeCommand(cmd);

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
            .IsExecuting.CombineLatest(TransformData.IsExecuting)
            .Throttle(TimeSpan.FromMilliseconds(25))
            .Select(t => t.First || t.Second)
            .ToProperty(this, x => x.IsBusy, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);

        _busyMessageHelper = RetrieveData
            .IsExecuting.Where(x => x)
            .Select(_ => "Retrieving data")
            .Merge(TransformData.IsExecuting.Where(x => x).Select(_ => "Parsing results"))
            .ToProperty(this, x => x.BusyMessage, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    private ReactiveCommand<DataSet, Seq<ChartSeries>> CreateCommandTransformData()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            (DataSet ds) => ds.Data.Map(s => new ChartSeries(s))
        );

        _chartSeriesHelper = cmd.ToProperty(
            this,
            x => x.ChartSeries,
            scheduler: RxApp.MainThreadScheduler
        );

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
    private ReactiveCommand<
        (SdmxWebSource, DataFlow, string),
        Option<DataSet>
    > CreateCommandRetrieveData(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        var cmd = ReactiveCommand.CreateFromTask<
            (SdmxWebSource, DataFlow, string),
            Option<DataSet>
        >(async t =>
        {
            var (source, flow, key) = t;

            var dataSet = await clientFactory
                .GetClient()
                .GetDataAsync(
                    new()
                    {
                        Source = source.Id,
                        Flow = flow.Ref,
                        Key = key,
                    }
                );

            return dataSet.ToModel();
        });

        _dataSetHelper = cmd.ToProperty(this, x => x.DataSet, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.Source)
            .CombineLatest(this.WhenAnyValue(x => x.Flow), this.WhenAnyValue(x => x.Key))
            .Select(t =>
            {
                var (source, flow, key) = t;
                var tuple = from s in source from f in flow from k in key select (s, f, k);

                return tuple.Match(
                    Observable.Return,
                    Observable.Empty<(SdmxWebSource, DataFlow, string)>
                );
            })
            .Switch()
            .InvokeCommand(cmd);

        return cmd;
    }

    private static Option<string> BuildUri(
        Option<SdmxWebSource> source,
        Option<DataFlow> flow,
        Option<string> key
    ) => from s in source from f in flow from k in key select $"sdmx-dl:/{s.Id}/{f.Ref}/{k}";

    private static Option<string> BuildTitle(
        Option<SdmxWebSource> source,
        Option<DataFlow> flow,
        Option<string> key
    ) => from s in source from f in flow from k in key select BuildTitle(s, f, k);

    private bool HighlightGridImpl(Option<DateTime> highlightOpt)
    {
        var cell = highlightOpt.Match(
            o =>
            {
                return from pCell in LinkedHierarchyGridViewModel.DrawnCells.Find(pc =>
                        pc.ConsumerDefinition.Tag!.Equals(o)
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
                    if (
                        !LinkedHierarchyGridViewModel.DrawnCells.Exists(pc =>
                            pc.ConsumerDefinition.Tag!.Equals(o)
                        )
                    )
                    {
                        return LinkedHierarchyGridViewModel
                            .ColumnsDefinitions.OfType<ConsumerDefinition>()
                            .Find(x => x.Tag!.Equals(o))
                            .Match(p => p.Position, () => -1);
                    }

                    return -1;
                },
                () => -1
            );

            if (hOffset != -1)
                LinkedHierarchyGridViewModel.HorizontalOffset = hOffset;
            else
                return true; /* Trigger the method again with elements that should have been drawn */
        }

        LinkedHierarchyGridViewModel.HoveredCell = cell;
        LinkedHierarchyGridViewModel.HoveredColumn = cell.Match(
            c => c.ConsumerDefinition!.Position,
            () => -1
        );

        Observable
            .Return(false)
            .InvokeCommand(LinkedHierarchyGridViewModel, x => x.DrawGridCommand);

        return false;
    }

    private async Task<Option<DataSet>> RetrieveDataSetImpl(
        SeriesRequest seriesRequest,
        ClientFactory clientFactory,
        ResiliencePipeline pipeline
    )
    {
        var dataSet = await clientFactory.GetClient().GetDataAsync((KeyRequest)seriesRequest);

        return dataSet.ToModel();
    }
}
