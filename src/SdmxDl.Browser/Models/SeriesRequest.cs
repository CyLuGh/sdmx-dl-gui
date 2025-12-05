using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Models;

/// <summary>
/// Represents a request for a series in a data flow, including the data source,
/// the data flow, and a key identifying a specific series within the flow.
/// </summary>
public readonly record struct SeriesRequest(SdmxWebSource Source, DataFlow Flow, KeyIdentifier Key)
{
    public static readonly SeriesRequest Empty =
        new(SdmxWebSource.None, DataFlow.None, KeyIdentifier.Empty);

    public void Deconstruct(out SdmxWebSource source, out DataFlow flow, out KeyIdentifier key)
    {
        source = Source;
        flow = Flow;
        key = Key;
    }

    private KeyRequest ToKeyRequest() =>
        new()
        {
            Source = Source.Id,
            Flow = Flow.Ref,
            Key = (string)Key,
        };

    public static implicit operator KeyRequest(SeriesRequest request) => request.ToKeyRequest();

    public string Title => BuildTitle(Source, Flow, Key);
    public string Uri => $"sdmx-dl:/{Source.Id}/{Flow.Ref}/{Key}";
    public string SourceId => Source.Id;
    public string FlowRef => Flow.Ref;
    public string FullKey => (string)Key;

    public string FetchData => $"sdmx-dl fetch data \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";
    public string FetchMeta => $"sdmx-dl fetch meta \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";
    public string FetchKeys => $"sdmx-dl fetch keys \"{SourceId}\" \"{FlowRef}\" \"{FullKey}\"";

    public static string BuildTitle(SdmxWebSource source, DataFlow flow, KeyIdentifier key) =>
        $"{source.Id} {flow.Ref} {key}";
}
