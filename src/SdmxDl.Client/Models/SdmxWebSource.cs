using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct SdmxWebSource
{
    public required string Id { get; init; }
    public HashMap<string, string> Names { get; init; }
    public required string Driver { get; init; }
    public required string Endpoint { get; init; }
    public HashMap<string, string> Properties { get; init; }
    public Seq<string> Aliases { get; init; }
    public Option<string> Website { get; init; }
    public Option<string> Monitor { get; init; }
    public Option<string> MonitorWebsite { get; init; }
    public Confidentiality Confidentiality { get; init; }

    public SdmxWebSource() { }

    [SetsRequiredMembers]
    public SdmxWebSource(Sdmxdl.Format.Protobuf.Web.SdmxWebSource source)
    {
        Id = source.Id;
        Driver = source.Driver;
        Endpoint = source.Endpoint;
        Aliases = source.Aliases.ToSeq();

        Names = source.Names.Select(x => (x.Key, x.Value)).ToHashMap();
        Properties = source.Properties.Select(x => (x.Key, x.Value)).ToHashMap();

        if (source.HasWebsite)
            Website = source.Website;
        if (source.HasMonitor)
            Monitor = source.Monitor;
        if (source.HasMonitorWebsite)
            MonitorWebsite = source.MonitorWebsite;

        Confidentiality = (Confidentiality)source.Confidentiality;
    }
}

public enum Confidentiality
{
    Public = 0,
    Unrestricted = 1,
    Restricted = 2,
    Confidential = 3,
    Secret = 4,
}
