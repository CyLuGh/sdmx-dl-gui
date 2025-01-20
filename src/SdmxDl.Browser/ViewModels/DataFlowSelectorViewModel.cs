using System.Collections.Generic;
using System.Threading;
using LanguageExt;
using ReactiveUI;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public partial class DataFlowSelectorViewModel : BaseViewModel
{
    [Reactive]
    public partial bool IsSearching { get; set; }

    [Reactive]
    public partial string CurrentInput { get; set; }

    [Reactive]
    public partial DataFlow? CurrentSelection { get; set; }

    [Reactive]
    public partial Option<DataFlow> Selection { get; set; }

    [ObservableAsProperty]
    public partial Seq<DataFlow> AllFlows { get; }

    [ObservableAsProperty]
    public partial Seq<DataFlow> CurrentFlows { get; }

    public ReactiveCommand<SdmxWebSource, Seq<DataFlow>> RetrieveDataFlows { get; }

    public DataFlowSelectorViewModel(ClientFactory clientFactory)
    {
        RetrieveDataFlows = CreateCommandRetrieveDataFlows(clientFactory);
    }

    private static ReactiveCommand<SdmxWebSource, Seq<DataFlow>> CreateCommandRetrieveDataFlows(
        ClientFactory clientFactory
    )
    {
        return ReactiveCommand.CreateFromTask(
            async (SdmxWebSource source) =>
            {
                var rawFlows = new List<Sdmxdl.Format.Protobuf.Dataflow>();
                using var response = clientFactory
                    .GetClient()
                    .GetFlows(new SourceRequest() { Source = source.Id });

                while (await response.ResponseStream.MoveNext(CancellationToken.None))
                {
                    var dataFlow = response.ResponseStream.Current;
                    rawFlows.Add(dataFlow);
                }

                return rawFlows.Map(f => new DataFlow(f)).ToSeq().Strict();
                // return Seq<DataFlow>.Empty;
            }
        );
    }
}
