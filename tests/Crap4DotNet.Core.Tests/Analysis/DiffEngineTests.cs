using System.Text.Json;
using Crap4DotNet.Core.Analysis;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Analysis;

public sealed class DiffEngineTests
{
    private static JsonDocument MakeReport(
        params (string fullName, double crap, int complexity, double coverage)[] methods)
    {
        var methodsJson = methods.Select(m => new
        {
            fullName = m.fullName,
            crap = m.crap,
            complexity = m.complexity,
            coverage = m.coverage
        });

        var report = new
        {
            schemaVersion = "1.0",
            project = "TestProject",
            timestamp = "2026-03-12T12:00:00Z",
            threshold = 30,
            stats = new
            {
                methodCount = methods.Length,
                crappyMethodCount = methods.Count(m => m.crap > 30),
                totalCrapLoad = 0.0
            },
            methods = methodsJson
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(report));
    }

    // === Spec 10.2.3 test scenarios ===

    [Fact]
    public void MethodAdded()
    {
        var before = MakeReport();
        var after = MakeReport(("MyApp.Service.New()", 5.0, 2, 0.8));

        var result = DiffEngine.Compare(before, after, 30);

        result.Added.Should().ContainSingle()
            .Which.Status.Should().Be("added");
        result.Summary.Added.Should().Be(1);
    }

    [Fact]
    public void MethodRemoved()
    {
        var before = MakeReport(("MyApp.Service.Old()", 85.0, 20, 0.1));
        var after = MakeReport();

        var result = DiffEngine.Compare(before, after, 30);

        result.Removed.Should().ContainSingle();
        result.Removed[0].Status.Should().Be("removed");
        result.Removed[0].WasCrappy.Should().BeTrue();
        result.Summary.Removed.Should().Be(1);
    }

    [Fact]
    public void MethodBecomesCrappy()
    {
        var before = MakeReport(("MyApp.Service.M()", 25.0, 8, 0.5));
        var after = MakeReport(("MyApp.Service.M()", 45.0, 12, 0.2));

        var result = DiffEngine.Compare(before, after, 30);

        result.NewCrappy.Should().ContainSingle()
            .Which.Status.Should().Be("new_crappy");
    }

    [Fact]
    public void MethodFixed()
    {
        var before = MakeReport(("MyApp.Service.M()", 45.0, 12, 0.2));
        var after = MakeReport(("MyApp.Service.M()", 12.0, 12, 0.95));

        var result = DiffEngine.Compare(before, after, 30);

        result.FixedCrappy.Should().ContainSingle()
            .Which.Status.Should().Be("fixed");
    }

    [Fact]
    public void WorseCrossedThreshold_IsNewCrappy_NotRegressed()
    {
        // P3 wins over P5
        var before = MakeReport(("MyApp.Service.M()", 25.0, 8, 0.5));
        var after = MakeReport(("MyApp.Service.M()", 35.0, 10, 0.3));

        var result = DiffEngine.Compare(before, after, 30);

        result.NewCrappy.Should().ContainSingle();
        result.Regressed.Should().BeEmpty();
    }

    [Fact]
    public void ImprovedCrossedThreshold_IsFixed_NotImproved()
    {
        // P4 wins over P6
        var before = MakeReport(("MyApp.Service.M()", 50.0, 15, 0.1));
        var after = MakeReport(("MyApp.Service.M()", 25.0, 15, 0.8));

        var result = DiffEngine.Compare(before, after, 30);

        result.FixedCrappy.Should().ContainSingle();
        result.Improved.Should().BeEmpty();
    }

    [Fact]
    public void WorsenedButStillClean_IsRegressed()
    {
        var before = MakeReport(("MyApp.Service.M()", 5.0, 2, 0.9));
        var after = MakeReport(("MyApp.Service.M()", 15.0, 5, 0.5));

        var result = DiffEngine.Compare(before, after, 30);

        result.Regressed.Should().ContainSingle();
        result.Regressed[0].Delta.Should().Be(10.0);
    }

    [Fact]
    public void ImprovedButStillCrappy_IsImproved()
    {
        var before = MakeReport(("MyApp.Service.M()", 80.0, 20, 0.1));
        var after = MakeReport(("MyApp.Service.M()", 50.0, 20, 0.4));

        var result = DiffEngine.Compare(before, after, 30);

        result.Improved.Should().ContainSingle();
        result.Improved[0].Delta.Should().Be(-30.0);
    }

    [Fact]
    public void Unchanged_Omitted()
    {
        var before = MakeReport(("MyApp.Service.M()", 10.0, 3, 0.8));
        var after = MakeReport(("MyApp.Service.M()", 10.0, 3, 0.8));

        var result = DiffEngine.Compare(before, after, 30);

        result.Added.Should().BeEmpty();
        result.Removed.Should().BeEmpty();
        result.NewCrappy.Should().BeEmpty();
        result.FixedCrappy.Should().BeEmpty();
        result.Regressed.Should().BeEmpty();
        result.Improved.Should().BeEmpty();
        result.Summary.Unchanged.Should().Be(1);
    }

    [Fact]
    public void EmptyBefore_AllAdded()
    {
        var before = MakeReport();
        var after = MakeReport(
            ("MyApp.Service.A()", 5.0, 2, 0.9),
            ("MyApp.Service.B()", 10.0, 3, 0.7));

        var result = DiffEngine.Compare(before, after, 30);

        result.Added.Should().HaveCount(2);
        result.Summary.Added.Should().Be(2);
    }

    [Fact]
    public void EmptyAfter_AllRemoved()
    {
        var before = MakeReport(
            ("MyApp.Service.A()", 5.0, 2, 0.9),
            ("MyApp.Service.B()", 10.0, 3, 0.7));
        var after = MakeReport();

        var result = DiffEngine.Compare(before, after, 30);

        result.Removed.Should().HaveCount(2);
        result.Summary.Removed.Should().Be(2);
    }

    // === Summary counts ===

    [Fact]
    public void Summary_CountsAllCategories()
    {
        var before = MakeReport(
            ("MyApp.A()", 25.0, 8, 0.5),   // will become crappy
            ("MyApp.B()", 45.0, 12, 0.2),  // will be fixed
            ("MyApp.C()", 5.0, 2, 0.9),    // will be unchanged
            ("MyApp.D()", 10.0, 3, 0.8),   // will be removed
            ("MyApp.E()", 5.0, 2, 0.9));   // will regress

        var after = MakeReport(
            ("MyApp.A()", 45.0, 12, 0.1),  // new_crappy
            ("MyApp.B()", 12.0, 12, 0.95), // fixed
            ("MyApp.C()", 5.0, 2, 0.9),    // unchanged
            ("MyApp.E()", 15.0, 5, 0.5),   // regressed
            ("MyApp.F()", 8.0, 3, 0.7));   // added

        var result = DiffEngine.Compare(before, after, 30);

        result.Summary.NewCrappy.Should().Be(1);
        result.Summary.FixedCrappy.Should().Be(1);
        result.Summary.Unchanged.Should().Be(1);
        result.Summary.Removed.Should().Be(1);
        result.Summary.Regressed.Should().Be(1);
        result.Summary.Added.Should().Be(1);
    }
}
