using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct DataStructure
{
    public required string Ref { get; init; }
    public Seq<Dimension> Dimensions { get; init; }
    public Seq<Attribute> Attributes { get; init; }
    public Option<string> TimeDimensionId { get; init; }
    public required string PrimaryMeasureId { get; init; }
    public required string Name { get; init; }

    [SetsRequiredMembers]
    public DataStructure(Sdmxdl.Format.Protobuf.StructureDto structure)
    {
        Ref = structure.Ref;
        Dimensions = structure.Dimensions.Select((d, i) => new Dimension(d, i)).ToSeq().Strict();
        Attributes = structure.Attributes.Map(a => new Attribute(a)).ToSeq().Strict();
        if (structure.HasTimeDimensionId)
            TimeDimensionId = structure.TimeDimensionId;
        PrimaryMeasureId = structure.PrimaryMeasureId;
        Name = structure.Name;
    }
}
