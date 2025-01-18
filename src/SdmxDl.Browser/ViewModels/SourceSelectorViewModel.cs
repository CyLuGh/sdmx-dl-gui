using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LanguageExt;
using ReactiveUI;
using SdmxDl.Browser.Models;
using SdmxDl.Client;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class SourceSelectorViewModel
{
    public ReactiveCommand<RxUnit, Seq<SdmxWebSource>> RetrieveSources { get; }

    public SourceSelectorViewModel(ClientFactory clientFactory)
    {
        RetrieveSources = CreateCommandRetrieveSources(clientFactory);
    }

    private ReactiveCommand<RxUnit, Seq<SdmxWebSource>> CreateCommandRetrieveSources(
        ClientFactory clientFactory
    )
    {
        var command = ReactiveCommand.CreateFromTask(async () =>
        {
            var rawSources = new List<Sdmxdl.Format.Protobuf.Web.SdmxWebSource>();
            using var response = clientFactory.GetClient().GetSources(new Empty());
            while (await response.ResponseStream.MoveNext(CancellationToken.None))
            {
                var source = response.ResponseStream.Current;
                rawSources.Add(source);
            }

            return rawSources.Select(s => new SdmxWebSource(s)).ToSeq();
        });

        return command;
    }
}
