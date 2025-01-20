using System.Diagnostics.CodeAnalysis;

namespace SdmxDl.Browser.Models;

public readonly record struct DataFlow
{
    public required string Ref { get; init; }
    public required string StructureRef { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    [SetsRequiredMembers]
    public DataFlow(Sdmxdl.Format.Protobuf.Dataflow dataFlow)
    {
        Ref = dataFlow.Ref;
        StructureRef = dataFlow.StructureRef;
        Name = dataFlow.Name;
        Description = dataFlow.HasDescription ? dataFlow.Description : string.Empty;
    }
}
