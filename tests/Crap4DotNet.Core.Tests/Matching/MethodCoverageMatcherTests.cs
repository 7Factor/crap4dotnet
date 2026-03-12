using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Matching;

public sealed class MethodCoverageMatcherTests
{
    private static MethodComplexityResult MakeComplexity(
        string methodName,
        string className = "Service",
        string ns = "MyApp",
        string signature = "()",
        int complexity = 5) =>
        new()
        {
            Identity = new MethodIdentity
            {
                Namespace = ns,
                ClassName = className,
                MethodName = methodName,
                Signature = signature,
                FullName = $"{ns}.{className}.{methodName}{signature}",
                FilePath = "Test.cs",
                LineNumber = 1
            },
            Complexity = complexity
        };

    private static CoberturaMethodCoverage MakeCoverage(
        string methodName,
        string className = "MyApp.Service",
        string signature = "()",
        double coverage = 0.8) =>
        new()
        {
            ClassName = className,
            MethodName = methodName,
            Signature = signature,
            Coverage = coverage
        };

    // === Join behavior per spec 6.4.3 ===

    [Fact]
    public void PerfectMatch_AllMethodsHaveCoverage()
    {
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B"),
            MakeComplexity("C")
        };
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.9),
            MakeCoverage("B", coverage: 0.7),
            MakeCoverage("C", coverage: 0.5)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().HaveCount(3);
        result.Methods.Select(m => m.Coverage).Should().Equal(0.9, 0.7, 0.5);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void PartialCoverage_UnmatchedMethodsGetZero()
    {
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B"),
            MakeComplexity("C")
        };
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.9),
            MakeCoverage("B", coverage: 0.7)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().HaveCount(3);
        var methodC = result.Methods.First(m => m.Complexity.Identity.MethodName == "C");
        methodC.Coverage.Should().Be(0.0);
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
    }

    [Fact]
    public void NoCoverageEntries_AllGetZero()
    {
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B"),
            MakeComplexity("C")
        };

        var result = MethodCoverageMatcher.Match(complexity, Array.Empty<CoberturaMethodCoverage>());

        result.Methods.Should().HaveCount(3);
        result.Methods.Should().AllSatisfy(m => m.Coverage.Should().Be(0.0));
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
    }

    [Fact]
    public void ExtraCoverageEntries_Ignored()
    {
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B")
        };
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.9),
            MakeCoverage("B", coverage: 0.7),
            MakeCoverage("C", coverage: 0.5),
            MakeCoverage("D", coverage: 0.3)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().HaveCount(2);
        result.Warnings.Should().Contain(w => w.Code == "ORPHANED_COVERAGE");
    }

    [Fact]
    public void CompleteMismatch_AllZeroAndStaleWarning()
    {
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B")
        };
        var coverage = new[]
        {
            MakeCoverage("X", coverage: 0.9),
            MakeCoverage("Y", coverage: 0.7)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().HaveCount(2);
        result.Methods.Should().AllSatisfy(m => m.Coverage.Should().Be(0.0));
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
        result.Warnings.Should().Contain(w => w.Code == "ORPHANED_COVERAGE");
        result.Warnings.Should().Contain(w => w.Code == "COVERAGE_STALE");
    }

    [Fact]
    public void EmptyComplexity_NoResults()
    {
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.9),
            MakeCoverage("B", coverage: 0.7)
        };

        var result = MethodCoverageMatcher.Match(
            Array.Empty<MethodComplexityResult>(), coverage);

        result.Methods.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Code == "ORPHANED_COVERAGE");
    }

    // === Warning thresholds ===

    [Fact]
    public void StaleWarning_WhenOver20PercentOrphaned()
    {
        // 5 coverage entries, 2 matched, 3 orphaned = 60% > 20%
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B")
        };
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.9),
            MakeCoverage("B", coverage: 0.7),
            MakeCoverage("X", coverage: 0.5),
            MakeCoverage("Y", coverage: 0.3),
            MakeCoverage("Z", coverage: 0.1)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Warnings.Should().Contain(w => w.Code == "COVERAGE_STALE");
    }

    [Fact]
    public void NoStaleWarning_When20PercentOrLess()
    {
        // 5 coverage entries, 4 matched, 1 orphaned = 20% (not >20%)
        var complexity = new[]
        {
            MakeComplexity("A"),
            MakeComplexity("B"),
            MakeComplexity("C"),
            MakeComplexity("D")
        };
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.9),
            MakeCoverage("B", coverage: 0.7),
            MakeCoverage("C", coverage: 0.5),
            MakeCoverage("D", coverage: 0.3),
            MakeCoverage("X", coverage: 0.1) // 1 orphaned = 20%
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Warnings.Should().NotContain(w => w.Code == "COVERAGE_STALE");
    }

    // === CLR type matching ===

    [Fact]
    public void Match_ClrPrimitiveSignatures()
    {
        var complexity = new[]
        {
            MakeComplexity("Process", signature: "(string, int)")
        };
        var coverage = new[]
        {
            MakeCoverage("Process", signature: "(System.String, System.Int32)", coverage: 0.75)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().ContainSingle()
            .Which.Coverage.Should().Be(0.75);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Match_Constructor()
    {
        var complexity = new[]
        {
            MakeComplexity("Service", signature: "(string)")
        };
        var coverage = new[]
        {
            MakeCoverage(".ctor", signature: "(System.String)", coverage: 0.85)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().ContainSingle()
            .Which.Coverage.Should().Be(0.85);
    }

    [Fact]
    public void Match_GenericClass()
    {
        var complexity = new[]
        {
            new MethodComplexityResult
            {
                Identity = new MethodIdentity
                {
                    Namespace = "MyApp",
                    ClassName = "Cache<T>",
                    MethodName = "Get",
                    Signature = "(string)",
                    FullName = "MyApp.Cache<T>.Get(string)",
                    FilePath = "Test.cs",
                    LineNumber = 1
                },
                Complexity = 3
            }
        };
        var coverage = new[]
        {
            MakeCoverage("Get", className: "MyApp.Cache`1",
                signature: "(System.String)", coverage: 0.6)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().ContainSingle()
            .Which.Coverage.Should().Be(0.6);
    }

    [Fact]
    public void Match_NestedType()
    {
        var complexity = new[]
        {
            new MethodComplexityResult
            {
                Identity = new MethodIdentity
                {
                    Namespace = "MyApp",
                    ClassName = "Outer.Inner",
                    MethodName = "Run",
                    Signature = "()",
                    FullName = "MyApp.Outer.Inner.Run()",
                    FilePath = "Test.cs",
                    LineNumber = 1
                },
                Complexity = 1
            }
        };
        var coverage = new[]
        {
            MakeCoverage("Run", className: "MyApp.Outer/Inner",
                signature: "()", coverage: 1.0)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().ContainSingle()
            .Which.Coverage.Should().Be(1.0);
    }

    // === Fallback matching ===

    [Fact]
    public void FallbackMatch_SignatureMismatch_SingleCandidate()
    {
        // Roslyn uses "out string" but CLR uses "System.String&" → "ref string"
        // Exact key won't match, but name-only fallback finds single candidate
        var complexity = new[]
        {
            MakeComplexity("TryGet", signature: "(string, out string)")
        };
        var coverage = new[]
        {
            MakeCoverage("TryGet", signature: "(System.String, System.String&)", coverage: 0.7)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Should().ContainSingle()
            .Which.Coverage.Should().Be(0.7);
    }

    [Fact]
    public void FallbackMatch_OverloadedMethods_NoFallback()
    {
        // Two overloads with incompatible signatures → fallback finds multiple candidates → no match
        var complexity = new[]
        {
            MakeComplexity("Process", signature: "(out int)")
        };
        var coverage = new[]
        {
            MakeCoverage("Process", signature: "(System.Int32&)", coverage: 0.7),
            MakeCoverage("Process", signature: "(System.String)", coverage: 0.5)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        // Fallback finds 2 candidates for "Process" → ambiguous → defaults to 0.0
        result.Methods.Should().ContainSingle()
            .Which.Coverage.Should().Be(0.0);
    }

    // === Preserves order ===

    [Fact]
    public void ResultsPreserveComplexityOrder()
    {
        var complexity = new[]
        {
            MakeComplexity("C"),
            MakeComplexity("A"),
            MakeComplexity("B")
        };
        var coverage = new[]
        {
            MakeCoverage("A", coverage: 0.1),
            MakeCoverage("B", coverage: 0.2),
            MakeCoverage("C", coverage: 0.3)
        };

        var result = MethodCoverageMatcher.Match(complexity, coverage);

        result.Methods.Select(m => m.Complexity.Identity.MethodName)
            .Should().Equal("C", "A", "B");
        result.Methods.Select(m => m.Coverage)
            .Should().Equal(0.3, 0.1, 0.2);
    }
}
