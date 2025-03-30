using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using LanguageExt;
using Polly;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class DataFlowSelectorViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    : SelectorViewModel<DataFlow, SdmxWebSource>(clientFactory, pipeline)
{
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
        var rawFlows = new List<Sdmxdl.Format.Protobuf.Flow>();
        using var response = clientFactory
            .GetClient()
            .GetFlows(new DatabaseRequest() { Source = input.Id });

        while (await response.ResponseStream.MoveNext(CancelTokenSource.Token))
        {
            var dataFlow = response.ResponseStream.Current;
            rawFlows.Add(dataFlow);
        }

        return rawFlows.Map(f => new DataFlow(f)).ToSeq().Strict();
    }
}
