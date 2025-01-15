namespace SdmxDl.Engine;

public readonly record struct Settings
{
    public required string JavaPath { get; init; }

    public required string JarPath { get; init; }

    public required string ServerUri { get; init; }
}
