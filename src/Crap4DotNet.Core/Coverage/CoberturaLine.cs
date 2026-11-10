namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// One source line of a method as read from Cobertura XML.
/// </summary>
public sealed record CoberturaLine
{
    public required int Number { get; init; }
    public required bool Hit { get; init; }
    public required int BranchesCovered { get; init; }
    public required int BranchesTotal { get; init; }
}
