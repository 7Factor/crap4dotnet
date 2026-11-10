namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Resolves Cobertura file names to normalized absolute paths.
/// </summary>
public static class CoverageFilePath
{
    // Each report's file names are relative to its own source roots, and two test projects
    // can have different roots for the same file.
    public static string? Resolve(string? fileName, IReadOnlyList<string> sources)
    {
        if (string.IsNullOrEmpty(fileName))
            return fileName;
        if (IsRooted(fileName) || sources.Count == 0)
            return Normalize(fileName);

        var candidates = sources.Select(source => Normalize(source + "/" + fileName)).ToList();

        // With several roots, the report does not say which root a file belongs to.
        return candidates.Find(File.Exists) ?? candidates[0];
    }

    // Forward slashes only, so a path built from Windows and Unix pieces compares equal.
    public static string Normalize(string path)
    {
        var unified = path.Replace('\\', '/');
        var segments = new List<string>();
        foreach (var segment in unified.Split('/'))
        {
            if (segment is "" or ".")
                continue;
            if (segment == ".." && segments.Count > 0 && segments[^1] != "..")
                segments.RemoveAt(segments.Count - 1);
            else
                segments.Add(segment);
        }

        var prefix = unified.StartsWith('/') ? "/" : string.Empty;
        return prefix + string.Join('/', segments);
    }

    private static bool IsRooted(string path) =>
        path.StartsWith('/') || path.StartsWith('\\') || (path.Length >= 2 && path[1] == ':');
}
