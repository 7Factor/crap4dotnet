namespace Crap4DotNet.Core.Models;

/// <summary>
/// Uniquely identifies a method by its fully-qualified name.
/// Equality is based solely on <see cref="FullName"/> (the canonical normalized form).
/// </summary>
public sealed record MethodIdentity
{
    public required string Namespace { get; init; }
    public required string ClassName { get; init; }
    public required string MethodName { get; init; }
    public required string Signature { get; init; }
    public required string FullName { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }

    public bool Equals(MethodIdentity? other) =>
        other is not null && string.Equals(FullName, other.FullName, StringComparison.Ordinal);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(FullName);
}
