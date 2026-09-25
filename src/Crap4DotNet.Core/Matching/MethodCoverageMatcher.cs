using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Performs a left-outer-join from complexity results to coverage entries per spec 6.4.
/// Every source method produces a result; unmatched methods default to coverage 0.0.
/// Matching passes, in order: exact canonical key, relaxed key, name-only key.
/// </summary>
public static class MethodCoverageMatcher
{
    public static MatchResult Match(
        IReadOnlyList<MethodComplexityResult> complexityResults,
        IReadOnlyList<CoberturaMethodCoverage> coverageEntries)
    {
        // Build coverage lookups by normalized key
        var fullKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);
        var relaxedKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);
        var nameKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);

        foreach (var entry in coverageEntries)
        {
            var fullKey = CoberturaMethodParser.ToCanonicalKey(entry);
            AddToLookup(fullKeyLookup, fullKey, entry);

            AddToLookup(relaxedKeyLookup, RelaxKey(fullKey), entry);

            var nameKey = MethodKeyHelper.GetNameOnlyKey(fullKey);
            AddToLookup(nameKeyLookup, nameKey, entry);
        }

        var matchedFullKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedRelaxedKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedNameKeys = new HashSet<string>(StringComparer.Ordinal);
        var methods = new List<MatchedMethod>();
        var unmatchedNames = new List<string>();
        var warnings = new List<DiagnosticWarning>();
        var sourceCountByRelaxedKey = complexityResults
            .GroupBy(c => RelaxKey(RoslynMethodParser.ToCanonicalKey(c.Identity)), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var complexity in complexityResults)
        {
            var fullKey = RoslynMethodParser.ToCanonicalKey(complexity.Identity);

            // Pass 1: Exact match on full canonical key (includes signature)
            if (fullKeyLookup.TryGetValue(fullKey, out var exactMatches))
            {
                matchedFullKeys.Add(fullKey);
                methods.Add(new MatchedMethod
                {
                    Complexity = complexity,
                    Coverage = exactMatches[0].Coverage
                });
                continue;
            }

            // Pass 1b: Relaxed key. Match only when exactly one coverage entry and exactly
            // one source method have this key.
            var relaxedKey = RelaxKey(fullKey);
            if (relaxedKeyLookup.TryGetValue(relaxedKey, out var relaxedMatches)
                && relaxedMatches.Count == 1
                && sourceCountByRelaxedKey[relaxedKey] == 1)
            {
                matchedRelaxedKeys.Add(relaxedKey);
                methods.Add(new MatchedMethod
                {
                    Complexity = complexity,
                    Coverage = relaxedMatches[0].Coverage
                });
                continue;
            }

            // Pass 2: Fallback to name-only key (without signature)
            var nameKey = MethodKeyHelper.GetNameOnlyKey(fullKey);
            if (nameKeyLookup.TryGetValue(nameKey, out var nameMatches) && nameMatches.Count == 1)
            {
                matchedNameKeys.Add(nameKey);
                methods.Add(new MatchedMethod
                {
                    Complexity = complexity,
                    Coverage = nameMatches[0].Coverage
                });
                continue;
            }

            // No match found → default to 0.0
            unmatchedNames.Add(complexity.Identity.FullName);
            methods.Add(new MatchedMethod
            {
                Complexity = complexity,
                Coverage = 0.0
            });
        }

        // Count orphaned coverage entries (not matched by either pass)
        var orphanedCount = 0;
        var orphanedNames = new List<string>();

        foreach (var kvp in fullKeyLookup)
        {
            if (matchedFullKeys.Contains(kvp.Key))
                continue;

            // Check if matched by the relaxed or name-only fallback
            if (matchedRelaxedKeys.Contains(RelaxKey(kvp.Key)))
                continue;

            var nameKey = MethodKeyHelper.GetNameOnlyKey(kvp.Key);
            if (matchedNameKeys.Contains(nameKey))
                continue;

            orphanedCount += kvp.Value.Count;
            orphanedNames.AddRange(
                kvp.Value.Select(c => $"{c.ClassName}.{c.MethodName}"));
        }

        // Emit warnings
        if (unmatchedNames.Count > 0)
        {
            var preview = string.Join(", ", unmatchedNames.Take(5));
            var suffix = unmatchedNames.Count > 5
                ? $" and {unmatchedNames.Count - 5} more"
                : "";
            warnings.Add(new DiagnosticWarning
            {
                Code = "UNMATCHED_METHODS",
                Message = $"{unmatchedNames.Count} method(s) have no coverage data (defaulting to 0.0): {preview}{suffix}"
            });
        }

        if (orphanedCount > 0)
        {
            var preview = string.Join(", ", orphanedNames.Take(5));
            var suffix = orphanedCount > 5
                ? $" and {orphanedCount - 5} more"
                : "";
            warnings.Add(new DiagnosticWarning
            {
                Code = "ORPHANED_COVERAGE",
                Message = $"{orphanedCount} coverage entry/entries have no matching source method: {preview}{suffix}"
            });
        }

        // Version mismatch detection: >20% orphaned
        if (coverageEntries.Count > 0)
        {
            var orphanedPercent = (double)orphanedCount / coverageEntries.Count * 100;
            if (orphanedPercent > 20)
            {
                warnings.Add(new DiagnosticWarning
                {
                    Code = "COVERAGE_STALE",
                    Message = $"Coverage data may be stale: {orphanedPercent:F0}% of coverage entries ({orphanedCount}/{coverageEntries.Count}) have no matching source method."
                });
            }
        }

        return new MatchResult
        {
            Methods = methods,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Canonical key without the method's generic arity and without <c>?</c> annotations.
    /// Cobertura method names often have no arity, and CLR signatures have no nullable reference types.
    /// </summary>
    private static string RelaxKey(string canonicalKey)
    {
        var sigStart = MethodKeyHelper.FindSignatureStart(canonicalKey);
        return sigStart < 0
            ? MethodKeyHelper.StripMethodGenericArity(canonicalKey)
            : MethodKeyHelper.StripMethodGenericArity(canonicalKey[..sigStart])
              + canonicalKey[sigStart..].Replace("?", "", StringComparison.Ordinal);
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
