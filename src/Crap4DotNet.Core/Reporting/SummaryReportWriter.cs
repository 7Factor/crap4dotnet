using System.Globalization;
using System.Text;
using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Reporting;

/// <summary>
/// Formats ProjectCrapData as a fixed-width text table sorted by CRAP score descending.
/// </summary>
public static class SummaryReportWriter
{
    private const int MethodWidth = 30;
    private const int ClassWidth = 35;
    private const int CcWidth = 4;
    private const int CovWidth = 7;
    private const int CrapWidth = 8;

    public static string Write(ProjectCrapData data)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        // Header
        var title = string.Format(inv, "CRAP Report: {0}", data.Project);
        sb.AppendLine(title);
        sb.AppendLine(new string('=', title.Length));
        sb.AppendLine();

        // Column headers
        sb.Append("Method".PadRight(MethodWidth));
        sb.Append("  ");
        sb.Append("Class".PadRight(ClassWidth));
        sb.Append("  ");
        sb.Append("CC".PadLeft(CcWidth));
        sb.Append("  ");
        sb.Append("Cov%".PadLeft(CovWidth));
        sb.Append("  ");
        sb.AppendLine("CRAP".PadLeft(CrapWidth));

        var lineWidth = MethodWidth + 2 + ClassWidth + 2 + CcWidth + 2 + CovWidth + 2 + CrapWidth;
        sb.AppendLine(new string('-', lineWidth));

        // Methods sorted by CRAP descending
        var sorted = data.Methods.OrderByDescending(m => m.CrapScore);
        foreach (var method in sorted)
        {
            sb.Append(Truncate(method.Identity.MethodName, MethodWidth).PadRight(MethodWidth));
            sb.Append("  ");
            var qualifiedClass = string.IsNullOrEmpty(method.Identity.Namespace)
                ? method.Identity.ClassName
                : string.Concat(method.Identity.Namespace, ".", method.Identity.ClassName);
            sb.Append(Truncate(qualifiedClass, ClassWidth).PadRight(ClassWidth));
            sb.Append("  ");
            sb.Append(method.Complexity.ToString(inv).PadLeft(CcWidth));
            sb.Append("  ");
            sb.Append(FormatCoverage(method.Coverage).PadLeft(CovWidth));
            sb.Append("  ");
            sb.AppendLine(method.CrapScore.ToString("F1", inv).PadLeft(CrapWidth));
        }

        // Footer
        sb.AppendLine();
        sb.Append(inv, $"{data.Stats.MethodCount} methods analyzed, ");
        sb.AppendLine(inv, $"{data.Stats.CrappyMethodCount} CRAPpy (threshold: {data.Threshold})");

        var avg = data.Stats.AverageCrap.HasValue
            ? data.Stats.AverageCrap.Value.ToString("F1", inv) : "N/A";
        var median = data.Stats.MedianCrap.HasValue
            ? data.Stats.MedianCrap.Value.ToString("F1", inv) : "N/A";
        var load = data.Stats.TotalCrapLoad.ToString("F1", inv);
        sb.Append(inv, $"Average CRAP: {avg}  |  Median: {median}  |  Total CRAP Load: {load}");

        return sb.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }

    private static string FormatCoverage(double coverage)
    {
        return (coverage * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
    }
}
