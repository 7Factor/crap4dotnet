using Crap4DotNet.Core.Calculation;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Calculation;

public sealed class CrapCalculatorTests
{
    // Spec table 2.2 — known values
    [Theory]
    [InlineData(1, 1.0, 1.0)]        // Simplest, fully tested
    [InlineData(1, 0.0, 2.0)]        // Simple, no tests
    [InlineData(10, 1.0, 10.0)]      // Moderate, fully tested
    [InlineData(10, 0.0, 110.0)]     // Moderate, no tests
    [InlineData(30, 1.0, 30.0)]      // Complex, fully tested
    [InlineData(30, 0.5, 142.5)]     // Complex, half tested
    [InlineData(30, 0.0, 930.0)]     // Complex, no tests
    public void Calculate_SpecTableValues(int complexity, double coverage, double expected)
    {
        CrapCalculator.Calculate(complexity, coverage).Should().Be(expected);
    }

    [Fact]
    public void Calculate_Complexity0_ReturnsZero()
    {
        // Spec 2.4.2: complexity=0 reaches calculator → CRAP=0
        CrapCalculator.Calculate(0, 0.5).Should().Be(0.0);
    }

    [Fact]
    public void Calculate_FullCoverage_EqualsCyclomaticComplexity()
    {
        // Spec 2.3: at 100% coverage, CRAP = comp (the (1-cov)^3 term vanishes)
        CrapCalculator.Calculate(15, 1.0).Should().Be(15.0);
    }

    [Fact]
    public void Calculate_CoverageClampedAbove1()
    {
        // Spec 2.4.1: coverage > 1.0 clamped to 1.0
        CrapCalculator.Calculate(10, 1.5).Should().Be(10.0);
    }

    [Fact]
    public void Calculate_CoverageClampedBelow0()
    {
        // Spec 2.4.1: coverage < 0.0 clamped to 0.0
        CrapCalculator.Calculate(10, -0.5).Should().Be(110.0);
    }

    [Fact]
    public void Calculate_ExtremelyHighComplexity()
    {
        // Spec 2.4.4: complexity=100, cov=0 → 100^2 * 1 + 100 = 10100
        CrapCalculator.Calculate(100, 0.0).Should().Be(10100.0);
    }

    [Fact]
    public void Calculate_PartialCoverage_ProducesExpectedValue()
    {
        // comp=5, cov=0.8: 25 * (0.2)^3 + 5 = 25 * 0.008 + 5 = 0.2 + 5 = 5.2
        CrapCalculator.Calculate(5, 0.8).Should().BeApproximately(5.2, 1e-10);
    }
}
