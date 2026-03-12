using Crap4DotNet.Core.Calculation;
using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Configuration;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Analysis;

/// <summary>
/// Top-level orchestrator: source → complexity → coverage → match → CRAP scores → stats.
/// Keeps the full pipeline in Core so the CLI is just a thin shell.
/// </summary>
public static class CrapAnalyzer
{
    public static AnalysisResult Analyze(
        IReadOnlyList<string> sourceFiles,
        IReadOnlyList<CoberturaMethodCoverage> coverageEntries,
        CrapOptions options,
        string projectName = "")
    {
        // Step 1: Compute complexity for all source files
        var complexityResults = new List<MethodComplexityResult>();
        foreach (var (filePath, sourceCode) in sourceFiles.Select(f => (f, File.ReadAllText(f))))
        {
            var results = CyclomaticComplexityWalker.Analyze(sourceCode, filePath);
            complexityResults.AddRange(results);
        }

        return AnalyzeFromResults(complexityResults, coverageEntries, options, projectName);
    }

    /// <summary>
    /// Run the pipeline from pre-computed complexity and coverage data.
    /// Useful for testing without file I/O.
    /// </summary>
    public static AnalysisResult AnalyzeFromResults(
        IReadOnlyList<MethodComplexityResult> complexityResults,
        IReadOnlyList<CoberturaMethodCoverage> coverageEntries,
        CrapOptions options,
        string projectName = "")
    {
        // Step 2: Match complexity to coverage (left outer join)
        var matchResult = MethodCoverageMatcher.Match(complexityResults, coverageEntries);

        // Step 3: Calculate CRAP score for each method
        var methods = new List<MethodCrapData>();
        foreach (var matched in matchResult.Methods)
        {
            var crapScore = CrapCalculator.Calculate(matched.Complexity.Complexity, matched.Coverage);
            var isCrappy = crapScore > options.Threshold;
            var crapLoad = CrapLoadCalculator.Calculate(
                matched.Complexity.Complexity, matched.Coverage, options.Threshold);
            var severity = SeverityBandClassifier.Classify(crapScore);

            methods.Add(new MethodCrapData
            {
                Identity = matched.Complexity.Identity,
                CrapScore = crapScore,
                Complexity = matched.Complexity.Complexity,
                Coverage = matched.Coverage,
                CrapLoad = crapLoad,
                IsCrappy = isCrappy,
                Severity = severity
            });
        }

        // Step 4: Compute stats, histogram, hierarchy
        var stats = CrapStatisticsCalculator.Calculate(methods);
        var histogram = HistogramGenerator.Generate(methods);
        var hierarchy = HierarchyBuilder.Build(methods);

        var projectData = new ProjectCrapData
        {
            Project = projectName,
            Threshold = options.Threshold,
            Stats = stats,
            Methods = methods,
            Histogram = histogram,
            Namespaces = hierarchy
        };

        return new AnalysisResult
        {
            Data = projectData,
            Warnings = matchResult.Warnings
        };
    }
}
