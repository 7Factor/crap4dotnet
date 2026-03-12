using System.Text.Json;
using System.Text.Json.Serialization;
using Crap4DotNet.Core.Analysis;

namespace Crap4DotNet.Core.Reporting;

/// <summary>
/// Serializes DiffResult into the spec 10.2.1 diff JSON schema.
/// </summary>
public static class DiffReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Write(DiffResult result)
    {
        var report = new
        {
            SchemaVersion = "1.0",
            Type = "diff",
            Before = new { Project = result.BeforeProject, Timestamp = result.BeforeTimestamp },
            After = new { Project = result.AfterProject, Timestamp = result.AfterTimestamp },
            Summary = result.Summary,
            Methods = new
            {
                NewCrappy = result.NewCrappy,
                FixedCrappy = result.FixedCrappy,
                Regressed = result.Regressed,
                Improved = result.Improved,
                Added = result.Added,
                Removed = result.Removed
            }
        };

        return JsonSerializer.Serialize(report, SerializerOptions);
    }
}
