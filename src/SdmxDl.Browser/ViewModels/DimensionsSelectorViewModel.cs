using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using LanguageExt;
using ReactiveUI;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public partial class DimensionsSelectorViewModel : BaseViewModel
{
    [ObservableAsProperty]
    public partial Option<DataStructure> DataStructure { get; }

    public ReactiveCommand<
        (SdmxWebSource, DataFlow),
        Option<DataStructure>
    > RetrieveDimensions { get; }
    public ReactiveCommand<RxUnit, Option<DataStructure>> Clear { get; }

    public DimensionsSelectorViewModel(ClientFactory clientFactory)
    {
        RetrieveDimensions = CreateCommandRetrieveDimensions(clientFactory);
        Clear = ReactiveCommand.Create(() => Option<DataStructure>.None);

        RetrieveDimensions
            .Merge(Clear)
            .ToProperty(
                this,
                x => x.DataStructure,
                out _dataStructureHelper,
                scheduler: RxApp.MainThreadScheduler
            );
    }

    private ReactiveCommand<
        (SdmxWebSource, DataFlow),
        Option<DataStructure>
    > CreateCommandRetrieveDimensions(ClientFactory clientFactory)
    {
        var command = ReactiveCommand.CreateFromTask(
            async ((SdmxWebSource, DataFlow) t) =>
            {
                var (source, flow) = t;
                var ds = await FetchDimensions(clientFactory, source, flow).ConfigureAwait(false);
                return Option<DataStructure>.Some(ds);
            }
        );

        return command;
    }

    private static async Task<DataStructure> FetchDimensions(
        ClientFactory clientFactory,
        SdmxWebSource source,
        DataFlow dataFlow
    )
    {
        var client = clientFactory.GetClient();

        var dataStructure = await client.GetStructureAsync(
            new FlowRequest() { Source = source.Id, Flow = dataFlow.Name }
        );

        return new DataStructure(dataStructure);
    }
}
