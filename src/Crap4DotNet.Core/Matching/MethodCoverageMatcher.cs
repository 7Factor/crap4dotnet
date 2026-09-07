using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Performs a left-outer-join from complexity results to coverage entries per spec 6.4.
/// Every source method produces a result; unmatched methods default to coverage 0.0.
/// Uses two-pass matching: exact canonical key first, then name-only fallback.
/// </summary>
public static class MethodCoverageMatcher
{
    public static MatchResult Match(
        IReadOnlyList<MethodComplexityResult> complexityResults,
        IReadOnlyList<CoberturaMethodCoverage> coverageEntries)
    {
        // Build coverage lookups by normalized key
        var fullKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);
        var arityKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);
        var nameKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);

        foreach (var entry in coverageEntries)
        {
            var fullKey = CoberturaMethodParser.ToCanonicalKey(entry);
            AddToLookup(fullKeyLookup, fullKey, entry);

            AddToLookup(arityKeyLookup, StripArity(fullKey), entry);

            var nameKey = MethodKeyHelper.GetNameOnlyKey(fullKey);
            AddToLookup(nameKeyLookup, nameKey, entry);
        }

        var matchedFullKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedArityKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedNameKeys = new HashSet<string>(StringComparer.Ordinal);
        var methods = new List<MatchedMethod>();
        var unmatchedNames = new List<string>();
        var warnings = new List<DiagnosticWarning>();

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

            // Pass 1b: Retry with the method's generic arity dropped. Roslyn always knows a
            // method is generic and writes Foo<>; a Cobertura <method name> usually carries no
            // method-level arity at all, so those two keys can never be equal and EVERY generic
            // method would otherwise score 0.0 however well tested. Arity is kept in the exact
            // key above rather than stripped there, so a genuine Find<T>/Find<T,U> pair stays
            // distinguishable; this pass only relaxes it, and only when the result is unique.
            var arityKey = StripArity(fullKey);
            if (arityKeyLookup.TryGetValue(arityKey, out var arityMatches) && arityMatches.Count == 1)
            {
                matchedArityKeys.Add(arityKey);
                methods.Add(new MatchedMethod
                {
                    Complexity = complexity,
                    Coverage = arityMatches[0].Coverage
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

            // Check if matched by the arity-relaxed or name-only fallback
            if (matchedArityKeys.Contains(StripArity(kvp.Key)))
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

    /// <summary>Canonical key with the method's own generic arity removed.</summary>
    private static string StripArity(string canonicalKey)
    {
        var sigStart = MethodKeyHelper.FindSignatureStart(canonicalKey);
        return sigStart < 0
            ? MethodKeyHelper.StripMethodGenericArity(canonicalKey)
            : MethodKeyHelper.StripMethodGenericArity(canonicalKey[..sigStart]) + canonicalKey[sigStart..];
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
