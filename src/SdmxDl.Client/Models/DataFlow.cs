using System.Diagnostics.CodeAnalysis;

namespace SdmxDl.Client.Models;

public readonly record struct DataFlow
{
    public required string Ref { get; init; }
    public required string StructureRef { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    [SetsRequiredMembers]
    public DataFlow(Sdmxdl.Format.Protobuf.Flow flow)
    {
        Ref = flow.Ref;
        StructureRef = flow.StructureRef;
        Name = flow.Name;
        Description = flow.HasDescription ? flow.Description : string.Empty;
    }
}
