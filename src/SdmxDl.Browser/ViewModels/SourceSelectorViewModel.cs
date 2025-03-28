using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using DynamicData;
using LanguageExt;
using Polly;
using ReactiveUI;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class SourceSelectorViewModel(ClientFactory clientFactory, ResiliencePipeline pipeline)
    : SelectorViewModel<SdmxWebSource, RxUnit>(clientFactory, pipeline)
{
    [Pure]
    protected override async Task<Seq<SdmxWebSource>> RetrieveDataImpl(
        RxUnit input,
        ClientFactory clientFactory
    )
    {
        var rawSources = new List<Sdmxdl.Format.Protobuf.Web.WebSource>();
        using var response = clientFactory.GetClient().GetSources(new Empty());
        while (await response.ResponseStream.MoveNext(CancelTokenSource.Token))
        {
            var source = response.ResponseStream.Current;
            rawSources.Add(source);
        }

        return rawSources.Select(s => new SdmxWebSource(s)).ToSeq();
    }

    [Pure]
    protected override Seq<SdmxWebSource> Filter(Seq<SdmxWebSource> all, string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return all.OrderBy(s => s.Id).ToSeq().Strict();

        return all.Where(s =>
                s.Id.Contains(input, StringComparison.CurrentCultureIgnoreCase)
                || s.Aliases.Any(a => a.Contains(input, StringComparison.CurrentCultureIgnoreCase))
                // TODO: check culture?
                || s.GetDescription().Contains(input, StringComparison.CurrentCultureIgnoreCase)
            )
            .OrderBy(s => s.Id)
            .ToSeq()
            .Strict();
    }
}
