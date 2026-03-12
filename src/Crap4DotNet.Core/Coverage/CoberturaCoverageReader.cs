using System.Globalization;
using System.Xml.Linq;

namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Parses Cobertura XML (from Coverlet) and applies the field selection decision tree
/// per spec 6.2: branch-rate preferred for methods with branches, line-rate fallback
/// for branchless methods. Branchless detection uses condition element count.
/// </summary>
public static class CoberturaCoverageReader
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

        foreach (var cls in root.Descendants("class"))
        {
            var className = cls.Attribute("name")?.Value ?? string.Empty;
            var filename = cls.Attribute("filename")?.Value;

            var methodsElement = cls.Element("methods");
            if (methodsElement is null) continue;

            foreach (var method in methodsElement.Elements("method"))
            {
                var methodName = method.Attribute("name")?.Value ?? string.Empty;
                var signature = method.Attribute("signature")?.Value ?? string.Empty;
                var coverage = SelectCoverage(method);

                results.Add(new CoberturaMethodCoverage
                {
                    ClassName = className,
                    MethodName = methodName,
                    Signature = signature,
                    FileName = filename,
                    Coverage = coverage
                });
            }
        }

        return results;
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
        var branchRateAttr = method.Attribute("branch-rate");
        var lineRateAttr = method.Attribute("line-rate");

        if (branchRateAttr is null)
            return lineRateAttr is not null ? ParseDouble(lineRateAttr.Value) : 0.0;

        var conditionCount = method.Descendants("condition").Count();

        if (conditionCount == 0)
            return lineRateAttr is not null ? ParseDouble(lineRateAttr.Value) : 0.0;

        return ParseDouble(branchRateAttr.Value);
    }

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0.0;
}
