using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Linq;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace SdmxDl.Browser.ViewModels;

public class HierarchicalDimensionViewModel : BaseViewModel
{
    private readonly int _level;
    private readonly Seq<PositionedDimensionViewModel> _positionedDimensions;

    [Reactive]
    public Seq<HierarchicalDimensionViewModel> Children { get; private set; }

    [Reactive]
    public bool IsExpanded { get; set; }

    public static HierarchicalDimensionViewModel None { get; } =
        new HierarchicalDimensionViewModel();

    private HierarchicalDimensionViewModel()
    {
        this.WhenAnyValue(x => x.IsExpanded)
            .Where(x => x)
            .Subscribe(_ =>
            {
                Children =
                    _level < _positionedDimensions.Length - 1
                        ? BuildHierarchy(_positionedDimensions, Keys, _level + 1)
                        : Seq<HierarchicalDimensionViewModel>.Empty;
            });

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

    public string Description { get; }

    public HashMap<int, string> Keys { get; }

    [Pure]
    internal static Seq<HierarchicalDimensionViewModel> BuildHierarchy(
        Seq<PositionedDimensionViewModel> positionedDimensions,
        HashMap<int, string> keys,
        int level = 0
    )
    {
        return positionedDimensions[level]
            .Dimension.CodeList.Codes.Select(t => new HierarchicalDimensionViewModel(
                level,
                t.Key,
                t.Value,
                positionedDimensions,
                keys
            ))
            .OrderBy(x => x.Description)
            .ToSeq()
            .Strict();

        // var children =
        //     level < positionedDimensions.Length - 1
        //         ? BuildHierarchy(positionedDimensions, level + 1)
        //         : Seq<HierarchicalDimensionViewModel>.Empty;
        //
        // return positionedDimensions[level]
        //     .Dimension.CodeList.Codes.Select(t => new HierarchicalDimensionViewModel(
        //         level,
        //         t.Key,
        //         t.Value,
        //         children.Map(c =>
        //         {
        //             c.AddKey(level, t.Key);
        //             return c;
        //         })
        //     ))
        //     .OrderBy(x => x.Description)
        //     .ToSeq()
        //     .Strict();
    }
}
