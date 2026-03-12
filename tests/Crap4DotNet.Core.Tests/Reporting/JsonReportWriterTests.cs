using System.Text.Json;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;
using Crap4DotNet.Core.Reporting;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Reporting;

public sealed class JsonReportWriterTests
{
    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 3, 12, 12, 0, 0, TimeSpan.Zero);

    private static MethodCrapData MakeMethod(
        string ns = "MyApp",
        string className = "Service",
        string methodName = "DoWork",
        string signature = "()",
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
                Signature = signature,
                FullName = $"{ns}.{className}.{methodName}{signature}",
                FilePath = "Test.cs",
                LineNumber = 10
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
                AverageCrap = m.Average(x => x.CrapScore),
                MedianCrap = m.Count > 0 ? m[m.Count / 2].CrapScore : null,
                StandardDeviation = 0.0,
                CrappyMethodCount = m.Count(x => x.IsCrappy),
                CrappyMethodPercent = m.Count > 0
                    ? (double)m.Count(x => x.IsCrappy) / m.Count * 100 : 0,
                TotalCrapLoad = m.Sum(x => x.CrapLoad)
            },
            Methods = m,
            Histogram =
            [
                new HistogramBin { Range = "0-5", Count = 1, Percent = 100.0 }
            ],
            Namespaces =
            [
                new NamespaceCrapData
                {
                    Name = "MyApp",
                    Stats = new CrapStatistics
                    {
                        MethodCount = m.Count,
                        TotalCrap = m.Sum(x => x.CrapScore),
                        AverageCrap = m.Average(x => x.CrapScore),
                        MedianCrap = m.Count > 0 ? m[m.Count / 2].CrapScore : null,
                        StandardDeviation = 0.0,
                        CrappyMethodCount = 0,
                        CrappyMethodPercent = 0,
                        TotalCrapLoad = 0
                    },
                    Classes =
                    [
                        new TypeCrapData
                        {
                            Name = "Service",
                            Stats = new CrapStatistics
                            {
                                MethodCount = m.Count,
                                TotalCrap = m.Sum(x => x.CrapScore),
                                AverageCrap = m.Average(x => x.CrapScore),
                                MedianCrap = m.Count > 0 ? m[m.Count / 2].CrapScore : null,
                                StandardDeviation = 0.0,
                                CrappyMethodCount = 0,
                                CrappyMethodPercent = 0,
                                TotalCrapLoad = 0
                            },
                            Methods = m
                        }
                    ]
                }
            ]
        };
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement;

    // === Schema structure ===

    [Fact]
    public void HasSchemaVersion()
    {
        var json = JsonReportWriter.Write(MakeProject(), timestamp: FixedTimestamp);
        Parse(json).GetProperty("schemaVersion").GetString().Should().Be("1.0");
    }

    [Fact]
    public void HasProjectName()
    {
        var json = JsonReportWriter.Write(
            MakeProject(project: "MyProject"), timestamp: FixedTimestamp);
        Parse(json).GetProperty("project").GetString().Should().Be("MyProject");
    }

    [Fact]
    public void HasTimestamp()
    {
        var json = JsonReportWriter.Write(MakeProject(), timestamp: FixedTimestamp);
        var ts = Parse(json).GetProperty("timestamp").GetString();
        ts.Should().Contain("2026-03-12");
    }

    [Fact]
    public void HasThreshold()
    {
        var json = JsonReportWriter.Write(
            MakeProject(threshold: 15), timestamp: FixedTimestamp);
        Parse(json).GetProperty("threshold").GetInt32().Should().Be(15);
    }

    // === Stats ===

    [Fact]
    public void Stats_ContainsAllFields()
    {
        var json = JsonReportWriter.Write(MakeProject(), timestamp: FixedTimestamp);
        var stats = Parse(json).GetProperty("stats");

        stats.GetProperty("methodCount").GetInt32().Should().Be(1);
        stats.GetProperty("totalCrap").GetDouble().Should().Be(5.0);
        stats.GetProperty("averageCrap").GetDouble().Should().Be(5.0);
        stats.GetProperty("crappyMethodCount").GetInt32().Should().Be(0);
        stats.GetProperty("crappyMethodPercent").GetDouble().Should().Be(0.0);
        stats.GetProperty("totalCrapLoad").GetDouble().Should().Be(0.0);
    }

    [Fact]
    public void Stats_NullableFieldsSerializedAsNull_WhenEmpty()
    {
        var emptyProject = new ProjectCrapData
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
        var json = JsonReportWriter.Write(emptyProject, timestamp: FixedTimestamp);
        var stats = Parse(json).GetProperty("stats");

        stats.GetProperty("averageCrap").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("medianCrap").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("standardDeviation").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // === Methods array ===

    [Fact]
    public void Methods_ContainsAllFields()
    {
        var method = MakeMethod(
            ns: "MyApp.Services",
            className: "UserService",
            methodName: "Validate",
            signature: "(string)",
            crap: 45.3,
            complexity: 12,
            coverage: 0.35,
            crapLoad: 9.0,
            isCrappy: true,
            severity: SeverityBand.High);

        var json = JsonReportWriter.Write(
            MakeProject(methods: [method]), timestamp: FixedTimestamp);
        var m = Parse(json).GetProperty("methods")[0];

        m.GetProperty("namespace").GetString().Should().Be("MyApp.Services");
        m.GetProperty("className").GetString().Should().Be("UserService");
        m.GetProperty("methodName").GetString().Should().Be("Validate");
        m.GetProperty("signature").GetString().Should().Be("(string)");
        m.GetProperty("fullName").GetString()
            .Should().Be("MyApp.Services.UserService.Validate(string)");
        m.GetProperty("filePath").GetString().Should().Be("Test.cs");
        m.GetProperty("lineNumber").GetInt32().Should().Be(10);
        m.GetProperty("crap").GetDouble().Should().Be(45.3);
        m.GetProperty("complexity").GetInt32().Should().Be(12);
        m.GetProperty("coverage").GetDouble().Should().Be(0.35);
        m.GetProperty("crapLoad").GetDouble().Should().Be(9.0);
        m.GetProperty("isCrappy").GetBoolean().Should().BeTrue();
        m.GetProperty("severity").GetString().Should().Be("high");
    }

    [Fact]
    public void Methods_CrapRoundedToTwoDecimals()
    {
        var method = MakeMethod(crap: 12.345678);
        var json = JsonReportWriter.Write(
            MakeProject(methods: [method]), timestamp: FixedTimestamp);
        Parse(json).GetProperty("methods")[0]
            .GetProperty("crap").GetDouble().Should().Be(12.35);
    }

    [Fact]
    public void Methods_MultipleMethodsSerialized()
    {
        var methods = new[]
        {
            MakeMethod(methodName: "A"),
            MakeMethod(methodName: "B"),
            MakeMethod(methodName: "C")
        };
        var json = JsonReportWriter.Write(
            MakeProject(methods: methods), timestamp: FixedTimestamp);
        Parse(json).GetProperty("methods").GetArrayLength().Should().Be(3);
    }

    // === Histogram ===

    [Fact]
    public void Histogram_Serialized()
    {
        var json = JsonReportWriter.Write(MakeProject(), timestamp: FixedTimestamp);
        var histogram = Parse(json).GetProperty("histogram");
        histogram.GetArrayLength().Should().BeGreaterThan(0);
        var bin = histogram[0];
        bin.GetProperty("range").GetString().Should().Be("0-5");
        bin.GetProperty("count").GetInt32().Should().Be(1);
        bin.GetProperty("percent").GetDouble().Should().Be(100.0);
    }

    // === Hierarchy ===

    [Fact]
    public void Hierarchy_HasNamespacesWithClasses()
    {
        var json = JsonReportWriter.Write(MakeProject(), timestamp: FixedTimestamp);
        var hierarchy = Parse(json).GetProperty("hierarchy");
        var namespaces = hierarchy.GetProperty("namespaces");
        namespaces.GetArrayLength().Should().Be(1);

        var ns = namespaces[0];
        ns.GetProperty("name").GetString().Should().Be("MyApp");
        ns.GetProperty("stats").GetProperty("methodCount").GetInt32().Should().Be(1);

        var classes = ns.GetProperty("classes");
        classes.GetArrayLength().Should().Be(1);
        classes[0].GetProperty("name").GetString().Should().Be("Service");
        classes[0].GetProperty("methods").GetArrayLength().Should().Be(1);
    }

    // === Warnings ===

    [Fact]
    public void Warnings_Empty_WhenNone()
    {
        var json = JsonReportWriter.Write(MakeProject(), timestamp: FixedTimestamp);
        Parse(json).GetProperty("warnings").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Warnings_Serialized()
    {
        var warnings = new List<DiagnosticWarning>
        {
            new() { Code = "COVERAGE_STALE", Message = "Coverage data may be stale" },
            new() { Code = "UNMATCHED_METHODS", Message = "2 methods unmatched" }
        };
        var json = JsonReportWriter.Write(MakeProject(), warnings, FixedTimestamp);
        var w = Parse(json).GetProperty("warnings");
        w.GetArrayLength().Should().Be(2);
        w[0].GetProperty("code").GetString().Should().Be("COVERAGE_STALE");
        w[1].GetProperty("code").GetString().Should().Be("UNMATCHED_METHODS");
    }

    // === Stream overload ===

    [Fact]
    public void WriteToStream_ProducesValidJson()
    {
        using var stream = new MemoryStream();
        JsonReportWriter.Write(stream, MakeProject(), timestamp: FixedTimestamp);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
    }

    // === Severity as lowercase string ===

    [Theory]
    [InlineData(SeverityBand.Low, "low")]
    [InlineData(SeverityBand.Moderate, "moderate")]
    [InlineData(SeverityBand.Elevated, "elevated")]
    [InlineData(SeverityBand.High, "high")]
    [InlineData(SeverityBand.Critical, "critical")]
    public void Severity_SerializedAsLowercase(SeverityBand band, string expected)
    {
        var method = MakeMethod(severity: band);
        var json = JsonReportWriter.Write(
            MakeProject(methods: [method]), timestamp: FixedTimestamp);
        Parse(json).GetProperty("methods")[0]
            .GetProperty("severity").GetString().Should().Be(expected);
    }
}
