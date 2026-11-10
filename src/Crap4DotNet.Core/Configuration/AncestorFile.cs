namespace Crap4DotNet.Core.Configuration;

public static class AncestorFile
{
    public static string? Find(string startDirectory, string fileName)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
