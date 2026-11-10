using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Performs a left-outer-join from complexity results to coverage entries per spec 6.4.
/// Every source method produces a result; unmatched methods default to coverage 0.0.
/// </summary>
public static class MethodCoverageMatcher
{
    public static MatchResult Match(
        IReadOnlyList<MethodComplexityResult> complexityResults,
        IReadOnlyList<CoberturaMethodCoverage> reportedEntries)
    {
        // Every pass assumes one entry per method, and each test project's report repeats
        // the assemblies it loads.
        var coverageEntries = CoverageMerger.Merge(reportedEntries);
        var index = new CoverageIndex(complexityResults, coverageEntries);

        var methods = new List<MatchedMethod>();
        var unmatchedNames = new List<string>();
        foreach (var complexity in complexityResults)
        {
            var coverage = index.Find(complexity);
            if (coverage is null)
                unmatchedNames.Add(complexity.Identity.FullName);
            methods.Add(new MatchedMethod { Complexity = complexity, Coverage = coverage ?? 0.0 });
        }

        return new MatchResult
        {
            Methods = methods,
            Warnings = BuildWarnings(unmatchedNames, index.UnsplitEntries, index.OrphanedEntryNames(), coverageEntries.Count)
        };
    }

    private sealed class CoverageIndex
    {
        private readonly Dictionary<string, List<CoberturaMethodCoverage>> _byFullKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<CoberturaMethodCoverage>> _byRelaxedKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<CoberturaMethodCoverage>> _byNameKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<MethodComplexityResult>> _sourcesByRelaxedKey;
        private readonly Dictionary<string, List<MethodComplexityResult>> _sourcesByNameKey;
        private readonly Dictionary<MethodIdentity, double?> _splitCoverage;
        private readonly HashSet<string> _matchedFullKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _matchedRelaxedKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _matchedNameKeys = new(StringComparer.Ordinal);

        public List<string> UnsplitEntries { get; } = [];

        public CoverageIndex(
            IReadOnlyList<MethodComplexityResult> complexityResults,
            IReadOnlyList<CoberturaMethodCoverage> coverageEntries)
        {
            foreach (var entry in coverageEntries)
            {
                var fullKey = CoberturaMethodParser.ToCanonicalKey(entry);
                AddToLookup(_byFullKey, fullKey, entry);
                AddToLookup(_byRelaxedKey, RelaxKey(fullKey), entry);
                AddToLookup(_byNameKey, MethodKeyHelper.GetNameOnlyKey(fullKey), entry);
            }

            _sourcesByRelaxedKey = GroupSources(complexityResults, RelaxKey);
            _sourcesByNameKey = GroupSources(complexityResults, MethodKeyHelper.GetNameOnlyKey);
            _splitCoverage = SplitSharedEntries(_sourcesByRelaxedKey, _byRelaxedKey, UnsplitEntries);
        }

        public double? Find(MethodComplexityResult complexity)
        {
            var fullKey = RoslynMethodParser.ToCanonicalKey(complexity.Identity);
            var relaxedKey = RelaxKey(fullKey);
            var nameKey = MethodKeyHelper.GetNameOnlyKey(fullKey);

            if (_splitCoverage.TryGetValue(complexity.Identity, out var splitRate))
            {
                _matchedRelaxedKeys.Add(relaxedKey);
                return splitRate;
            }

            if (_byFullKey.TryGetValue(fullKey, out var exact))
            {
                _matchedFullKeys.Add(fullKey);
                return exact[0].Coverage;
            }

            if (_byRelaxedKey.TryGetValue(relaxedKey, out var relaxed)
                && relaxed.Count == 1
                && _sourcesByRelaxedKey[relaxedKey].Count == 1)
            {
                _matchedRelaxedKeys.Add(relaxedKey);
                return relaxed[0].Coverage;
            }

            if (_byNameKey.TryGetValue(nameKey, out var byName)
                && TryResolveByName(byName, complexity, _sourcesByNameKey[nameKey], out var nameRate))
            {
                _matchedNameKeys.Add(nameKey);
                return nameRate;
            }

            return null;
        }

        public List<string> OrphanedEntryNames() =>
            _byFullKey
                .Where(kvp => !_matchedFullKeys.Contains(kvp.Key)
                              && !_matchedRelaxedKeys.Contains(RelaxKey(kvp.Key))
                              && !_matchedNameKeys.Contains(MethodKeyHelper.GetNameOnlyKey(kvp.Key)))
                .SelectMany(kvp => kvp.Value.Select(c => $"{c.ClassName}.{c.MethodName}"))
                .ToList();

        private static Dictionary<string, List<MethodComplexityResult>> GroupSources(
            IReadOnlyList<MethodComplexityResult> complexityResults, Func<string, string> toKey) =>
            complexityResults
                .GroupBy(c => toKey(RoslynMethodParser.ToCanonicalKey(c.Identity)), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }

    private static List<DiagnosticWarning> BuildWarnings(
        List<string> unmatchedNames, List<string> unsplitEntries, List<string> orphanedNames, int entryCount)
    {
        var warnings = new List<DiagnosticWarning>();

        if (unmatchedNames.Count > 0)
            warnings.Add(new DiagnosticWarning
            {
                Code = "UNMATCHED_METHODS",
                Message = $"{unmatchedNames.Count} method(s) have no coverage data (defaulting to 0.0): {Preview(unmatchedNames)}"
            });

        if (unsplitEntries.Count > 0)
            warnings.Add(new DiagnosticWarning
            {
                Code = "SHARED_COVERAGE",
                Message = $"{unsplitEntries.Count} coverage entry/entries each cover several methods and have no line data to split them, so those methods' coverage may be wrong: {Preview(unsplitEntries)}"
            });

        if (orphanedNames.Count > 0)
            warnings.Add(new DiagnosticWarning
            {
                Code = "ORPHANED_COVERAGE",
                Message = $"{orphanedNames.Count} coverage entry/entries have no matching source method: {Preview(orphanedNames)}"
            });

        var orphanedPercent = entryCount == 0 ? 0 : (double)orphanedNames.Count / entryCount * 100;
        if (orphanedPercent > 20)
            warnings.Add(new DiagnosticWarning
            {
                Code = "COVERAGE_STALE",
                Message = $"Coverage data may be stale: {orphanedPercent:F0}% of coverage entries ({orphanedNames.Count}/{entryCount}) have no matching source method."
            });

        return warnings;
    }

    private static string Preview(List<string> names) =>
        string.Join(", ", names.Take(5)) + (names.Count > 5 ? $" and {names.Count - 5} more" : "");

    // Cobertura method names often omit generic arity, and CLR signatures have no nullable reference types.
    private static string RelaxKey(string canonicalKey)
    {
        var sigStart = MethodKeyHelper.FindSignatureStart(canonicalKey);
        return sigStart < 0
            ? MethodKeyHelper.StripMethodGenericArity(canonicalKey)
            : MethodKeyHelper.StripMethodGenericArity(canonicalKey[..sigStart])
              + MethodKeyHelper.StripNullableReferenceAnnotations(canonicalKey[sigStart..]);
    }

    // Coverlet writes one entry for Foo(int) and Foo<T>(int), holding the lines of both.
    private static Dictionary<MethodIdentity, double?> SplitSharedEntries(
        Dictionary<string, List<MethodComplexityResult>> sourcesByRelaxedKey,
        Dictionary<string, List<CoberturaMethodCoverage>> relaxedKeyLookup,
        List<string> unsplitEntries)
    {
        var rates = new Dictionary<MethodIdentity, double?>();
        foreach (var (key, sources) in sourcesByRelaxedKey)
        {
            if (sources.Count < 2 || !relaxedKeyLookup.TryGetValue(key, out var entries) || entries.Count != 1)
                continue;

            var entry = entries[0];
            if (!CanSplit(entry, sources))
            {
                unsplitEntries.Add($"{entry.ClassName}.{entry.MethodName}");
                continue;
            }

            foreach (var source in sources)
            {
                var lines = entry.Lines!
                    .Where(l => l.Number >= source.Identity.LineNumber && l.Number <= source.Identity.EndLineNumber)
                    .ToList();
                rates[source.Identity] = lines.Count == 0 ? null : CoverageRate.FromLines(lines);
            }
        }

        return rates;
    }

    private static bool CanSplit(CoberturaMethodCoverage entry, List<MethodComplexityResult> sources) =>
        entry.Lines is not null
        && sources.TrueForAll(s => s.Identity.LineNumber is not null
                                   && s.Identity.EndLineNumber is not null
                                   && SameFile(s.Identity.FilePath, entry.FileName));

    // Async and iterator entries have no signature, so overloads share a name key.
    // Their state machines keep the source line numbers, so overloads pair up by line order.
    private static bool TryResolveByName(
        List<CoberturaMethodCoverage> entries,
        MethodComplexityResult complexity,
        List<MethodComplexityResult> sources,
        out double coverage)
    {
        coverage = 0.0;

        if (entries.Count == 1 && sources.Count == 1)
        {
            coverage = entries[0].Coverage;
            return true;
        }

        if (entries.Count != sources.Count
            || entries.Exists(e => e.StartLine is null || !SameFile(complexity.Identity.FilePath, e.FileName))
            || sources.Exists(s => s.Identity.LineNumber is null))
            return false;

        var index = sources
            .OrderBy(s => s.Identity.LineNumber)
            .ToList()
            .FindIndex(s => s.Identity == complexity.Identity);
        coverage = entries.OrderBy(e => e.StartLine).ElementAt(index).Coverage;
        return true;
    }

    // Cobertura file names are relative to the project; Roslyn paths are absolute.
    private static bool SameFile(string? sourcePath, string? coverageFile)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(coverageFile))
            return false;

        var source = sourcePath.Replace('\\', '/');
        var report = coverageFile.Replace('\\', '/');
        return source.EndsWith(report, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(source), Path.GetFileName(report), StringComparison.OrdinalIgnoreCase);
    }

    private static void AddToLookup(
        Dictionary<string, List<CoberturaMethodCoverage>> lookup,
        string key,
        CoberturaMethodCoverage entry)
    {
        if (!lookup.TryGetValue(key, out var list))
        {
            list = [];
            lookup[key] = list;
        }

        list.Add(entry);
    }
}
