using Crap4DotNet.Core.Calculation;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;
using static Crap4DotNet.Core.Tests.TestHelpers;

namespace Crap4DotNet.Core.Tests.Calculation;

public sealed class CrapStatisticsCalculatorTests
{
    [Fact]
    public void Calculate_EmptyMethods_ReturnsNullAverages()
    {
        // Spec 2.4.5: empty set produces null for averages
        var stats = CrapStatisticsCalculator.Calculate([]);

        stats.MethodCount.Should().Be(0);
        stats.TotalCrap.Should().Be(0);
        stats.AverageCrap.Should().BeNull();
        stats.MedianCrap.Should().BeNull();
        stats.StandardDeviation.Should().BeNull();
        stats.CrappyMethodCount.Should().Be(0);
        stats.CrappyMethodPercent.Should().Be(0.0);
        stats.TotalCrapLoad.Should().Be(0);
    }

    [Fact]
    public void Calculate_SingleMethod()
    {
        var methods = new[] { MakeMethod(crapScore: 10.0, crapLoad: 0, isCrappy: false) };
        var stats = CrapStatisticsCalculator.Calculate(methods);

        stats.MethodCount.Should().Be(1);
        stats.TotalCrap.Should().Be(10.0);
        stats.AverageCrap.Should().Be(10.0);
        stats.MedianCrap.Should().Be(10.0);
        stats.StandardDeviation.Should().Be(0.0);
        stats.CrappyMethodCount.Should().Be(0);
        stats.CrappyMethodPercent.Should().Be(0.0);
        stats.TotalCrapLoad.Should().Be(0);
    }

    [Fact]
    public void Calculate_MultipleMethods_CorrectAggregation()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 2.0, crapLoad: 0, isCrappy: false, fullName: "A.B.M1()"),
            MakeMethod(crapScore: 10.0, crapLoad: 0, isCrappy: false, fullName: "A.B.M2()"),
            MakeMethod(crapScore: 110.0, crapLoad: 31, isCrappy: true, fullName: "A.B.M3()")
        };
        var stats = CrapStatisticsCalculator.Calculate(methods);

        stats.MethodCount.Should().Be(3);
        stats.TotalCrap.Should().Be(122.0);
        stats.AverageCrap.Should().BeApproximately(122.0 / 3.0, 1e-10);
        stats.MedianCrap.Should().Be(10.0); // sorted: 2, 10, 110 → middle is 10
        stats.CrappyMethodCount.Should().Be(1);
        stats.CrappyMethodPercent.Should().BeApproximately(100.0 / 3.0, 1e-10);
        stats.TotalCrapLoad.Should().Be(31);
    }

    [Fact]
    public void Calculate_EvenCount_MedianIsAverageOfMiddleTwo()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 2.0, fullName: "A.B.M1()"),
            MakeMethod(crapScore: 4.0, fullName: "A.B.M2()"),
            MakeMethod(crapScore: 6.0, fullName: "A.B.M3()"),
            MakeMethod(crapScore: 8.0, fullName: "A.B.M4()")
        };
        var stats = CrapStatisticsCalculator.Calculate(methods);

        // sorted: 2, 4, 6, 8 → median = (4+6)/2 = 5
        stats.MedianCrap.Should().Be(5.0);
    }

    [Fact]
    public void Calculate_StandardDeviation_PopulationFormula()
    {
        // scores: 2, 4, 4, 4, 5, 5, 7, 9 → mean=5, pop stddev=2
        var methods = new[]
        {
            MakeMethod(crapScore: 2.0, fullName: "A.B.M1()"),
            MakeMethod(crapScore: 4.0, fullName: "A.B.M2()"),
            MakeMethod(crapScore: 4.0, fullName: "A.B.M3()"),
            MakeMethod(crapScore: 4.0, fullName: "A.B.M4()"),
            MakeMethod(crapScore: 5.0, fullName: "A.B.M5()"),
            MakeMethod(crapScore: 5.0, fullName: "A.B.M6()"),
            MakeMethod(crapScore: 7.0, fullName: "A.B.M7()"),
            MakeMethod(crapScore: 9.0, fullName: "A.B.M8()")
        };
        var stats = CrapStatisticsCalculator.Calculate(methods);

        stats.AverageCrap.Should().Be(5.0);
        stats.StandardDeviation.Should().Be(2.0);
    }
}
