namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Result of the left-outer-join from complexity to coverage.
/// Every source method produces a MatchedMethod entry; orphaned coverage is reported as warnings.
/// </summary>
public sealed record MatchResult
{
    public required IReadOnlyList<MatchedMethod> Methods { get; init; }
    public required IReadOnlyList<DiagnosticWarning> Warnings { get; init; }
}
