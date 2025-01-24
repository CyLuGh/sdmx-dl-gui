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
    public DataStructure(Sdmxdl.Format.Protobuf.DataStructure dataStructure)
    {
        Ref = dataStructure.Ref;
        Dimensions = dataStructure.Dimensions.Map(d => new Dimension(d)).ToSeq().Strict();
        Attributes = dataStructure.Attributes.Map(a => new Attribute(a)).ToSeq().Strict();
        if (dataStructure.HasTimeDimensionId)
            TimeDimensionId = dataStructure.TimeDimensionId;
        PrimaryMeasureId = dataStructure.PrimaryMeasureId;
        Name = dataStructure.Name;
    }
}
