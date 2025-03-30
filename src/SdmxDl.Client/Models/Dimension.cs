using System.Diagnostics.CodeAnalysis;

namespace SdmxDl.Client.Models;

public readonly record struct Dimension
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required CodeList CodeList { get; init; }
    public required int Position { get; init; }

    [SetsRequiredMembers]
    public Dimension(Sdmxdl.Format.Protobuf.Dimension dimension)
    {
        Id = dimension.Id;
        Name = dimension.Name;
        CodeList = new(dimension.Codelist);
        Position = dimension.Position;
    }
}
