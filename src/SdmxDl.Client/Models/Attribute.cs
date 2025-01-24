using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct Attribute
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Option<CodeList> CodeList { get; init; }
    public required AttributeRelationship Relationship { get; init; }

    [SetsRequiredMembers]
    public Attribute(Sdmxdl.Format.Protobuf.Attribute attribute)
    {
        Id = attribute.Id;
        Name = attribute.Name;

        // Check why optional isn't showing
        CodeList = new CodeList(attribute.Codelist);

        Relationship = (AttributeRelationship)attribute.Relationship;
    }
}