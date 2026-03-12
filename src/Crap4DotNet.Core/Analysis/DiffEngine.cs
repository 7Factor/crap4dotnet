using System.Text.Json;

namespace Crap4DotNet.Core.Analysis;

/// <summary>
/// Compares two CRAP analysis JSON reports and classifies changes per spec 10.2.2.
/// Priority order: added > removed > new_crappy > fixed > regressed > improved > unchanged.
/// </summary>
public static class DiffEngine
{
    private const double ChangeTolerance = 0.01;

    public static DiffResult Compare(JsonDocument before, JsonDocument after, int threshold = 30)
    {
        var beforeMethods = ExtractMethods(before);
        var afterMethods = ExtractMethods(after);

        var beforeProject = before.RootElement.TryGetProperty("project", out var bp) ? bp.GetString() ?? "" : "";
        var afterProject = after.RootElement.TryGetProperty("project", out var ap) ? ap.GetString() ?? "" : "";
        var beforeTimestamp = before.RootElement.TryGetProperty("timestamp", out var bt) ? bt.GetString() ?? "" : "";
        var afterTimestamp = after.RootElement.TryGetProperty("timestamp", out var at) ? at.GetString() ?? "" : "";

        if (before.RootElement.TryGetProperty("threshold", out var th))
            threshold = th.GetInt32();

        var added = new List<DiffMethod>();
        var removed = new List<DiffMethod>();
        var newCrappy = new List<DiffMethod>();
        var fixedCrappy = new List<DiffMethod>();
        var regressed = new List<DiffMethod>();
        var improved = new List<DiffMethod>();
        var unchangedCount = 0;

        // Find added and changed methods
        foreach (var (fullName, afterMethod) in afterMethods)
        {
            if (!beforeMethods.TryGetValue(fullName, out var beforeMethod))
            {
                // P1: Added
                added.Add(new DiffMethod
                {
                    FullName = fullName,
                    Status = "added",
                    Crap = afterMethod.Crap,
                    IsCrappy = afterMethod.Crap > threshold
                });
                continue;
            }

            Classify(fullName, beforeMethod, afterMethod, threshold,
                newCrappy, fixedCrappy, regressed, improved, ref unchangedCount);
        }

        // Find removed methods
        foreach (var (fullName, beforeMethod) in beforeMethods)
        {
            if (!afterMethods.ContainsKey(fullName))
            {
                // P2: Removed
                removed.Add(new DiffMethod
                {
                    FullName = fullName,
                    Status = "removed",
                    Crap = beforeMethod.Crap,
                    WasCrappy = beforeMethod.Crap > threshold
                });
            }
        }

        var beforeStats = ExtractStats(before);
        var afterStats = ExtractStats(after);

        return new DiffResult
        {
            BeforeProject = beforeProject,
            AfterProject = afterProject,
            BeforeTimestamp = beforeTimestamp,
            AfterTimestamp = afterTimestamp,
            Summary = new DiffSummary
            {
                TotalMethodsBefore = beforeMethods.Count,
                TotalMethodsAfter = afterMethods.Count,
                CrappyMethodsBefore = beforeStats.crappyCount,
                CrappyMethodsAfter = afterStats.crappyCount,
                TotalCrapLoadBefore = beforeStats.crapLoad,
                TotalCrapLoadAfter = afterStats.crapLoad,
                NewCrappy = newCrappy.Count,
                FixedCrappy = fixedCrappy.Count,
                Added = added.Count,
                Removed = removed.Count,
                Improved = improved.Count,
                Regressed = regressed.Count,
                Unchanged = unchangedCount
            },
            Added = added,
            Removed = removed,
            NewCrappy = newCrappy,
            FixedCrappy = fixedCrappy,
            Regressed = regressed,
            Improved = improved
        };
    }

    private static void Classify(
        string fullName,
        MethodSnapshot before,
        MethodSnapshot after,
        int threshold,
        List<DiffMethod> newCrappy,
        List<DiffMethod> fixedCrappy,
        List<DiffMethod> regressed,
        List<DiffMethod> improved,
        ref int unchangedCount)
    {
        var wasCrappy = before.Crap > threshold;
        var isCrappy = after.Crap > threshold;
        var delta = after.Crap - before.Crap;

        // P3: Threshold crossed upward
        if (!wasCrappy && isCrappy)
        {
            newCrappy.Add(MakeChangedMethod(fullName, before, after, "new_crappy"));
            return;
        }

        // P4: Threshold crossed downward
        if (wasCrappy && !isCrappy)
        {
            fixedCrappy.Add(MakeChangedMethod(fullName, before, after, "fixed"));
            return;
        }

        // P5/P6: Score changed significantly
        if (Math.Abs(delta) > ChangeTolerance)
        {
            if (delta > 0)
                regressed.Add(MakeChangedMethod(fullName, before, after, "regressed"));
            else
                improved.Add(MakeChangedMethod(fullName, before, after, "improved"));
            return;
        }

        // P7: Unchanged
        unchangedCount++;
    }

    private static DiffMethod MakeChangedMethod(
        string fullName, MethodSnapshot before, MethodSnapshot after, string status) =>
        new()
        {
            FullName = fullName,
            Status = status,
            CrapBefore = before.Crap,
            CrapAfter = after.Crap,
            ComplexityBefore = before.Complexity,
            ComplexityAfter = after.Complexity,
            CoverageBefore = before.Coverage,
            CoverageAfter = after.Coverage,
            Delta = Math.Round(after.Crap - before.Crap, 2)
        };

    private static Dictionary<string, MethodSnapshot> ExtractMethods(JsonDocument doc)
    {
        var methods = new Dictionary<string, MethodSnapshot>(StringComparer.Ordinal);
        if (!doc.RootElement.TryGetProperty("methods", out var methodsArray))
            return methods;

        foreach (var m in methodsArray.EnumerateArray())
        {
            var fullName = m.GetProperty("fullName").GetString() ?? "";
            methods[fullName] = new MethodSnapshot
            {
                Crap = m.GetProperty("crap").GetDouble(),
                Complexity = m.TryGetProperty("complexity", out var c) ? c.GetInt32() : 0,
                Coverage = m.TryGetProperty("coverage", out var cov) ? cov.GetDouble() : 0.0
            };
        }

        return methods;
    }

    private static (int crappyCount, double crapLoad) ExtractStats(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("stats", out var stats))
            return (0, 0);

        var crappy = stats.TryGetProperty("crappyMethodCount", out var cc) ? cc.GetInt32() : 0;
        var load = stats.TryGetProperty("totalCrapLoad", out var cl) ? cl.GetDouble() : 0;
        return (crappy, load);
    }

    private readonly record struct MethodSnapshot
    {
        public double Crap { get; init; }
        public int Complexity { get; init; }
        public double Coverage { get; init; }
    }
}

public sealed record DiffResult
{
    public required string BeforeProject { get; init; }
    public required string AfterProject { get; init; }
    public required string BeforeTimestamp { get; init; }
    public required string AfterTimestamp { get; init; }
    public required DiffSummary Summary { get; init; }
    public required IReadOnlyList<DiffMethod> Added { get; init; }
    public required IReadOnlyList<DiffMethod> Removed { get; init; }
    public required IReadOnlyList<DiffMethod> NewCrappy { get; init; }
    public required IReadOnlyList<DiffMethod> FixedCrappy { get; init; }
    public required IReadOnlyList<DiffMethod> Regressed { get; init; }
    public required IReadOnlyList<DiffMethod> Improved { get; init; }
}

public sealed record DiffSummary
{
    public int TotalMethodsBefore { get; init; }
    public int TotalMethodsAfter { get; init; }
    public int CrappyMethodsBefore { get; init; }
    public int CrappyMethodsAfter { get; init; }
    public double TotalCrapLoadBefore { get; init; }
    public double TotalCrapLoadAfter { get; init; }
    public int NewCrappy { get; init; }
    public int FixedCrappy { get; init; }
    public int Added { get; init; }
    public int Removed { get; init; }
    public int Improved { get; init; }
    public int Regressed { get; init; }
    public int Unchanged { get; init; }
}

public sealed record DiffMethod
{
    public required string FullName { get; init; }
    public required string Status { get; init; }
    public double? Crap { get; init; }
    public double? CrapBefore { get; init; }
    public double? CrapAfter { get; init; }
    public int? ComplexityBefore { get; init; }
    public int? ComplexityAfter { get; init; }
    public double? CoverageBefore { get; init; }
    public double? CoverageAfter { get; init; }
    public double? Delta { get; init; }
    public bool? IsCrappy { get; init; }
    public bool? WasCrappy { get; init; }
}
