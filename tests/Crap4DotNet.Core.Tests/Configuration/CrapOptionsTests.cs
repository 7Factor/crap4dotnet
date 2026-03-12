using Crap4DotNet.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Configuration;

public sealed class CrapOptionsTests
{
    [Fact]
    public void DefaultThreshold_Is30()
    {
        var options = new CrapOptions();

        options.Threshold.Should().Be(30);
    }

    [Fact]
    public void Threshold_CanBeCustomized()
    {
        var options = new CrapOptions { Threshold = 15 };

        options.Threshold.Should().Be(15);
    }

    [Fact]
    public void Threshold_Zero_Throws()
    {
        var act = () => new CrapOptions { Threshold = 0 };

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Threshold");
    }

    [Fact]
    public void Threshold_Negative_Throws()
    {
        var act = () => new CrapOptions { Threshold = -5 };

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Threshold");
    }

    [Fact]
    public void Threshold_One_IsValid()
    {
        var options = new CrapOptions { Threshold = 1 };

        options.Threshold.Should().Be(1);
    }
}
