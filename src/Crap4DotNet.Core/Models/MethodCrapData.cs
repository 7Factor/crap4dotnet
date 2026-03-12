namespace Crap4DotNet.Core.Models;

/// <summary>
/// CRAP analysis result for a single method.
/// </summary>
public sealed record MethodCrapData
{
    public required MethodIdentity Identity { get; init; }
    public required double CrapScore { get; init; }
    public required int Complexity { get; init; }
    public required double Coverage { get; init; }
    public required double CrapLoad { get; init; }
    public required bool IsCrappy { get; init; }
    public required SeverityBand Severity { get; init; }
}
