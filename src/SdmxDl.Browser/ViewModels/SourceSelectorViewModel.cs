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
using ReactiveUI;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class SourceSelectorViewModel(ClientFactory clientFactory)
    : SelectorViewModel<SdmxWebSource, RxUnit>(clientFactory)
{
    [Pure]
    protected override async Task<Seq<SdmxWebSource>> RetrieveDataImpl(
        RxUnit input,
        ClientFactory clientFactory
    )
    {
        return Seq.create(
            new SdmxWebSource()
            {
                Driver = "test",
                Endpoint = "",
                Id = "AAA",
                Confidentiality = Confidentiality.Public,
            },
            new SdmxWebSource()
            {
                Driver = "test",
                Endpoint = "",
                Id = "CCC",
                Confidentiality = Confidentiality.Public,
            },
            new SdmxWebSource()
            {
                Driver = "test",
                Endpoint = "",
                Id = "BBB",
                Confidentiality = Confidentiality.Public,
            }
        );

        var rawSources = new List<Sdmxdl.Format.Protobuf.Web.SdmxWebSource>();
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
            )
            .OrderBy(s => s.Id)
            .ToSeq()
            .Strict();
    }
}
