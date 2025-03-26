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
    public SdmxWebSource(Sdmxdl.Format.Protobuf.Web.WebSource webSource)
    {
        Id = webSource.Id;
        Driver = webSource.Driver;
        Endpoint = webSource.Endpoint;
        Aliases = webSource.Aliases.ToSeq();

        Names = webSource.Names.Select(x => (x.Key, x.Value)).ToHashMap();
        Properties = webSource.Properties.Select(x => (x.Key, x.Value)).ToHashMap();

        if (webSource.HasWebsite)
            Website = webSource.Website;
        if (webSource.HasMonitor)
            Monitor = webSource.Monitor;
        if (webSource.HasMonitorWebsite)
            MonitorWebsite = webSource.MonitorWebsite;

        Confidentiality = (Confidentiality)webSource.Confidentiality;
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
