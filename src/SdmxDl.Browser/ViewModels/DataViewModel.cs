using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using LanguageExt;
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

    public string BusyMessage
    {
        [ObservableAsProperty]
        get;
    }

    public Seq<ChartSeries> ChartSeries
    {
        [ObservableAsProperty]
        get;
    }

    public static string BuildTitle(SdmxWebSource source, DataFlow flow, string key) =>
        $"{source.Id} {flow.Ref} {key}";

    public ReactiveCommand<(SdmxWebSource, DataFlow, string), Option<DataSet>> RetrieveData { get; }

    public DataViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);

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
            .Select(d => d.Data.Map(s => new ChartSeries(s)))
            .ToPropertyEx(this, x => x.ChartSeries, scheduler: RxApp.MainThreadScheduler);

        this.WhenActivated(disposables =>
        {
            ManageBusyState(disposables);
        });
    }

    private void ManageBusyState(CompositeDisposable disposables)
    {
        RetrieveData.IsExecuting.ToPropertyEx(this, x => x.IsBusy).DisposeWith(disposables);

        RetrieveData
            .IsExecuting.Where(x => x)
            .Select(_ => "Retrieving data")
            .ToPropertyEx(this, x => x.BusyMessage, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
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

            var rawSeries = new System.Collections.Generic.List<Sdmxdl.Format.Protobuf.Series>();
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
