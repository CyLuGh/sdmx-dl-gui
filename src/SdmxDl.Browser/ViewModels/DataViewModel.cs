using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using HierarchyGrid.Definitions;
using LanguageExt;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Polly;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Browser.Infrastructure;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using SukiUI.Locale;

namespace SdmxDl.Browser.ViewModels;

public class DataViewModel : BaseViewModel
{
    [Reactive]
    public Option<SdmxWebSource> Source { get; set; }

    [Reactive]
    public Option<DataFlow> Flow { get; set; }

    [Reactive]
    public Option<string> Key { get; set; }

    [Reactive]
    public bool IsSplitView { get; set; }

    public Option<DataSet> DataSet
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

    public Seq<ChartSeries> ChartSeries
    {
        [ObservableAsProperty]
        get;
    }

    public Seq<LineSeries<DateTimePoint>> LineSeries
    {
        [ObservableAsProperty]
        get;
    }

    public ICartesianAxis[] XAxes
    {
        [ObservableAsProperty]
        get;
    }

    public LiveChartsCore.Measure.Margin Margins { get; } = new(10, 50);

    public bool HasNoData
    {
        [ObservableAsProperty]
        get;
    }

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
    public string SourceId => Source.Match(s => s.Id, () => string.Empty);
    public string FlowRef => Flow.Match(s => s.Ref, () => string.Empty);
    public string FullKey => Key.Match(s => s, () => string.Empty);

    public string FetchData => $"sdmx-dl fetch data \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";
    public string FetchMeta => $"sdmx-dl fetch meta \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";
    public string FetchKeys => $"sdmx-dl fetch keys \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";

    public string Uri
    {
        [ObservableAsProperty]
        get;
    }

    private ReactiveCommand<
        (SdmxWebSource, DataFlow, string),
        Option<DataSet>
    > RetrieveData { get; }
    private ReactiveCommand<DataSet, Seq<ChartSeries>> TransformData { get; }
    private ReactiveCommand<Seq<ChartSeries>, RxUnit> BuildStandAloneGrid { get; }
    private ReactiveCommand<Seq<ChartSeries>, RxUnit> BuildLinkedGrid { get; }
    public ReactiveCommand<Seq<ChartSeries>, ICartesianAxis[]> SetAxes { get; }

    public ReactiveCommand<Seq<ChartSeries>, Seq<LineSeries<DateTimePoint>>> SetLinesSeries { get; }
    public ReactiveCommand<string, RxUnit> CopyToClipboard { get; }
    public Interaction<string, RxUnit> CopyToClipboardInteraction { get; } =
        new(RxApp.MainThreadScheduler);

    public DataViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);
        TransformData = CreateCommandTransformData();
        BuildStandAloneGrid = CreateCommandBuildStandAloneGrid();
        BuildLinkedGrid = CreateCommandLinkedAloneGrid();
        CopyToClipboard = ReactiveCommand.CreateFromObservable(
            (string s) => CopyToClipboardInteraction.Handle(s)
        );

        SetLinesSeries = CreateCommandSetLinesSeries();
        SetAxes = CreateCommandSetAxes();

        this.WhenActivated(disposables =>
        {
            ManageBusyState(disposables);

            this.WhenAnyValue(x => x.DataSet, x => x.ChartSeries)
                .Select(t =>
                {
                    var (ods, css) = t;
                    return ods.IsSome && css.IsEmpty;
                })
                .ToPropertyEx(this, x => x.HasNoData, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Source, x => x.Flow, x => x.Key)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(t =>
                {
                    var (source, flow, key) = t;
                    return BuildUri(source, flow, key).Match(s => s, () => string.Empty);
                })
                .ToPropertyEx(this, x => x.Uri, scheduler: RxApp.MainThreadScheduler)
                .DisposeWith(disposables);

            // this.WhenAnyValue(x => x.Source, x => x.Flow, x => x.Key)
            //     .Throttle(TimeSpan.FromMilliseconds(100))
            //     .Select(t =>
            //     {
            //         var (source, flow, key) = t;
            //         return BuildTitle(source, flow, key);
            //     })
            //     .ToPropertyEx(this, x => x.Title, scheduler: RxApp.MainThreadScheduler)
            //     .DisposeWith(disposables);
        });
    }

    private ReactiveCommand<
        Seq<ChartSeries>,
        Seq<LineSeries<DateTimePoint>>
    > CreateCommandSetLinesSeries()
    {
        var cmd = ReactiveCommand.CreateRunInBackground<
            Seq<ChartSeries>,
            Seq<LineSeries<DateTimePoint>>
        >(x => x.ToLineSeries());

        cmd.ToPropertyEx(this, x => x.LineSeries, scheduler: RxApp.MainThreadScheduler);
        this.WhenAnyValue(x => x.ChartSeries).InvokeCommand(cmd);
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

        cmd.ToPropertyEx(this, x => x.XAxes, scheduler: RxApp.MainThreadScheduler);
        this.WhenAnyValue(x => x.ChartSeries).Where(seq => !seq.IsEmpty).InvokeCommand(cmd);

        return cmd;
    }

    private ReactiveCommand<Seq<ChartSeries>, RxUnit> CreateCommandBuildStandAloneGrid()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            (Seq<ChartSeries> series) => BuildStandAloneGridImpl(series)
        );

        this.WhenAnyValue(x => x.ChartSeries).Where(seq => !seq.IsEmpty).InvokeCommand(cmd);

        return cmd;
    }

    private ReactiveCommand<Seq<ChartSeries>, RxUnit> CreateCommandLinkedAloneGrid()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            (Seq<ChartSeries> series) => BuildLinkedGridImpl(series)
        );

        this.WhenAnyValue(x => x.ChartSeries).Where(seq => !seq.IsEmpty).InvokeCommand(cmd);

        return cmd;
    }

    private void BuildStandAloneGridImpl(Seq<ChartSeries> series)
    {
        var formatter = series.GetHighestFreq().GetFormatter();
        var data = series.Map(s => (s.Key, s.Values)).ToHashMap();

        var producerDefinitions = series
            .GetDates()
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
                    Option<double> d => d.Match(
                        x => Qualification.Normal,
                        () => Qualification.Empty
                    ),
                    _ => Qualification.Empty,
                },
        });

        StandAloneHierarchyGridViewModel.Set(
            new HierarchyDefinitions(producerDefinitions, consumerDefinitions)
        );
    }

    private void BuildLinkedGridImpl(Seq<ChartSeries> series)
    {
        var formatter = series.GetHighestFreq().GetFormatter();
        var data = series.Map(s => (s.Key, s.Values)).ToHashMap();

        var producerDefinitions = series
            .Map(s => new ProducerDefinition() { Content = s.Title, Producer = () => s })
            .ToSeq();

        var consumerDefinitions = series
            .GetDates()
            .OrderBy(x => x)
            .Map(d => new ConsumerDefinition()
            {
                Content = formatter(d),
                Consumer = o =>
                    o switch
                    {
                        ChartSeries s => from x in data.Find(s.Key, d)
                        from v in x
                        select Tuple.Create(v, s.Format),
                        _ => Option<Tuple<double, string>>.None,
                    },
                Formatter = o =>
                    o switch
                    {
                        Option<Tuple<double, string>> t => t.Match(
                            x => x.Item1.ToString(x.Item2),
                            () => string.Empty
                        ),
                        _ => string.Empty,
                    },
                Qualify = o =>
                    o switch
                    {
                        Option<Tuple<double, string>> d => d.Match(
                            x => Qualification.Normal,
                            () => Qualification.Empty
                        ),
                        _ => Qualification.Empty,
                    },
            });

        LinkedHierarchyGridViewModel.Set(
            new HierarchyDefinitions(producerDefinitions, consumerDefinitions)
        );
    }

    private void ManageBusyState(CompositeDisposable disposables)
    {
        RetrieveData
            .IsExecuting.CombineLatest(TransformData.IsExecuting)
            .Throttle(TimeSpan.FromMilliseconds(25))
            .Select(t => t.First || t.Second)
            .ToPropertyEx(this, x => x.IsBusy, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);

        RetrieveData
            .IsExecuting.Where(x => x)
            .Select(_ => "Retrieving data")
            .Merge(TransformData.IsExecuting.Where(x => x).Select(_ => "Parsing results"))
            .ToPropertyEx(this, x => x.BusyMessage, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    private ReactiveCommand<DataSet, Seq<ChartSeries>> CreateCommandTransformData()
    {
        var cmd = ReactiveCommand.CreateRunInBackground(
            (DataSet ds) => ds.Data.Map(s => new ChartSeries(s))
        );

        cmd.ToPropertyEx(this, x => x.ChartSeries, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.DataSet)
            .Select(d => d.Match(Observable.Return, Observable.Empty<DataSet>))
            .Switch()
            .InvokeCommand(cmd);

        return cmd;
    }

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

        cmd.ToPropertyEx(this, x => x.DataSet, scheduler: RxApp.MainThreadScheduler);

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
}
