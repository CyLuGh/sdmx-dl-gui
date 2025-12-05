namespace SdmxDl.Browser.Models;

/// <summary>
/// Represents an immutable key identifier.
/// </summary>
public readonly record struct KeyIdentifier(string Identifier)
{
    public static readonly KeyIdentifier Empty = new(string.Empty);

    public static implicit operator KeyIdentifier(string identifier) => new(identifier);

    public static explicit operator string(KeyIdentifier identifier) => identifier.Identifier;

    public override string ToString() => Identifier;
}
