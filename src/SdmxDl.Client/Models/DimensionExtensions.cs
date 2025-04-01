using LanguageExt;

namespace SdmxDl.Client.Models;

public static class DimensionExtensions
{
    public static bool CheckComponents(this Seq<Dimension> dimensions, string key)
    {
        var split = key.Split('.');
        return split.Any(s => !string.IsNullOrWhiteSpace(s))
               && split
                   .Select(
                       (s, idx) =>
                           string.IsNullOrEmpty(s) || dimensions[idx].CodeList.Codes.ContainsKey(s)
                   )
                   .All(x => x);
    }
}