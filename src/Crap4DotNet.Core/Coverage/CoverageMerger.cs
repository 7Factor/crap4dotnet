namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Merges the entries reported for the same method, from one coverage file or several.
/// </summary>
public static class CoverageMerger
{
    // Raw Cobertura names: the merge runs before matching normalizes them.
    private readonly record struct MethodKey(string ClassName, string MethodName, string Signature, string? FileName);

    public static IReadOnlyList<CoberturaMethodCoverage> Merge(IReadOnlyList<CoberturaMethodCoverage> entries) =>
        entries
            .GroupBy(e => new MethodKey(e.ClassName, e.MethodName, e.Signature, e.FileName?.ToUpperInvariant()))
            .Select(g => g.Count() == 1 ? g.First() : MergeSameMethod(g.ToList()))
            .ToList();

    private static CoberturaMethodCoverage MergeSameMethod(List<CoberturaMethodCoverage> entries)
    {
        var highestRate = entries.Max(e => e.Coverage);
        if (entries.Exists(e => e.Lines is null))
            return entries[0] with { Coverage = highestRate, Lines = null };

        var lines = entries
            .SelectMany(e => e.Lines!)
            .GroupBy(l => l.Number)
            .Select(MergeLine)
            .OrderBy(l => l.Number)
            .ToList();

        if (lines.Count == 0)
            return entries[0] with { Coverage = highestRate };

        return entries[0] with
        {
            Coverage = CoverageRate.FromLines(lines),
            Lines = lines,
            StartLine = lines[0].Number
        };
    }

    // Reports give branch counts, not which branches ran, so the merge keeps one reported
    // pair instead of adding counts that could describe the same branch twice.
    private static CoberturaLine MergeLine(IEnumerable<CoberturaLine> copies)
    {
        var all = copies.ToList();
        var mostCovered = all
            .OrderByDescending(BranchFraction)
            .ThenByDescending(l => l.BranchesTotal)
            .First();

        return mostCovered with { Hit = all.Exists(l => l.Hit) };
    }

    private static double BranchFraction(CoberturaLine line) =>
        line.BranchesTotal == 0 ? 0.0 : (double)line.BranchesCovered / line.BranchesTotal;
}
