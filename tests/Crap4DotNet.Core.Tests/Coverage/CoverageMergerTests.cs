using Crap4DotNet.Core.Coverage;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Coverage;

public sealed class CoverageMergerTests
{
    private static CoberturaMethodCoverage Entry(
        double coverage,
        string methodName = "Run",
        string fileName = "/repo/src/Service.cs",
        int? startLine = null,
        IReadOnlyList<CoberturaLine>? lines = null) =>
        new()
        {
            ClassName = "MyApp.Service",
            MethodName = methodName,
            Signature = "()",
            FileName = fileName,
            StartLine = startLine,
            Coverage = coverage,
            Lines = lines
        };

    private static CoberturaLine Line(int number, bool hit, int branchesCovered = 0, int branchesTotal = 0) =>
        new() { Number = number, Hit = hit, BranchesCovered = branchesCovered, BranchesTotal = branchesTotal };

    [Fact]
    public void SingleEntry_IsReturnedUnchanged()
    {
        var entry = Entry(0.5, lines: [Line(1, true), Line(2, false)]);

        CoverageMerger.Merge([entry]).Should().Equal(entry);
    }

    [Fact]
    public void DifferentMethods_AreNotMerged()
    {
        var merged = CoverageMerger.Merge([Entry(0.5, "Run"), Entry(0.2, "Stop")]);

        merged.Select(e => e.Coverage).Should().Equal(0.5, 0.2);
    }

    [Fact]
    public void SameMethodNameInDifferentFiles_IsNotMerged()
    {
        var merged = CoverageMerger.Merge(
        [
            Entry(1.0, fileName: "/repo/App1/Program.cs", lines: [Line(1, true)]),
            Entry(0.0, fileName: "/repo/App2/Program.cs", lines: [Line(1, false)])
        ]);

        merged.Select(e => e.Coverage).Should().Equal(1.0, 0.0);
    }

    [Fact]
    public void FileNamesThatDifferOnlyInCase_AreMerged()
    {
        var merged = CoverageMerger.Merge(
        [
            Entry(0.0, fileName: "C:/Repo/Service.cs", lines: [Line(1, false)]),
            Entry(1.0, fileName: "c:/repo/service.cs", lines: [Line(1, true)])
        ]);

        merged.Single().Coverage.Should().Be(1.0);
    }

    [Fact]
    public void LinesCoveredInDifferentFiles_AreCombined()
    {
        var merged = CoverageMerger.Merge(
        [
            Entry(0.5, lines: [Line(1, true), Line(2, false)]),
            Entry(0.5, lines: [Line(1, false), Line(2, true)])
        ]);

        merged.Single().Coverage.Should().Be(1.0);
    }

    [Fact]
    public void FileOrder_DoesNotChangeTheResult()
    {
        var covered = Entry(1.0, lines: [Line(1, true)]);
        var uncovered = Entry(0.0, lines: [Line(1, false)]);

        CoverageMerger.Merge([uncovered, covered]).Single().Coverage.Should().Be(1.0);
        CoverageMerger.Merge([covered, uncovered]).Single().Coverage.Should().Be(1.0);
    }

    [Fact]
    public void BranchingMethod_DoesNotAddBranchCountsAcrossFiles()
    {
        // Two reports of 1/2 can be the same branch, so they must not add up to 2/2.
        var merged = CoverageMerger.Merge(
        [
            Entry(0.5, lines: [Line(1, true, 1, 2), Line(2, true, 0, 2)]),
            Entry(0.5, lines: [Line(1, true, 1, 2), Line(2, true, 2, 2)])
        ]);

        merged.Single().Coverage.Should().Be(0.75);
    }

    [Fact]
    public void DifferentBranchTotals_KeepTheMostCoveredReportedPair()
    {
        var merged = CoverageMerger.Merge(
        [
            Entry(0.5, lines: [Line(1, true, 1, 2)]),
            Entry(0.0, lines: [Line(1, true, 0, 4)])
        ]);

        merged.Single().Lines.Should().Equal(Line(1, true, 1, 2));
        merged.Single().Coverage.Should().Be(0.5);
    }

    [Fact]
    public void MergedEntry_StartsAtItsFirstMergedLine()
    {
        var merged = CoverageMerger.Merge(
        [
            Entry(1.0, startLine: 12, lines: [Line(12, true)]),
            Entry(1.0, startLine: 10, lines: [Line(10, true), Line(12, true)])
        ]);

        merged.Single().StartLine.Should().Be(10);
    }

    [Fact]
    public void EntriesWithoutLineData_TakeTheHighestRate()
    {
        var merged = CoverageMerger.Merge([Entry(0.0), Entry(0.7, lines: [Line(1, true)])]);

        merged.Single().Coverage.Should().Be(0.7);
        merged.Single().Lines.Should().BeNull();
    }

    [Fact]
    public void EntriesWithNoLines_TakeTheHighestRate()
    {
        var merged = CoverageMerger.Merge([Entry(0.0, lines: []), Entry(1.0, lines: [])]);

        merged.Single().Coverage.Should().Be(1.0);
    }
}
