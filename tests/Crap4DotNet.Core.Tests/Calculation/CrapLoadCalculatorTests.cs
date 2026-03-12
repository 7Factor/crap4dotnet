using Crap4DotNet.Core.Calculation;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Calculation;

public sealed class CrapLoadCalculatorTests
{
    [Fact]
    public void Calculate_BelowThreshold_ReturnsZero()
    {
        // comp=1, cov=1.0, CRAP=1 which is <= 30
        CrapLoadCalculator.Calculate(1, 1.0, 30).Should().Be(0.0);
    }

    [Fact]
    public void Calculate_ExactlyAtThreshold_ReturnsZero()
    {
        // Spec 3.1: strict > comparison. CRAP=30 at comp=30,cov=1.0 is NOT CRAPpy
        CrapLoadCalculator.Calculate(30, 1.0, 30).Should().Be(0.0);
    }

    [Fact]
    public void Calculate_AboveThreshold_ReturnsLoad()
    {
        // comp=30, cov=0.0, threshold=30: CRAP=930 > 30
        // crapLoad = 30 * (1-0) + 30/30 = 30 + 1 = 31
        CrapLoadCalculator.Calculate(30, 0.0, 30).Should().Be(31.0);
    }

    [Fact]
    public void Calculate_PartialCoverage_AboveThreshold()
    {
        // comp=30, cov=0.5, threshold=30: CRAP=142.5 > 30
        // crapLoad = 30 * 0.5 + 30/30 = 15 + 1 = 16
        CrapLoadCalculator.Calculate(30, 0.5, 30).Should().Be(16.0);
    }

    [Fact]
    public void Calculate_SpecExample_Comp60Cov0()
    {
        // Spec 4.3: comp=60, threshold=30, cov=0.0
        // crapLoad = 60 * 1.0 + 60/30 = 60 + 2 = 62
        CrapLoadCalculator.Calculate(60, 0.0, 30).Should().Be(62.0);
    }

    [Fact]
    public void Calculate_SpecExample_Comp60Cov05()
    {
        // Spec 4.3: comp=60, threshold=30, cov=0.5
        // crapLoad = 60 * 0.5 + 60/30 = 30 + 2 = 32
        CrapLoadCalculator.Calculate(60, 0.5, 30).Should().Be(32.0);
    }

    [Fact]
    public void Calculate_CustomThreshold()
    {
        // comp=10, cov=0.0, threshold=5: CRAP=110 > 5
        // crapLoad = 10 * 1.0 + 10/5 = 10 + 2 = 12
        CrapLoadCalculator.Calculate(10, 0.0, 5).Should().Be(12.0);
    }

    [Fact]
    public void Calculate_ThresholdZero_Throws()
    {
        var act = () => CrapLoadCalculator.Calculate(10, 0.5, 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("threshold");
    }

    [Fact]
    public void Calculate_ThresholdNegative_Throws()
    {
        var act = () => CrapLoadCalculator.Calculate(10, 0.5, -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("threshold");
    }

    [Fact]
    public void Calculate_Complexity0_ReturnsZero()
    {
        // comp=0 → CRAP=0, which is <= any positive threshold
        CrapLoadCalculator.Calculate(0, 0.0, 30).Should().Be(0.0);
    }
}
