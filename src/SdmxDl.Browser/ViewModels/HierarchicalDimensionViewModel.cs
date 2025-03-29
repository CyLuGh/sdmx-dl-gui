using LanguageExt;

namespace SdmxDl.Browser.ViewModels;

public class HierarchicalDimensionViewModel(
    string key,
    string description,
    Seq<HierarchicalDimensionViewModel> children
)
{
    public string Key { get; } = key;
    public string Description { get; } = description;
    public Seq<HierarchicalDimensionViewModel> Children { get; } = children;
}
