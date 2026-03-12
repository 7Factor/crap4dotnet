using System.Text.Json;
using System.Text.Json.Serialization;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Reporting;

/// <summary>
/// Serializes ProjectCrapData into the spec 7.1 JSON report format.
/// Uses System.Text.Json with camelCase naming for machine-friendly output.
/// </summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Write(
        ProjectCrapData data,
        IReadOnlyList<DiagnosticWarning>? warnings = null,
        DateTimeOffset? timestamp = null)
    {
        var report = BuildReport(data, warnings, timestamp);
        return JsonSerializer.Serialize(report, SerializerOptions);
    }

    public static void Write(
        Stream stream,
        ProjectCrapData data,
        IReadOnlyList<DiagnosticWarning>? warnings = null,
        DateTimeOffset? timestamp = null)
    {
        var report = BuildReport(data, warnings, timestamp);
        JsonSerializer.Serialize(stream, report, SerializerOptions);
    }

    private static JsonReport BuildReport(
        ProjectCrapData data,
        IReadOnlyList<DiagnosticWarning>? warnings,
        DateTimeOffset? timestamp)
    {
        return new JsonReport
        {
            SchemaVersion = "1.0",
            Project = data.Project,
            Timestamp = (timestamp ?? DateTimeOffset.UtcNow).ToString("O"),
            Threshold = data.Threshold,
            Stats = MapStats(data.Stats),
            Methods = data.Methods.Select(MapMethod).ToList(),
            Histogram = data.Histogram.Select(MapBin).ToList(),
            Hierarchy = MapHierarchy(data.Namespaces),
            Warnings = warnings?.Select(MapWarning).ToList() ?? []
        };
    }

    private static JsonStats MapStats(CrapStatistics stats) =>
        new()
        {
            MethodCount = stats.MethodCount,
            TotalCrap = stats.TotalCrap,
            AverageCrap = stats.AverageCrap,
            MedianCrap = stats.MedianCrap,
            StandardDeviation = stats.StandardDeviation,
            CrappyMethodCount = stats.CrappyMethodCount,
            CrappyMethodPercent = stats.CrappyMethodPercent,
            TotalCrapLoad = stats.TotalCrapLoad
        };

    private static JsonMethod MapMethod(MethodCrapData method) =>
        new()
        {
            Namespace = method.Identity.Namespace,
            ClassName = method.Identity.ClassName,
            MethodName = method.Identity.MethodName,
            Signature = method.Identity.Signature,
            FullName = method.Identity.FullName,
            FilePath = method.Identity.FilePath,
            LineNumber = method.Identity.LineNumber,
            Crap = Math.Round(method.CrapScore, 2),
            Complexity = method.Complexity,
            Coverage = method.Coverage,
            CrapLoad = Math.Round(method.CrapLoad, 2),
            IsCrappy = method.IsCrappy,
            Severity = method.Severity.ToString().ToLowerInvariant()
        };

    private static JsonHistogramBin MapBin(HistogramBin bin) =>
        new()
        {
            Range = bin.Range,
            Count = bin.Count,
            Percent = bin.Percent
        };

    private static JsonHierarchy MapHierarchy(IReadOnlyList<NamespaceCrapData> namespaces) =>
        new()
        {
            Namespaces = namespaces.Select(ns => new JsonNamespace
            {
                Name = ns.Name,
                Stats = MapStats(ns.Stats),
                Classes = ns.Classes.Select(cls => new JsonClass
                {
                    Name = cls.Name,
                    Stats = MapStats(cls.Stats),
                    Methods = cls.Methods.Select(m => m.Identity.FullName).ToList()
                }).ToList()
            }).ToList()
        };

    private static JsonWarning MapWarning(DiagnosticWarning warning) =>
        new()
        {
            Code = warning.Code,
            Message = warning.Message
        };

    // --- JSON DTOs (internal shape for serialization) ---

    private sealed class JsonReport
    {
        public required string SchemaVersion { get; init; }
        public required string Project { get; init; }
        public required string Timestamp { get; init; }
        public required int Threshold { get; init; }
        public required JsonStats Stats { get; init; }
        public required List<JsonMethod> Methods { get; init; }
        public required List<JsonHistogramBin> Histogram { get; init; }
        public required JsonHierarchy Hierarchy { get; init; }
        public required List<JsonWarning> Warnings { get; init; }
    }

    private sealed class JsonStats
    {
        public int MethodCount { get; init; }
        public double TotalCrap { get; init; }
        public double? AverageCrap { get; init; }
        public double? MedianCrap { get; init; }
        public double? StandardDeviation { get; init; }
        public int CrappyMethodCount { get; init; }
        public double CrappyMethodPercent { get; init; }
        public double TotalCrapLoad { get; init; }
    }

    private sealed class JsonMethod
    {
        public required string Namespace { get; init; }
        public required string ClassName { get; init; }
        public required string MethodName { get; init; }
        public required string Signature { get; init; }
        public required string FullName { get; init; }
        public string? FilePath { get; init; }
        public int? LineNumber { get; init; }
        public double Crap { get; init; }
        public int Complexity { get; init; }
        public double Coverage { get; init; }
        public double CrapLoad { get; init; }
        public bool IsCrappy { get; init; }
        public required string Severity { get; init; }
    }

    private sealed class JsonHistogramBin
    {
        public required string Range { get; init; }
        public int Count { get; init; }
        public double Percent { get; init; }
    }

    private sealed class JsonHierarchy
    {
        public required List<JsonNamespace> Namespaces { get; init; }
    }

    private sealed class JsonNamespace
    {
        public required string Name { get; init; }
        public required JsonStats Stats { get; init; }
        public required List<JsonClass> Classes { get; init; }
    }

    private sealed class JsonClass
    {
        public required string Name { get; init; }
        public required JsonStats Stats { get; init; }
        public required List<string> Methods { get; init; }
    }

    private sealed class JsonWarning
    {
        public required string Code { get; init; }
        public required string Message { get; init; }
    }
}
