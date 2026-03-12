namespace Crap4DotNet.Core.Models;

/// <summary>
/// Aggregated CRAP statistics, reused at project, namespace, and class levels.
/// Nullable fields are null when <see cref="MethodCount"/> is zero.
/// </summary>
public sealed record CrapStatistics
{
    public required int MethodCount { get; init; }
    public required double TotalCrap { get; init; }
    public required double? AverageCrap { get; init; }
    public required double? MedianCrap { get; init; }
    public required double? StandardDeviation { get; init; }
    public required int CrappyMethodCount { get; init; }
    public required double CrappyMethodPercent { get; init; }
    public required double TotalCrapLoad { get; init; }
}
