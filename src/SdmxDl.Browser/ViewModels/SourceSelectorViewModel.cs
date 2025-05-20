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
    protected override Task<Seq<SdmxWebSource>> RetrieveDataImpl(
        RxUnit input,
        ClientFactory clientFactory
    ) => clientFactory.GetClient().GetSources(CancelTokenSource.Token);

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
