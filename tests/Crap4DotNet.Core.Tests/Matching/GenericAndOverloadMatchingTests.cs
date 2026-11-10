using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Matching;

public sealed class GenericAndOverloadMatchingTests
{
    private static MethodComplexityResult Source(string methodName, string signature, string className = "Helper",
        int line = 1, int? endLine = null) =>
        new()
        {
            Identity = new MethodIdentity
            {
                Namespace = "MyApp",
                ClassName = className,
                MethodName = methodName,
                Signature = signature,
                FullName = $"MyApp.{className}.{methodName}{signature}",
                FilePath = "Test.cs",
                LineNumber = line,
                EndLineNumber = endLine
            },
            Complexity = 10
        };

    private static CoberturaMethodCoverage Cover(string methodName, string signature, double coverage,
        string className = "MyApp.Helper", IReadOnlyList<CoberturaLine>? lines = null) =>
        new()
        {
            ClassName = className,
            MethodName = methodName,
            Signature = signature,
            Coverage = coverage,
            FileName = "Test.cs",
            Lines = lines
        };

    private static CoberturaLine Line(int number, bool hit, int branchesCovered = 0, int branchesTotal = 0) =>
        new() { Number = number, Hit = hit, BranchesCovered = branchesCovered, BranchesTotal = branchesTotal };

    [Fact]
    public void GenericMethod_WithByRefOutParameter_TakesItsCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("TryConvertToNumeric<T>", "(string, out T)")],
            [Cover("TryConvertToNumeric", "(System.String,T&)", 1.0)]);

        result.Methods.Should().HaveCount(1);
        result.Methods[0].Coverage.Should().Be(1.0);
    }

    [Fact]
    public void GenericMethod_NoParameters_TakesItsCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("GetObjectTypeName<T>", "()")],
            [Cover("GetObjectTypeName", "()", 0.75)]);

        result.Methods[0].Coverage.Should().Be(0.75);
    }

    [Fact]
    public void NullableReferenceAnnotation_DoesNotBlockTheMatch()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("BuildLink", "(string, string, int, string?)")],
            [Cover("BuildLink", "(System.String,System.String,System.Int32,System.String)", 0.5)]);

        result.Methods[0].Coverage.Should().Be(0.5);
    }

    [Fact]
    public void OverloadSet_PairsEachOverloadWithItsOwnCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [
                Source("BuildLink", "(string, string, int, string?)"),
                Source("BuildLink", "(EcsDtoBase, string, string?)")
            ],
            [
                Cover("BuildLink", "(System.String,System.String,System.Int32,System.String)", 0.25),
                Cover("BuildLink", "(MyApp.Dto.EcsDtoBase,System.String,System.String)", 0.75)
            ]);

        result.Methods.Should().HaveCount(2);
        result.Methods[0].Coverage.Should().Be(0.25);
        result.Methods[1].Coverage.Should().Be(0.75);
    }

    [Fact]
    public void NullableValueTypeOverloads_KeepTheirOwnCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int)"), Source("Foo", "(int?)")],
            [
                Cover("Foo", "(System.Int32)", 0.2),
                Cover("Foo", "(System.Nullable`1<System.Int32>)", 0.8)
            ]);

        result.Methods[0].Coverage.Should().Be(0.2);
        result.Methods[1].Coverage.Should().Be(0.8);
    }

    [Fact]
    public void NullableValueType_DoesNotTakeCoverageFromNonNullableOverload()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int?)"), Source("Foo", "(bool)")],
            [
                Cover("Foo", "(System.Int32)", 0.9),
                Cover("Foo", "(System.Boolean)", 0.2)
            ]);

        result.Methods[0].Coverage.Should().Be(0.0);
        result.Methods[1].Coverage.Should().Be(0.2);
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
        result.Warnings.Should().Contain(w => w.Code == "ORPHANED_COVERAGE");
    }

    [Fact]
    public void SharedEntry_SplitsCoverageByEachMethodsLines()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int)", line: 4, endLine: 4), Source("Foo<T>", "(int)", line: 5, endLine: 5)],
            [Cover("Foo", "(System.Int32)", 0.5, lines: [Line(4, hit: false, 0, 2), Line(5, hit: true, 2, 2)])]);

        result.Methods[0].Coverage.Should().Be(0.0);
        result.Methods[1].Coverage.Should().Be(1.0);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SharedEntry_MethodWithNoLinesInTheEntry_IsUnmatched()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int)", line: 4, endLine: 6), Source("Foo<T>", "(int)", line: 8, endLine: 10)],
            [Cover("Foo", "(System.Int32)", 0.5, lines: [Line(5, hit: true), Line(6, hit: false)])]);

        result.Methods[0].Coverage.Should().Be(0.5);
        result.Methods[1].Coverage.Should().Be(0.0);
        result.Warnings.Should().ContainSingle(w => w.Code == "UNMATCHED_METHODS");
    }

    [Fact]
    public void SharedEntry_WithoutLineData_WarnsThatItCannotBeSplit()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int)", line: 4, endLine: 4), Source("Foo<T>", "(int)", line: 5, endLine: 5)],
            [Cover("Foo", "(System.Int32)", 0.6)]);

        result.Methods[0].Coverage.Should().Be(0.6);
        result.Methods[1].Coverage.Should().Be(0.0);
        result.Warnings.Should().Contain(w => w.Code == "SHARED_COVERAGE" && w.Message.Contains("MyApp.Helper.Foo"));
    }

    [Fact]
    public void GenuinelyUncoveredMethod_StillReportsZero()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Untested<T>", "(int)")],
            [Cover("SomethingElse", "()", 1.0)]);

        result.Methods[0].Coverage.Should().Be(0.0);
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
    }
}
