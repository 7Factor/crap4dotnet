namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Merges the entries that several coverage files report for the same method.
/// </summary>
public static class CoverageMerger
{
    public static IReadOnlyList<CoberturaMethodCoverage> Merge(IReadOnlyList<CoberturaMethodCoverage> entries) =>
        entries
            .GroupBy(e => (e.ClassName, e.MethodName, e.Signature))
            .Select(g => g.Count() == 1 ? g.First() : MergeGroup(g.ToList()))
            .ToList();

    private static CoberturaMethodCoverage MergeGroup(List<CoberturaMethodCoverage> group)
    {
        if (group.Exists(e => e.Lines.Count == 0))
            return group[0] with { Coverage = group.Max(e => e.Coverage), Lines = [] };

        // Reports give branch counts, not which branches ran, so the highest count per
        // line is the most that is certain.
        var lines = group
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.Number)
            .Select(g => new CoberturaLine(
                g.Key,
                g.Any(l => l.Hit),
                g.Max(l => l.BranchesCovered),
                g.Max(l => l.BranchesTotal)))
            .OrderBy(l => l.Number)
            .ToList();

        return group[0] with { Coverage = Rate(lines), Lines = lines };
    }

    // Same choice as the reader: branch rate when the method branches, else line rate.
    private static double Rate(List<CoberturaLine> lines)
    {
        var branchesTotal = lines.Sum(l => l.BranchesTotal);
        return branchesTotal > 0
            ? (double)lines.Sum(l => l.BranchesCovered) / branchesTotal
            : (double)lines.Count(l => l.Hit) / lines.Count;
    }
}
