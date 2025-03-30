using System;
using System.Diagnostics.Contracts;
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
using static System.Int32;

namespace SdmxDl.Browser.ViewModels;

public class DimensionsSelectorViewModel : BaseViewModel
{
    [Reactive]
    public HierarchicalDimensionViewModel? SelectedDimension { get; set; }

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

    public Seq<HierarchicalDimensionViewModel> HierarchicalDimensions
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
            UpdateHierarchy(disposables);
            ClearSelectionOnSort(disposables);
        });
    }

    private void ClearSelectionOnSort(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.PositionedDimensions)
            .Subscribe(_ => SelectedDimension = null)
            .DisposeWith(disposables);
    }

    private void UpdatePositionedDimensions(CompositeDisposable disposables)
    {
        // Notify when position change is required
        var positionChanged = this.WhenAnyValue(x => x.PositionedDimensions)
            .Select(seq => seq.Map(d => d.WhenAnyValue(x => x.DesiredPosition)).Merge())
            .Switch()
            .Select(x => x.IsSome);

        // Generate sorted list when receiving data or when ordering again
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
            .Merge(positionChanged.Select(_ => SortDimensions(PositionedDimensions)))
            .ToPropertyEx(this, x => x.PositionedDimensions, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    private void UpdateHierarchy(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.PositionedDimensions)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .Select(seq =>
                seq.Length == 0
                    ? Seq<HierarchicalDimensionViewModel>.Empty
                    : HierarchicalDimensionViewModel.BuildHierarchy(seq, HashMap<int, string>.Empty)
            )
            .ToPropertyEx(this, x => x.HierarchicalDimensions, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    [Pure]
    private static Seq<PositionedDimensionViewModel> SortDimensions(
        Seq<PositionedDimensionViewModel> dimensions
    )
    {
        return dimensions
            .Find(d => d.DesiredPosition.IsSome)
            .Some(d =>
            {
                var position = d.DesiredPosition.Match(p => p, () => MaxValue);
                var others = dimensions.Where(x => !x.Dimension.Equals(d.Dimension));

                return others
                    .Where(x => x.CurrentPosition < position)
                    .Add(new PositionedDimensionViewModel(d.Dimension, position, dimensions.Length))
                    .Concat(
                        others
                            .Where(x => x.CurrentPosition >= position)
                            .Select(x => new PositionedDimensionViewModel(
                                x.Dimension,
                                x.CurrentPosition == position
                                    ? (x.CurrentPosition + (1 * -d.ShiftSign))
                                    : x.CurrentPosition,
                                dimensions.Length
                            ))
                    )
                    .OrderBy(x => x.CurrentPosition)
                    .ToSeq()
                    .Strict();
            })
            .None(() => dimensions);
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
