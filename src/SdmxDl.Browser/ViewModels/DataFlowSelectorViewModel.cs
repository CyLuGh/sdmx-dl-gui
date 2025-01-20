using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using ReactiveUI;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public partial class DataFlowSelectorViewModel : SelectorViewModel<DataFlow, SdmxWebSource>
{
    public DataFlowSelectorViewModel(ClientFactory clientFactory)
        : base(clientFactory) { }

    [Pure]
    protected override Seq<DataFlow> Filter(Seq<DataFlow> all, string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return all;

        return all.Where(s =>
                s.Name.Contains(input, StringComparison.CurrentCultureIgnoreCase)
                || s.Description.Contains(input, StringComparison.CurrentCultureIgnoreCase)
            )
            .OrderBy(s => s.Name)
            .ToSeq()
            .Strict();
    }

    [Pure]
    protected override async Task<Seq<DataFlow>> RetrieveDataImpl(
        SdmxWebSource input,
        ClientFactory clientFactory
    )
    {
        var rawFlows = new List<Sdmxdl.Format.Protobuf.Dataflow>();
        using var response = clientFactory
            .GetClient()
            .GetFlows(new SourceRequest() { Source = input.Id });

        while (await response.ResponseStream.MoveNext(CancellationToken.None))
        {
            var dataFlow = response.ResponseStream.Current;
            rawFlows.Add(dataFlow);
        }

        return rawFlows.Map(f => new DataFlow(f)).ToSeq().Strict();
    }
}
