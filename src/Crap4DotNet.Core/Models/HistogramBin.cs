namespace Crap4DotNet.Core.Models;

/// <summary>
/// A single bin in the CRAP score histogram.
/// Bins use half-open intervals: [lower, upper). A score of exactly 5.0 falls in "5-10".
/// </summary>
public sealed record HistogramBin
{
    public required string Range { get; init; }
    public required int Count { get; init; }
    public required double Percent { get; init; }
}
