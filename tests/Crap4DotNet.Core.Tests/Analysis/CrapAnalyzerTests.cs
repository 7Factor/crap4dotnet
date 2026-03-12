using Crap4DotNet.Core.Analysis;
using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Configuration;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Analysis;

public sealed class CrapAnalyzerTests
{
    private static MethodComplexityResult MakeComplexity(
        string methodName, int complexity,
        string ns = "MyApp", string className = "Service") =>
        new()
        {
            Identity = new MethodIdentity
            {
                Namespace = ns,
                ClassName = className,
                MethodName = methodName,
                Signature = "()",
                FullName = $"{ns}.{className}.{methodName}()",
                FilePath = "Test.cs",
                LineNumber = 1
            },
            Complexity = complexity
        };

    private static CoberturaMethodCoverage MakeCoverage(
        string methodName, double coverage,
        string className = "MyApp.Service") =>
        new()
        {
            ClassName = className,
            MethodName = methodName,
            Signature = "()",
            Coverage = coverage
        };

    [Fact]
    public void FullPipeline_ProducesCorrectResults()
    {
        var complexity = new[]
        {
            MakeComplexity("Simple", 1),
            MakeComplexity("Complex", 20)
        };
        var coverage = new[]
        {
            MakeCoverage("Simple", 1.0),
            MakeCoverage("Complex", 0.1)
        };

        var result = CrapAnalyzer.AnalyzeFromResults(
            complexity, coverage, new CrapOptions { Threshold = 30 }, "TestProject");

        result.Data.Project.Should().Be("TestProject");
        result.Data.Methods.Should().HaveCount(2);

        var simple = result.Data.Methods.First(m => m.Identity.MethodName == "Simple");
        // comp=1, cov=1.0 → 1^2*(1-1)^3 + 1 = 0 + 1 = 1.0
        simple.CrapScore.Should().Be(1.0);
        simple.IsCrappy.Should().BeFalse();

        var complex = result.Data.Methods.First(m => m.Identity.MethodName == "Complex");
        // comp=20, cov=0.1 → 20^2*(1-0.1)^3 + 20 = 400*0.729 + 20 = 291.6 + 20 = 311.6
        complex.CrapScore.Should().BeApproximately(311.6, 0.1);
        complex.IsCrappy.Should().BeTrue();
    }

    [Fact]
    public void Stats_ComputedCorrectly()
    {
        var complexity = new[]
        {
            MakeComplexity("A", 5),
            MakeComplexity("B", 10),
            MakeComplexity("C", 1)
        };
        var coverage = new[]
        {
            MakeCoverage("A", 0.5),
            MakeCoverage("B", 0.0),
            MakeCoverage("C", 1.0)
        };

        var result = CrapAnalyzer.AnalyzeFromResults(
            complexity, coverage, new CrapOptions { Threshold = 30 });

        result.Data.Stats.MethodCount.Should().Be(3);
        result.Data.Stats.TotalCrap.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Histogram_Generated()
    {
        var complexity = new[] { MakeComplexity("A", 5) };
        var coverage = new[] { MakeCoverage("A", 0.5) };

        var result = CrapAnalyzer.AnalyzeFromResults(
            complexity, coverage, new CrapOptions { Threshold = 30 });

        result.Data.Histogram.Should().NotBeEmpty();
    }

    [Fact]
    public void Hierarchy_Generated()
    {
        var complexity = new[] { MakeComplexity("A", 5) };
        var coverage = new[] { MakeCoverage("A", 0.5) };

        var result = CrapAnalyzer.AnalyzeFromResults(
            complexity, coverage, new CrapOptions { Threshold = 30 });

        result.Data.Namespaces.Should().NotBeEmpty();
        result.Data.Namespaces[0].Name.Should().Be("MyApp");
    }

    [Fact]
    public void Warnings_PropagatedFromMatcher()
    {
        var complexity = new[] { MakeComplexity("A", 5) };
        // No coverage → should get UNMATCHED_METHODS warning
        var result = CrapAnalyzer.AnalyzeFromResults(
            complexity,
            Array.Empty<CoberturaMethodCoverage>(),
            new CrapOptions { Threshold = 30 });

        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
    }

    [Fact]
    public void EmptyInput_ProducesEmptyResult()
    {
        var result = CrapAnalyzer.AnalyzeFromResults(
            Array.Empty<MethodComplexityResult>(),
            Array.Empty<CoberturaMethodCoverage>(),
            new CrapOptions { Threshold = 30 });

        result.Data.Methods.Should().BeEmpty();
        result.Data.Stats.MethodCount.Should().Be(0);
    }
}
