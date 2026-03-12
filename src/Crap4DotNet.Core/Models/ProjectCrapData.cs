namespace Crap4DotNet.Core.Models;

/// <summary>
/// Complete CRAP analysis result for a project. This is the top-level data model
/// that the JSON report writer serializes.
/// </summary>
public sealed record ProjectCrapData
{
    public required string Project { get; init; }
    public required int Threshold { get; init; }
    public required CrapStatistics Stats { get; init; }
    public required IReadOnlyList<MethodCrapData> Methods { get; init; }
    public required IReadOnlyList<HistogramBin> Histogram { get; init; }
    public required IReadOnlyList<NamespaceCrapData> Namespaces { get; init; }
}
