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
        var nameKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);
        var erasedKeyLookup = new Dictionary<string, List<CoberturaMethodCoverage>>(StringComparer.Ordinal);

        foreach (var entry in coverageEntries)
        {
            var fullKey = CoberturaMethodParser.ToCanonicalKey(entry);
            AddToLookup(fullKeyLookup, fullKey, entry);

            var nameKey = MethodKeyHelper.GetNameOnlyKey(fullKey);
            AddToLookup(nameKeyLookup, nameKey, entry);

            var erasedKey = MethodKeyHelper.EraseMethodGenericArity(nameKey);
            if (!string.Equals(erasedKey, nameKey, StringComparison.Ordinal))
                AddToLookup(erasedKeyLookup, erasedKey, entry);
            else
                AddToLookup(erasedKeyLookup, nameKey, entry);
        }

        // Source methods that share a name-only key are the overloads a signature-less
        // coverage key cannot tell apart; keep them together so they can be paired by
        // source position when that happens.
        var sourceGroups = new Dictionary<string, List<MethodComplexityResult>>(StringComparer.Ordinal);
        var erasedSourceGroups = new Dictionary<string, List<MethodComplexityResult>>(StringComparer.Ordinal);
        foreach (var complexity in complexityResults)
        {
            var key = MethodKeyHelper.GetNameOnlyKey(RoslynMethodParser.ToCanonicalKey(complexity.Identity));
            AddToGroup(sourceGroups, key, complexity);

            // The erased pass collapses Apply<T> and Apply onto one key, so it needs
            // its own grouping rather than reusing the name-only one.
            AddToGroup(erasedSourceGroups, MethodKeyHelper.EraseMethodGenericArity(key), complexity);
        }

        var matchedFullKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedNameKeys = new HashSet<string>(StringComparer.Ordinal);
        var matchedErasedKeys = new HashSet<string>(StringComparer.Ordinal);
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
                    // One method can appear in several coverage files. Covered by any
                    // of them means covered, and picking the first would make the
                    // result depend on the order the files were passed in.
                    Complexity = complexity,
                    Coverage = exactMatches.Max(m => m.Coverage)
                });
                continue;
            }

            // Pass 2: Fallback to name-only key (without signature)
            var nameKey = MethodKeyHelper.GetNameOnlyKey(fullKey);
            if (nameKeyLookup.TryGetValue(nameKey, out var nameMatches)
                && TryResolveForMethod(nameMatches, complexity, sourceGroups[nameKey], out var nameCoverage))
            {
                matchedNameKeys.Add(nameKey);
                methods.Add(new MatchedMethod
                {
                    Complexity = complexity,
                    Coverage = nameCoverage
                });
                continue;
            }

            // Pass 3: Generic methods carry no arity on the Cobertura side, so fall
            // back to a key with the method's arity erased from both sides.
            var erasedKey = MethodKeyHelper.EraseMethodGenericArity(nameKey);
            if (erasedKeyLookup.TryGetValue(erasedKey, out var erasedMatches)
                && TryResolveForMethod(
                    erasedMatches, complexity, erasedSourceGroups[erasedKey], out var erasedCoverage))
            {
                matchedErasedKeys.Add(erasedKey);
                methods.Add(new MatchedMethod
                {
                    Complexity = complexity,
                    Coverage = erasedCoverage
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

            // Check if matched by name-only fallback
            var nameKey = MethodKeyHelper.GetNameOnlyKey(kvp.Key);
            if (matchedNameKeys.Contains(nameKey))
                continue;

            // Check if matched by the generic-erased fallback
            if (matchedErasedKeys.Contains(MethodKeyHelper.EraseMethodGenericArity(nameKey)))
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
    /// Pick the coverage entry belonging to one specific source method from candidates
    /// that share a name-only key.
    /// </summary>
    /// <remarks>
    /// Candidates collide for two unrelated reasons. The same method is reported once
    /// per coverage file, because every file describes the whole assembly it loaded;
    /// those entries merge to the highest coverage observed. Separately, overloads
    /// collide whenever a signature-less key is in play, which is unavoidable for
    /// async and iterator methods. Those are told apart by source position, since a
    /// generated state machine keeps the positions of the method it was rewritten
    /// from. Pairing is positional and only applies when every overload has exactly
    /// one entry; anything less certain returns false so the caller declines rather
    /// than attributing one overload's coverage to another.
    /// </remarks>
    private static bool TryResolveForMethod(
        List<CoberturaMethodCoverage> candidates,
        MethodComplexityResult complexity,
        List<MethodComplexityResult> siblings,
        out double coverage)
    {
        coverage = 0.0;

        if (candidates.Count == 0)
            return false;

        // Collapse the same entry arriving from several coverage files.
        var distinct = candidates
            .GroupBy(c => (c.ClassName, c.MethodName, c.Signature, c.StartLine))
            .Select(g => new
            {
                g.First().FileName,
                g.First().StartLine,
                Coverage = g.Max(c => c.Coverage)
            })
            .ToList();

        if (distinct.Count == 1 && siblings.Count == 1)
        {
            coverage = distinct[0].Coverage;
            return true;
        }

        // More than one real method behind the key: pair by source position, and only
        // when the two sides line up exactly.
        if (distinct.Count != siblings.Count)
            return false;

        if (distinct.Exists(d => d.StartLine is null)
            || siblings.Exists(sibling => sibling.Identity.LineNumber is null))
            return false;

        if (!distinct.TrueForAll(d => SameFile(complexity.Identity.FilePath, d.FileName)))
            return false;

        var orderedEntries = distinct.OrderBy(d => d.StartLine).ToList();
        var orderedSiblings = siblings.OrderBy(sibling => sibling.Identity.LineNumber).ToList();

        var index = orderedSiblings.FindIndex(sibling => sibling.Identity == complexity.Identity);
        if (index < 0)
            return false;

        coverage = orderedEntries[index].Coverage;
        return true;
    }

    /// <summary>
    /// Whether a Roslyn file path and a Cobertura filename name the same file. The
    /// report path is relative to the project, the Roslyn path is absolute.
    /// </summary>
    private static bool SameFile(string? sourcePath, string? coverageFile)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(coverageFile))
            return false;

        var source = sourcePath.Replace('\\', '/');
        var report = coverageFile.Replace('\\', '/');

        return source.EndsWith(report, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Path.GetFileName(source), Path.GetFileName(report), StringComparison.OrdinalIgnoreCase);
    }

    private static void AddToGroup(
        Dictionary<string, List<MethodComplexityResult>> groups,
        string key,
        MethodComplexityResult complexity)
    {
        if (!groups.TryGetValue(key, out var group))
        {
            group = [];
            groups[key] = group;
        }

        group.Add(complexity);
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
