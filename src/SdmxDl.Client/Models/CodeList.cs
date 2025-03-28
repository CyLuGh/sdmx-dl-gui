using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct CodeList
{
    public required string Ref { get; init; }
    public HashMap<string, string> Codes { get; init; }

    [SetsRequiredMembers]
    public CodeList(Sdmxdl.Format.Protobuf.Codelist codeList)
    {
        Ref = codeList.Ref;
        Codes = codeList.Codes.Select(x => (x.Key, x.Value)).ToHashMap();
    }
}