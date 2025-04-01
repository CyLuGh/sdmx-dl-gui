using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct Obs
{
    public required string Period { get; init; }
    public required double Value { get; init; }
    public required HashMap<string, string> Meta { get; init; }
}
