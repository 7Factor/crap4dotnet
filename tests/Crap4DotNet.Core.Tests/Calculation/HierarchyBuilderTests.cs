using Crap4DotNet.Core.Calculation;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;
using static Crap4DotNet.Core.Tests.TestHelpers;

namespace Crap4DotNet.Core.Tests.Calculation;

public sealed class HierarchyBuilderTests
{
    [Fact]
    public void Build_EmptyMethods_ReturnsEmptyList()
    {
        HierarchyBuilder.Build([]).Should().BeEmpty();
    }

    [Fact]
    public void Build_SingleNamespace_SingleClass()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 5.0, ns: "MyApp", className: "Service", fullName: "MyApp.Service.A()"),
            MakeMethod(crapScore: 10.0, ns: "MyApp", className: "Service", fullName: "MyApp.Service.B()")
        };
        var hierarchy = HierarchyBuilder.Build(methods);

        hierarchy.Should().HaveCount(1);
        hierarchy[0].Name.Should().Be("MyApp");
        hierarchy[0].Stats.MethodCount.Should().Be(2);
        hierarchy[0].Classes.Should().HaveCount(1);
        hierarchy[0].Classes[0].Name.Should().Be("Service");
        hierarchy[0].Classes[0].Stats.MethodCount.Should().Be(2);
        hierarchy[0].Classes[0].Methods.Should().HaveCount(2);
    }

    [Fact]
    public void Build_MultipleNamespaces()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 5.0, ns: "MyApp.A", className: "Svc1", fullName: "MyApp.A.Svc1.Do()"),
            MakeMethod(crapScore: 10.0, ns: "MyApp.B", className: "Svc2", fullName: "MyApp.B.Svc2.Do()")
        };
        var hierarchy = HierarchyBuilder.Build(methods);

        hierarchy.Should().HaveCount(2);
        hierarchy.Select(n => n.Name).Should().Contain("MyApp.A").And.Contain("MyApp.B");
    }

    [Fact]
    public void Build_MultipleClassesInSameNamespace()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 5.0, ns: "MyApp", className: "Alpha", fullName: "MyApp.Alpha.Go()"),
            MakeMethod(crapScore: 10.0, ns: "MyApp", className: "Beta", fullName: "MyApp.Beta.Go()")
        };
        var hierarchy = HierarchyBuilder.Build(methods);

        hierarchy.Should().HaveCount(1);
        hierarchy[0].Classes.Should().HaveCount(2);
        hierarchy[0].Classes.Select(c => c.Name).Should().Contain("Alpha").And.Contain("Beta");
    }

    [Fact]
    public void Build_StatsComputedPerLevel()
    {
        var methods = new[]
        {
            MakeMethod(crapScore: 2.0, ns: "MyApp", className: "Svc", isCrappy: false, fullName: "MyApp.Svc.A()"),
            MakeMethod(crapScore: 110.0, ns: "MyApp", className: "Svc", isCrappy: true, crapLoad: 31, fullName: "MyApp.Svc.B()")
        };
        var hierarchy = HierarchyBuilder.Build(methods);
        var classStats = hierarchy[0].Classes[0].Stats;

        classStats.MethodCount.Should().Be(2);
        classStats.TotalCrap.Should().Be(112.0);
        classStats.CrappyMethodCount.Should().Be(1);
        classStats.TotalCrapLoad.Should().Be(31);
    }
}
