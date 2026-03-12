namespace Crap4DotNet.Core.Models;

/// <summary>
/// Aggregated CRAP data for a single namespace.
/// </summary>
public sealed record NamespaceCrapData
{
    public required string Name { get; init; }
    public required CrapStatistics Stats { get; init; }
    public required IReadOnlyList<TypeCrapData> Classes { get; init; }
}
