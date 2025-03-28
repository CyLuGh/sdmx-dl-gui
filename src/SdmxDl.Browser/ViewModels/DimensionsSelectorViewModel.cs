using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;

namespace SdmxDl.Browser.ViewModels;

public class DimensionsSelectorViewModel : BaseViewModel
{
    public Option<DataStructure> DataStructure
    {
        [ObservableAsProperty]
        get;
    }

    public Seq<PositionedDimensionViewModel> PositionedDimensions
    {
        [ObservableAsProperty]
        get;
    }

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
            .ToPropertyEx(this, x => x.DataStructure, scheduler: RxApp.MainThreadScheduler);

        this.WhenActivated(disposables =>
        {
            UpdatePositionedDimensions(disposables);
        });
    }

    private void UpdatePositionedDimensions(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.DataStructure)
            .Select(o =>
            {
                var dimensions = o.Match(ds => ds.Dimensions, () => Seq<Dimension>.Empty);
                return dimensions
                    .OrderBy(d => d.Position)
                    .Select((d, i) => new PositionedDimensionViewModel(d, i, dimensions.Length))
                    .ToSeq()
                    .Strict();
            })
            .ToPropertyEx(this, x => x.PositionedDimensions, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
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
            new FlowRequest() { Source = source.Id, Flow = dataFlow.Ref }
        );

        return new DataStructure(dataStructure);
    }
}
