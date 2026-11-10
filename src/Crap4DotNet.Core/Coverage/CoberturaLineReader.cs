using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Reads the <c>line</c> elements of a Cobertura method or class.
/// </summary>
public static partial class CoberturaLineReader
{
    public static int? FirstLineNumber(XElement element) =>
        NumberedLines(element).Select(l => (int?)l.Number).Min();

    /// <summary>
    /// The method's lines, or null when a hit or branch count cannot be parsed.
    /// </summary>
    /// <param name="method">A Cobertura <c>method</c> element.</param>
    /// <param name="countBranches">
    /// Whether to read branch counts. Spec 6.2 uses the branch rate only when the method has a
    /// <c>branch-rate</c>, so a merged rate follows the same rule.
    /// </param>
    public static List<CoberturaLine>? ReadLines(XElement method, bool countBranches)
    {
        var lines = new List<CoberturaLine>();
        foreach (var (line, number) in NumberedLines(method))
        {
            if (!long.TryParse(line.Attribute("hits")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hits))
                return null;

            var (covered, total) = (0, 0);
            if (countBranches && line.Descendants("condition").Any())
            {
                var counts = ConditionCountsRegex().Match(line.Attribute("condition-coverage")?.Value ?? string.Empty);
                if (!counts.Success)
                    return null;
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
}
