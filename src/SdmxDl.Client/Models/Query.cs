namespace SdmxDl.Client.Models;

public readonly record struct Query
{
    public required string Key { get; init; }
    public required Detail Detail { get; init; }
}
