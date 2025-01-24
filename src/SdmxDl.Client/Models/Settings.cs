namespace SdmxDl.Client.Models;

public readonly record struct Settings
{
    public static readonly Settings None = new()
    {
        JarPath = string.Empty,
        JavaPath = string.Empty,
        ServerUri = string.Empty,
        IsHosting = false,
    };

    public required string JavaPath { get; init; }
    public required string JarPath { get; init; }
    public required string ServerUri { get; init; }
    public required bool IsHosting { get; init; }
}
