using System;
using System.Data;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SdmxDl.Client;
using SdmxDl.Client.Models;
using Sdmxdl.Grpc;
using Splat;

namespace SdmxDl.Browser.ViewModels;

public partial class HierarchicalDimensionViewModel : BaseViewModel
{
    private readonly int _level;
    private readonly Seq<PositionedDimensionViewModel> _positionedDimensions;
    private bool _initialized;

    [Reactive]
    public partial Seq<HierarchicalDimensionViewModel> Children { get; set; }

    [Reactive]
    public partial bool IsExpanded { get; set; }

    public ReactiveCommand<
        RxUnit,
        Seq<HierarchicalDimensionViewModel>
    > BuildChildrenHierarchy { get; }

    public static HierarchicalDimensionViewModel None { get; } =
        new HierarchicalDimensionViewModel() { Description = "None" };

    public static HierarchicalDimensionViewModel Loading { get; } =
        new HierarchicalDimensionViewModel() { Description = "Loading..." };

    private HierarchicalDimensionViewModel()
    {
        BuildChildrenHierarchy = CreateCommandBuildChildrenHierarchy();
        Description = string.Empty;
        Children = Seq.create(HierarchicalDimensionViewModel.None);
    }

    private HierarchicalDimensionViewModel(
        int level,
        string key,
        string description,
        Seq<PositionedDimensionViewModel> positionedDimensions,
        HashMap<int, string> keys
    )
        : this()
    {
        _level = level;
        _positionedDimensions = positionedDimensions;
        Description = description;
        Keys = keys.AddOrUpdate(_positionedDimensions[_level].Dimension.Position, key);
    }

    public string Description { get; private init; }

    public HashMap<int, string> Keys { get; }

    private ReactiveCommand<
        RxUnit,
        Seq<HierarchicalDimensionViewModel>
    > CreateCommandBuildChildrenHierarchy()
    {
        var cmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var availableCodes = await DoFetchAvailableValues();

            return _level < _positionedDimensions.Length - 1
                ? BuildHierarchy(_positionedDimensions, availableCodes, Keys, _level + 1)
                : Seq<HierarchicalDimensionViewModel>.Empty;
        });

        this.WhenAnyValue(x => x.IsExpanded)
            .Where(x => x && !_initialized)
            .Select(_ => RxUnit.Default)
            .InvokeCommand(cmd);

        cmd.IsExecuting.Where(x => x)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => Children = Seq.create(Loading));

        cmd.Do(_ => _initialized = true)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(seq => Children = seq);

        return cmd;
    }

    private Task<HashSet<string>> DoFetchAvailableValues()
    {
        if (_level >= _positionedDimensions.Count - 1)
            return Task.FromResult(HashSet<string>.Empty);

        var client = Locator.Current.GetService<ClientFactory>()!.GetClient();
        var source = Locator.Current.GetService<SourceSelectorViewModel>()!.Selection;
        var flow = Locator.Current.GetService<DataFlowSelectorViewModel>()!.Selection;
        var key = DimensionsSelectorViewModel.DoBuildKey(_positionedDimensions, this);
        var dimensionPosition = _positionedDimensions[_level + 1].Dimension.Position;

        var selection = from s in source from f in flow select (s, f);

        return selection.MatchAsync(
            async t =>
            {
                var (s, f) = t;
                var request = client.GetAvailability(
                    new KeyDimensionRequestDto()
                    {
                        Source = s.Id,
                        Flow = f.Ref,
                        Key = key,
                        Dimension = dimensionPosition
                    }
                );

                Seq<Seq<string>> codes = Seq<Seq<string>>.Empty;
                while (await request.ResponseStream.MoveNext())
                {
                    var dto = request.ResponseStream.Current;
                    codes = codes.Add(dto.Codes.ToSeq());
                }

                return HashSet.createRange(codes.Flatten());
            },
            () => HashSet<string>.Empty
        );
    }

    [Pure]
    internal static Seq<HierarchicalDimensionViewModel> BuildHierarchy(
        Seq<PositionedDimensionViewModel> positionedDimensions,
        HashSet<string> availableCodes,
        HashMap<int, string> keys,
        int level = 0
    )
    {
        return positionedDimensions[level]
            .Dimension.CodeList.Codes.Where(t =>
                availableCodes.IsEmpty || availableCodes.Contains(t.Key)
            )
            .Select(t => new HierarchicalDimensionViewModel(
                level,
                t.Key,
                t.Value,
                positionedDimensions,
                keys
            ))
            .OrderBy(x => x.Description)
            .ToSeq()
            .Strict();
    }
}
