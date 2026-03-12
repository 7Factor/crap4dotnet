namespace Crap4DotNet.Core.Models;

/// <summary>
/// Aggregated CRAP data for a single class/type.
/// </summary>
public sealed record TypeCrapData
{
    public required string Name { get; init; }
    public required CrapStatistics Stats { get; init; }
    public required IReadOnlyList<MethodCrapData> Methods { get; init; }
}
