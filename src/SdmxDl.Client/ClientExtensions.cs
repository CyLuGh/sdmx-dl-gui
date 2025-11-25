using LanguageExt;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Client;

public static class ClientExtensions
{
    public static async Task<Seq<SdmxWebSource>> GetSources(
        this SdmxWebManager.SdmxWebManagerClient client,
        CancellationToken token
    )
    {
        var rawSources = new List<Sdmxdl.Format.Protobuf.Web.WebSourceDto>();
        using var response = client.GetSources(new EmptyDto(), cancellationToken: token);
        while (await response.ResponseStream.MoveNext(token))
        {
            var source = response.ResponseStream.Current;
            rawSources.Add(source);
        }

        return rawSources.Select(s => new SdmxWebSource(s)).ToSeq();
    }

    public static async Task<Seq<DataFlow>> GetDataFlows(
        this SdmxWebManager.SdmxWebManagerClient client,
        SdmxWebSource source,
        CancellationToken token
    )
    {
        var rawFlows = new List<Sdmxdl.Format.Protobuf.FlowDto>();
        using var response = client.GetFlows(
            new DatabaseRequestDto() { Source = source.Id },
            cancellationToken: token
        );

        while (await response.ResponseStream.MoveNext(token))
        {
            var dataFlow = response.ResponseStream.Current;
            rawFlows.Add(dataFlow);
        }

        return rawFlows.Map(f => new DataFlow(f)).ToSeq().Strict();
    }
}
