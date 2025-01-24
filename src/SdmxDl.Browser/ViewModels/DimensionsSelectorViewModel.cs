using System.Threading.Tasks;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class DimensionsSelectorViewModel : BaseViewModel
{
    public DimensionsSelectorViewModel(ClientFactory clientFactory) { }

    private async Task FetchDimensions(
        ClientFactory clientFactory,
        SdmxWebSource source,
        DataFlow dataFlow
    )
    {
        var client = clientFactory.GetClient();

        var dataStructure = await client.GetStructureAsync(
            new FlowRequest() { Source = source.Id, Flow = dataFlow.Name }
        );
    }
}
