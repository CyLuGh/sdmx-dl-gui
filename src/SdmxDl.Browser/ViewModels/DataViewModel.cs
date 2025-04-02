using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using LanguageExt;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Polly;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public class DataViewModel : BaseViewModel
{
    [Reactive]
    public Option<SdmxWebSource> Source { get; set; }

    [Reactive]
    public Option<DataFlow> Flow { get; set; }

    [Reactive]
    public Option<string> Key { get; set; }

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

    public static string BuildTitle(SdmxWebSource source, DataFlow flow, string key) =>
        $"{source.Id} {flow.Ref} {key}";

    public ReactiveCommand<(SdmxWebSource, DataFlow, string), Option<DataSet>> RetrieveData { get; }
    public ReactiveCommand<DataSet, Seq<ChartSeries>> TransformData { get; }

    public DataViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);
        TransformData = CreateCommandTransformData();

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
            .InvokeCommand(RetrieveData);

        this.WhenAnyValue(x => x.DataSet)
            .Select(d => d.Match(Observable.Return, Observable.Empty<DataSet>))
            .Switch()
            .InvokeCommand(TransformData);

        this.WhenAnyValue(x => x.ChartSeries)
            .Select(x => x.ToLineSeries())
            .ToPropertyEx(this, x => x.LineSeries, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.ChartSeries)
            .Select(series =>
                new[]
                {
                    new DateTimeAxis(TimeSpan.FromDays(30.5), date => date.ToString("yyyy-MM"))
                    {
                        // UnitWidth = TimeSpan.FromDays(30.5).Ticks,
                        // MinStep = TimeSpan.FromDays(30.5).Ticks,
                    },
                }
            )
            .ToPropertyEx(this, x => x.XAxes, scheduler: RxApp.MainThreadScheduler);

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
        });
    }

    private void ManageBusyState(CompositeDisposable disposables)
    {
        RetrieveData
            .IsExecuting.Merge(TransformData.IsExecuting)
            .Throttle(TimeSpan.FromMilliseconds(25))
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

        return cmd;
    }
}
