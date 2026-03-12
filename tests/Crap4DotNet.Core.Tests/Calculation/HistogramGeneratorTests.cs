using Crap4DotNet.Core.Calculation;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;
using static Crap4DotNet.Core.Tests.TestHelpers;

namespace Crap4DotNet.Core.Tests.Calculation;

public sealed class HistogramGeneratorTests
{
    [Fact]
    public void Generate_EmptyMethods_AllZeros()
    {
        var bins = HistogramGenerator.Generate([]);

        bins.Should().HaveCount(11);
        bins.Should().AllSatisfy(b =>
        {
            b.Count.Should().Be(0);
            b.Percent.Should().Be(0.0);
        });
    }

    [Fact]
    public void Generate_AllBinRangesPresent()
    {
        var bins = HistogramGenerator.Generate([]);
        var ranges = bins.Select(b => b.Range).ToList();

        ranges.Should().Equal(
            "0-5", "5-10", "10-15", "15-20", "20-25",
            "25-30", "30-40", "40-50", "50-75", "75-100", "100+");
    }

    [Fact]
    public void Generate_ScoreExactly5_FallsIn5To10Bin()
    {
        // Spec 3.4: half-open intervals. Score of exactly 5.0 falls in "5-10"
        var methods = new[] { MakeMethod(crapScore: 5.0) };
        var bins = HistogramGenerator.Generate(methods);

        bins.First(b => b.Range == "0-5").Count.Should().Be(0);
        bins.First(b => b.Range == "5-10").Count.Should().Be(1);
    }

    [Fact]
    public void Generate_ScoreJustBelow5_FallsIn0To5Bin()
    {
        var methods = new[] { MakeMethod(crapScore: 4.99) };
        var bins = HistogramGenerator.Generate(methods);

        bins.First(b => b.Range == "0-5").Count.Should().Be(1);
        bins.First(b => b.Range == "5-10").Count.Should().Be(0);
    }

    [Fact]
    public void Generate_Score100Plus()
    {
        var methods = new[] { MakeMethod(crapScore: 930.0) };
        var bins = HistogramGenerator.Generate(methods);

        bins.First(b => b.Range == "100+").Count.Should().Be(1);
        bins.First(b => b.Range == "100+").Percent.Should().Be(100.0);
    }

    [Fact]
    public void Generate_PercentsAddTo100()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 2.0, fullName: "A.B.M1()"),
            MakeMethod(crapScore: 10.0, fullName: "A.B.M2()"),
            MakeMethod(crapScore: 35.0, fullName: "A.B.M3()"),
            MakeMethod(crapScore: 110.0, fullName: "A.B.M4()")
        };
        var bins = HistogramGenerator.Generate(methods);

        bins.Sum(b => b.Percent).Should().BeApproximately(100.0, 1e-10);
    }

    [Fact]
    public void Generate_KnownDistribution()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 1.0, fullName: "A.B.M1()"),
            MakeMethod(crapScore: 3.0, fullName: "A.B.M2()"),
            MakeMethod(crapScore: 7.0, fullName: "A.B.M3()"),
            MakeMethod(crapScore: 50.0, fullName: "A.B.M4()")
        };
        var bins = HistogramGenerator.Generate(methods);

        bins.First(b => b.Range == "0-5").Count.Should().Be(2);
        bins.First(b => b.Range == "5-10").Count.Should().Be(1);
        bins.First(b => b.Range == "50-75").Count.Should().Be(1);
    }
}
