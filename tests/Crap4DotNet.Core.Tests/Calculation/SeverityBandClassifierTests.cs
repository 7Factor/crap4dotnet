using Crap4DotNet.Core.Calculation;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Calculation;

public sealed class SeverityBandClassifierTests
{
    // Spec 3.3.1 — severity band test scenarios
    [Theory]
    [InlineData(1.0, SeverityBand.Low)]
    [InlineData(5.0, SeverityBand.Low)]
    [InlineData(5.01, SeverityBand.Moderate)]
    [InlineData(10.0, SeverityBand.Moderate)]
    [InlineData(15.0, SeverityBand.Moderate)]
    [InlineData(15.01, SeverityBand.Elevated)]
    [InlineData(30.0, SeverityBand.Elevated)]
    [InlineData(30.01, SeverityBand.High)]
    [InlineData(45.0, SeverityBand.High)]
    [InlineData(60.0, SeverityBand.High)]
    [InlineData(60.01, SeverityBand.Critical)]
    [InlineData(930.0, SeverityBand.Critical)]
    public void Classify_SpecTestScenarios(double crapScore, SeverityBand expected)
    {
        SeverityBandClassifier.Classify(crapScore).Should().Be(expected);
    }

    [Fact]
    public void Classify_Zero_ReturnsLow()
    {
        SeverityBandClassifier.Classify(0.0).Should().Be(SeverityBand.Low);
    }
}
