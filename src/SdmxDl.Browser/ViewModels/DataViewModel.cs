using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using LanguageExt;
using Polly;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
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

    public static string BuildTitle(SdmxWebSource source, DataFlow flow, string key) =>
        $"{source.Id} {flow.Ref} {key}";

    public ReactiveCommand<(SdmxWebSource, DataFlow, string), Option<DataSet>> RetrieveData { get; }

    public DataViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    {
        RetrieveData = CreateCommandRetrieveData(clientFactory, pipeline);

        this.WhenActivated(disposables =>
        {
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
                .InvokeCommand(RetrieveData)
                .DisposeWith(disposables);
        });
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
            using var response = clientFactory
                .GetClient()
                .GetDataStream(
                    new()
                    {
                        Source = source.Id,
                        Flow = flow.Ref,
                        Key = key,
                    }
                );

            while (await response.ResponseStream.MoveNext(CancellationToken.None))
            {
                var series = response.ResponseStream.Current;
                rawSeries.Add(series);
            }

            return Option<DataSet>.None;
        });

        return cmd;
    }
}
