using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Parses Cobertura XML (from Coverlet) and applies the field selection decision tree
/// per spec 6.2: branch-rate preferred for methods with branches, line-rate fallback
/// for branchless methods. Branchless detection uses condition element count.
/// </summary>
public static partial class CoberturaCoverageReader
{
    public static IReadOnlyList<CoberturaMethodCoverage> Read(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        return ReadFromDocument(doc);
    }

    public static IReadOnlyList<CoberturaMethodCoverage> Read(Stream stream)
    {
        var doc = XDocument.Load(stream);
        return ReadFromDocument(doc);
    }

    private static List<CoberturaMethodCoverage> ReadFromDocument(XDocument doc)
    {
        var results = new List<CoberturaMethodCoverage>();
        var root = doc.Root;
        if (root is null) return results;

        var sources = root.Descendants("source").Select(s => s.Value.Trim()).ToList();

        foreach (var cls in root.Descendants("class"))
        {
            var className = cls.Attribute("name")?.Value ?? string.Empty;
            var filename = ResolveFileName(cls.Attribute("filename")?.Value, sources);

            var methodsElement = cls.Element("methods");
            if (methodsElement is null) continue;

            foreach (var method in methodsElement.Elements("method"))
            {
                var methodName = method.Attribute("name")?.Value ?? string.Empty;
                var signature = method.Attribute("signature")?.Value ?? string.Empty;
                var hasBranchRate = method.Attribute("branch-rate") is not null;

                results.Add(new CoberturaMethodCoverage
                {
                    ClassName = className,
                    MethodName = methodName,
                    Signature = signature,
                    FileName = filename,
                    StartLine = FirstLineNumber(method) ?? FirstLineNumber(cls),
                    Coverage = SelectCoverage(method),
                    Lines = ReadLines(method, countBranches: hasBranchRate)
                });
            }
        }

        return results;
    }

    // Each report's file names are relative to its own source root, and two test projects
    // can have different roots for the same file.
    private static string? ResolveFileName(string? fileName, List<string> sources)
    {
        if (fileName is null || sources.Count != 1 || Path.IsPathRooted(fileName))
            return fileName;

        return sources[0].TrimEnd('/', '\\') + "/" + fileName;
    }

    /// <summary>
    /// Applies the spec 6.2 decision tree:
    /// 1. branch-rate MISSING → use line-rate
    /// 2. branch-rate PRESENT, condition count == 0 → branchless → use line-rate
    /// 3. branch-rate PRESENT, condition count > 0 → use branch-rate
    /// 4. Neither present → 0.0
    /// </summary>
    private static double SelectCoverage(XElement method)
    {
        var branchRate = method.Attribute("branch-rate") is { } b ? ParseDouble(b.Value) : (double?)null;
        var lineRate = method.Attribute("line-rate") is { } l ? ParseDouble(l.Value) : (double?)null;

        return CoverageRate.Select(method.Descendants("condition").Any(), branchRate, lineRate);
    }

    private static int? FirstLineNumber(XElement element) =>
        NumberedLines(element).Select(l => (int?)l.Number).Min();

    // Branch counts are read only when the method has a branch-rate, so the merged rate
    // follows the same spec 6.2 rule as SelectCoverage.
    private static List<CoberturaLine> ReadLines(XElement method, bool countBranches)
    {
        var lines = new List<CoberturaLine>();
        foreach (var (line, number) in NumberedLines(method))
        {
            if (!long.TryParse(line.Attribute("hits")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hits))
                return [];

            var (covered, total) = (0, 0);
            if (countBranches && line.Descendants("condition").Any())
            {
                var counts = ConditionCountsRegex().Match(line.Attribute("condition-coverage")?.Value ?? "");
                if (!counts.Success)
                    return [];
                covered = int.Parse(counts.Groups[1].Value, CultureInfo.InvariantCulture);
                total = int.Parse(counts.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            lines.Add(new CoberturaLine
            {
                Number = number,
                Hit = hits > 0,
                BranchesCovered = covered,
                BranchesTotal = total
            });
        }

        return lines;
    }

    private static IEnumerable<(XElement Line, int Number)> NumberedLines(XElement element)
    {
        foreach (var line in element.Descendants("line"))
        {
            if (int.TryParse(line.Attribute("number")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                yield return (line, number);
        }
    }

    [GeneratedRegex(@"\((\d+)/(\d+)\)")]
    private static partial Regex ConditionCountsRegex();

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0.0;
}
