using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;
using static System.Int32;

namespace SdmxDl.Browser.ViewModels;

public partial class DimensionsSelectorViewModel : BaseViewModel
{
    [Reactive]
    public partial HierarchicalDimensionViewModel? SelectedDimension { get; set; }

    [ObservableAsProperty(ReadOnly = false)]
    private Option<DataStructure> _dataStructure;

    [ObservableAsProperty(ReadOnly = false)]
    private Seq<PositionedDimensionViewModel> _positionedDimensions;

    [ObservableAsProperty(ReadOnly = false)]
    private Seq<HierarchicalDimensionViewModel> _hierarchicalDimensions;

    public ReactiveCommand<
        (SdmxWebSource, DataFlow),
        Option<DataStructure>
    > RetrieveDimensions { get; }
    public ReactiveCommand<RxUnit, Option<DataStructure>> Clear { get; }

    public DimensionsSelectorViewModel(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        ClientFactory clientFactory
    )
    {
        Clear = ReactiveCommand.Create(() => Option<DataStructure>.None);

        RetrieveDimensions = CreateCommandRetrieveDimensions(
            sourceSelectorViewModel,
            dataFlowSelectorViewModel,
            clientFactory,
            Clear
        );

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
        _positionedDimensionsHelper = this.WhenAnyValue(x => x.DataStructure)
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
            .ToProperty(this, x => x.PositionedDimensions, scheduler: RxApp.MainThreadScheduler)
            .DisposeWith(disposables);
    }

    private void UpdateHierarchy(CompositeDisposable disposables)
    {
        _hierarchicalDimensionsHelper = this.WhenAnyValue(x => x.PositionedDimensions)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .Select(seq =>
                seq.Length == 0
                    ? Seq<HierarchicalDimensionViewModel>.Empty
                    : HierarchicalDimensionViewModel.BuildHierarchy(seq, HashMap<int, string>.Empty)
            )
            .ToProperty(this, x => x.HierarchicalDimensions, scheduler: RxApp.MainThreadScheduler)
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
    > CreateCommandRetrieveDimensions(
        SourceSelectorViewModel sourceSelectorViewModel,
        DataFlowSelectorViewModel dataFlowSelectorViewModel,
        ClientFactory clientFactory,
        ReactiveCommand<RxUnit, Option<DataStructure>> clear
    )
    {
        var cmd = ReactiveCommand.CreateFromTask(
            async ((SdmxWebSource, DataFlow) t) =>
            {
                var (source, flow) = t;
                var ds = await DoRetrieveDimensions(clientFactory, source, flow)
                    .ConfigureAwait(false);
                return Option<DataStructure>.Some(ds);
            }
        );

        sourceSelectorViewModel
            .WhenAnyValue(x => x.Selection)
            .Do(_ => Observable.Return(RxUnit.Default).InvokeCommand(clear))
            .CombineLatest(
                dataFlowSelectorViewModel
                    .WhenAnyValue(x => x.Selection)
                    .Do(_ => Observable.Return(RxUnit.Default).InvokeCommand(clear))
            )
            .Throttle(TimeSpan.FromMilliseconds(200))
            .Select(t =>
            {
                var (source, flow) = t;
                var o = from s in source from f in flow select (s, f);
                return o.Some(Observable.Return).None(Observable.Empty<(SdmxWebSource, DataFlow)>);
            })
            .Switch()
            .InvokeCommand(cmd);

        _dataStructureHelper = cmd.Merge(clear)
            .ToProperty(this, x => x.DataStructure, scheduler: RxApp.MainThreadScheduler);

        return cmd;
    }

    private static async Task<DataStructure> DoRetrieveDimensions(
        ClientFactory clientFactory,
        SdmxWebSource source,
        DataFlow dataFlow
    )
    {
        var client = clientFactory.GetClient();
        var metaSet = await client.GetMetaAsync(
            new FlowRequestDto() { Source = source.Id, Flow = dataFlow.Ref }
        );
        return new DataStructure(metaSet.Structure);
    }

    //private static async Task<Seq<string>> DoFetchAvailableValues(
    //    ClientFactory clientFactory,
    //    SdmxWebSource source,
    //    DataFlow dataFlow,
    //    Dimension dimension
    //)
    //{
    //    var client = clientFactory.GetClient();
    //    var request = client.GetAvailability(
    //        new KeyDimensionRequestDto() { Source = source.Id, Flow = dataFlow.Ref }
    //    );
    //}
}
