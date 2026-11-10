using Crap4DotNet.Core.Coverage;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Coverage;

public sealed class CoverageMergerTests
{
    private static CoberturaMethodCoverage Entry(
        double coverage,
        string methodName = "Run",
        params CoberturaLine[] lines) =>
        new()
        {
            ClassName = "MyApp.Service",
            MethodName = methodName,
            Signature = "()",
            FileName = "Service.cs",
            Coverage = coverage,
            Lines = lines
        };

    private static CoberturaLine Line(int number, bool hit, int branchesCovered = 0, int branchesTotal = 0) =>
        new(number, hit, branchesCovered, branchesTotal);

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
    public void BranchingMethod_UsesTheHighestBranchCountPerLine()
    {
        // The files give branch counts, not which branches ran, so 1/2 and 1/2 can
        // be the same branch. The merge counts 1 of 2 on that line.
        var merged = CoverageMerger.Merge(
        [
            Entry(0.5, lines: [Line(1, true, 1, 2), Line(2, true, 0, 2)]),
            Entry(0.5, lines: [Line(1, true, 1, 2), Line(2, true, 2, 2)])
        ]);

        merged.Single().Coverage.Should().Be(0.75);
    }

    [Fact]
    public void EntriesWithoutLineData_TakeTheHighestRate()
    {
        var merged = CoverageMerger.Merge([Entry(0.0), Entry(0.7)]);

        merged.Single().Coverage.Should().Be(0.7);
    }
}
