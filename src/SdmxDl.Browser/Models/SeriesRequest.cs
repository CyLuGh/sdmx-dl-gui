using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Models;

/// <summary>
/// Represents a request for a series in a data flow, including the data source,
/// the data flow, and a key identifying a specific series within the flow.
/// </summary>
public readonly record struct SeriesRequest(SdmxWebSource Source, DataFlow Flow, KeyIdentifier Key)
{
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
}
