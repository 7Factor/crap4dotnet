using Crap4DotNet.Core.Models;
using Crap4DotNet.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Reporting;

public sealed class SummaryReportWriterTests
{
    private static MethodCrapData MakeMethod(
        string ns = "MyApp",
        string className = "Service",
        string methodName = "DoWork",
        double crap = 5.0,
        int complexity = 2,
        double coverage = 0.8,
        double crapLoad = 0.0,
        bool isCrappy = false,
        SeverityBand severity = SeverityBand.Low) =>
        new()
        {
            Identity = new MethodIdentity
            {
                Namespace = ns,
                ClassName = className,
                MethodName = methodName,
                Signature = "()",
                FullName = $"{ns}.{className}.{methodName}()"
            },
            CrapScore = crap,
            Complexity = complexity,
            Coverage = coverage,
            CrapLoad = crapLoad,
            IsCrappy = isCrappy,
            Severity = severity
        };

    private static ProjectCrapData MakeProject(
        IReadOnlyList<MethodCrapData>? methods = null,
        string project = "TestProject",
        int threshold = 30)
    {
        var m = methods ?? [MakeMethod()];
        return new ProjectCrapData
        {
            Project = project,
            Threshold = threshold,
            Stats = new CrapStatistics
            {
                MethodCount = m.Count,
                TotalCrap = m.Sum(x => x.CrapScore),
                AverageCrap = m.Count > 0 ? m.Average(x => x.CrapScore) : null,
                MedianCrap = m.Count > 0 ? m[m.Count / 2].CrapScore : null,
                StandardDeviation = 0.0,
                CrappyMethodCount = m.Count(x => x.IsCrappy),
                CrappyMethodPercent = m.Count > 0
                    ? (double)m.Count(x => x.IsCrappy) / m.Count * 100 : 0,
                TotalCrapLoad = m.Sum(x => x.CrapLoad)
            },
            Methods = m,
            Histogram = [new HistogramBin { Range = "0-5", Count = 1, Percent = 100.0 }],
            Namespaces = []
        };
    }

    [Fact]
    public void Header_IncludesProjectName()
    {
        var report = SummaryReportWriter.Write(MakeProject(project: "MyApp"));

        report.Should().StartWith("CRAP Report: MyApp");
        report.Should().Contain(new string('=', "CRAP Report: MyApp".Length));
    }

    [Fact]
    public void Methods_SortedByCrapDescending()
    {
        var methods = new[]
        {
            MakeMethod(methodName: "Low", crap: 1.0),
            MakeMethod(methodName: "High", crap: 100.0),
            MakeMethod(methodName: "Mid", crap: 25.0)
        };
        var report = SummaryReportWriter.Write(MakeProject(methods: methods));

        var highIndex = report.IndexOf("High", StringComparison.Ordinal);
        var midIndex = report.IndexOf("Mid", StringComparison.Ordinal);
        var lowIndex = report.IndexOf("Low", StringComparison.Ordinal);
        highIndex.Should().BeLessThan(midIndex);
        midIndex.Should().BeLessThan(lowIndex);
    }

    [Fact]
    public void Columns_CoverageFormattedAsPercent()
    {
        var report = SummaryReportWriter.Write(
            MakeProject(methods: [MakeMethod(coverage: 1.0)]));

        report.Should().Contain("100.0%");
    }

    [Fact]
    public void Columns_CrapScoreOneDecimal()
    {
        var report = SummaryReportWriter.Write(
            MakeProject(methods: [MakeMethod(crap: 130.27)]));

        report.Should().Contain("130.3");
    }

    [Fact]
    public void Columns_ComplexityRightAligned()
    {
        var report = SummaryReportWriter.Write(
            MakeProject(methods: [MakeMethod(complexity: 12)]));
        var lines = report.Split('\n');
        var dataLine = lines.First(l => l.Contains("DoWork"));

        // CC column should have "  12" (right-aligned in 4-char column)
        dataLine.Should().Contain("  12");
    }

    [Fact]
    public void LongMethodName_TruncatedWithEllipsis()
    {
        var longName = new string('A', 40);
        var report = SummaryReportWriter.Write(
            MakeProject(methods: [MakeMethod(methodName: longName)]));

        report.Should().Contain("AAAAAAAAAAAAAAAAAAAAAAAAAAA...");
        report.Should().NotContain(longName);
    }

    [Fact]
    public void LongClassName_TruncatedWithEllipsis()
    {
        var report = SummaryReportWriter.Write(
            MakeProject(methods: [MakeMethod(
                ns: "Very.Long.Namespace.That.Goes.On",
                className: "AndOnService")]));

        // "Very.Long.Namespace.That.Goes.On.AndOnService" is 46 chars, > 35
        report.Should().Contain("...");
    }

    [Fact]
    public void Footer_ContainsMethodCount()
    {
        var methods = new[]
        {
            MakeMethod(methodName: "A"),
            MakeMethod(methodName: "B"),
            MakeMethod(methodName: "C")
        };
        var report = SummaryReportWriter.Write(MakeProject(methods: methods));

        report.Should().Contain("3 methods analyzed");
    }

    [Fact]
    public void Footer_ContainsCrappyCountAndThreshold()
    {
        var methods = new[]
        {
            MakeMethod(methodName: "A", crap: 50.0, isCrappy: true),
            MakeMethod(methodName: "B", crap: 1.0, isCrappy: false)
        };
        var report = SummaryReportWriter.Write(MakeProject(methods: methods, threshold: 30));

        report.Should().Contain("1 CRAPpy (threshold: 30)");
    }

    [Fact]
    public void Footer_ContainsAverageMedianAndLoad()
    {
        var report = SummaryReportWriter.Write(MakeProject());

        report.Should().Contain("Average CRAP:");
        report.Should().Contain("Median:");
        report.Should().Contain("Total CRAP Load:");
    }

    [Fact]
    public void EmptyMethodsList_ProducesCleanOutput()
    {
        var project = new ProjectCrapData
        {
            Project = "Empty",
            Threshold = 30,
            Stats = new CrapStatistics
            {
                MethodCount = 0,
                TotalCrap = 0,
                AverageCrap = null,
                MedianCrap = null,
                StandardDeviation = null,
                CrappyMethodCount = 0,
                CrappyMethodPercent = 0,
                TotalCrapLoad = 0
            },
            Methods = [],
            Histogram = [],
            Namespaces = []
        };

        var report = SummaryReportWriter.Write(project);

        report.Should().Contain("CRAP Report: Empty");
        report.Should().Contain("0 methods analyzed");
        report.Should().Contain("N/A");
    }

    [Fact]
    public void ClassColumn_IncludesNamespace()
    {
        var report = SummaryReportWriter.Write(
            MakeProject(methods: [MakeMethod(ns: "MyApp.Services", className: "FooService")]));

        report.Should().Contain("MyApp.Services.FooService");
    }
}
