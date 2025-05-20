using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct DataSet
{
    public required string Ref { get; init; }
    public required Query Query { get; init; }
    public required Seq<Series> Data { get; init; }
}
