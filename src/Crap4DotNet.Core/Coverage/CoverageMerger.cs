namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Merges the entries reported for the same method, from one coverage file or several.
/// </summary>
public static class CoverageMerger
{
    private readonly record struct MethodKey(string ClassName, string MethodName, string Signature, string? FileName);

    public static IReadOnlyList<CoberturaMethodCoverage> Merge(IReadOnlyList<CoberturaMethodCoverage> entries) =>
        entries
            .GroupBy(e => new MethodKey(e.ClassName, e.MethodName, e.Signature, e.FileName))
            .Select(g => g.Count() == 1 ? g.First() : MergeSameMethod(g.ToList()))
            .ToList();

    private static CoberturaMethodCoverage MergeSameMethod(List<CoberturaMethodCoverage> entries)
    {
        if (entries.Exists(e => e.Lines.Count == 0))
            return entries[0] with { Coverage = entries.Max(e => e.Coverage), Lines = [] };

        var lines = entries
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.Number)
            .Select(MergeLine)
            .OrderBy(l => l.Number)
            .ToList();

        return entries[0] with { Coverage = CoverageRate.FromLines(lines), Lines = lines };
    }

    // Reports give branch counts, not which branches ran, so the merge keeps one reported
    // pair instead of combining counts that no report gave.
    private static CoberturaLine MergeLine(IEnumerable<CoberturaLine> copies)
    {
        var all = copies.ToList();
        var branches = all
            .OrderByDescending(l => l.BranchesTotal)
            .ThenByDescending(l => l.BranchesCovered)
            .First();

        return branches with { Hit = all.Exists(l => l.Hit) };
    }
}
