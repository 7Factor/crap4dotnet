namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Spec 6.2 field selection, shared by the reader and the merger.
/// </summary>
public static class CoverageRate
{
    public static double Select(bool hasBranches, double? branchRate, double? lineRate) =>
        hasBranches && branchRate is not null ? branchRate.Value : lineRate ?? 0.0;

    public static double FromLines(IReadOnlyList<CoberturaLine> lines)
    {
        var branchesTotal = lines.Sum(l => l.BranchesTotal);
        var branchRate = branchesTotal > 0 ? (double)lines.Sum(l => l.BranchesCovered) / branchesTotal : (double?)null;
        var lineRate = lines.Count > 0 ? (double)lines.Count(l => l.Hit) / lines.Count : (double?)null;

        return Select(branchesTotal > 0, branchRate, lineRate);
    }
}
