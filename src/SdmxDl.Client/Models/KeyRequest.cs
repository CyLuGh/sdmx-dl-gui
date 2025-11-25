using LanguageExt;

namespace SdmxDl.Client.Models;

public readonly record struct KeyRequest
{
    public required string Source { get; init; }
    public required string Flow { get; init; }
    public required string Key { get; init; }

    public Option<string> Database { get; init; }
    public Option<string> Languages { get; init; }

    public static implicit operator Sdmxdl.Grpc.KeyRequestDto(KeyRequest keyRequest) =>
        new()
        {
            Source = keyRequest.Source,
            Flow = keyRequest.Flow,
            Key = keyRequest.Key,
        };
}
