using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Crap4DotNet.Core.Sources;

public static partial class SourceFiles
{
    public static List<string> InDirectory(string directory)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}") && !f.Contains($"{separator}bin{separator}"))
            .ToList();
    }

    public static List<string> InSolution(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        var projects = ProjectPaths(solutionPath)
            .Select(path => new ProjectDirectory(Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar, IsTestProject(path)))
            .OrderByDescending(p => p.Path.Length)
            .ToList();

        // A file belongs to the nearest enclosing project, so a test project nested inside
        // another project's directory still excludes its own files.
        return InDirectory(solutionDirectory)
            .Where(file => projects.Find(p => file.StartsWith(p.Path, StringComparison.OrdinalIgnoreCase)) is { IsTest: false })
            .ToList();
    }

    public static List<string> ProjectPaths(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        var relativePaths = solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? ReadXml(solutionPath).Descendants("Project").Select(p => (string?)p.Attribute("Path")).OfType<string>()
            : SlnProjectLineRegex().Matches(File.ReadAllText(solutionPath)).Select(m => m.Groups[1].Value);

        return relativePaths
            .Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFullPath(Path.Combine(solutionDirectory, p.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();
    }

    public static bool IsTestProject(string projectPath)
    {
        if (!File.Exists(projectPath))
            throw new InvalidSourceException("SOURCE_NOT_FOUND", projectPath, $"The solution lists a project that does not exist: {projectPath}");

        return ReadXml(projectPath).Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Any(IsTestPackage);
    }

    private static bool IsTestPackage(string package) =>
        package.Equals("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)
        || package.Contains("xunit", StringComparison.OrdinalIgnoreCase)
        || package.Contains("nunit", StringComparison.OrdinalIgnoreCase)
        || package.Contains("mstest", StringComparison.OrdinalIgnoreCase);

    private static XDocument ReadXml(string path)
    {
        try
        {
            return XDocument.Load(path);
        }
        catch (XmlException ex)
        {
            throw new InvalidSourceException("SOURCE_PARSE_ERROR", path, $"Failed to read {path}: {ex.Message}");
        }
    }

    private sealed record ProjectDirectory(string Path, bool IsTest);

    [GeneratedRegex(@"^Project\(""[^""]*""\)\s*=\s*""[^""]*"",\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex SlnProjectLineRegex();
}

public sealed class InvalidSourceException(string code, string filePath, string message) : Exception(message)
{
    public string Code { get; } = code;
    public string FilePath { get; } = filePath;
}
