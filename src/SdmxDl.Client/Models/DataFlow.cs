using System.Diagnostics.CodeAnalysis;

namespace SdmxDl.Client.Models;

/// <summary>
/// Represents a data flow in the SDMX system, encapsulating details such as the flow reference,
/// structure reference, name, and description.
/// </summary>
public readonly record struct DataFlow
{
    public static readonly DataFlow None =
        new()
        {
            Ref = "None",
            StructureRef = "None",
            Name = "None",
            Description = "None"
        };

    public required string Ref { get; init; }
    public required string StructureRef { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    [SetsRequiredMembers]
    public DataFlow(Sdmxdl.Format.Protobuf.FlowDto flow)
    {
        Ref = flow.Ref;
        StructureRef = flow.StructureRef;
        Name = flow.Name;
        Description = flow.HasDescription ? flow.Description : string.Empty;
    }
}
