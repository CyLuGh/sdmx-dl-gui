using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct Series
{
    public required string Key { get; init; }
    public required HashMap<string, string> Meta { get; init; }
    public required Seq<Obs> Obs { get; init; }
}
