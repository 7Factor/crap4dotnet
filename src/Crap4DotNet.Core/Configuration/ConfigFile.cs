using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crap4DotNet.Core.Configuration;

public static class ConfigFile
{
    public const string FileName = "crap4dotnet-config.json";

    // A misspelled setting must fail, not fall back to the default.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string? Find(string analyzedPath)
    {
        var fullPath = Path.GetFullPath(analyzedPath);
        return AncestorFile.Find(Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath)!, FileName);
    }

    public static Crap4DotNetConfig Load(string path)
    {
        Crap4DotNetConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<Crap4DotNetConfig>(File.ReadAllText(path), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidConfigException("INVALID_CONFIGURATION", path, ex.Message);
        }

        if (config is null)
            throw new InvalidConfigException("INVALID_CONFIGURATION", path, "The file must contain a JSON object.");

        if (config.Crap?.Threshold <= 0)
            throw new InvalidConfigException("INVALID_THRESHOLD", path,
                $"crap.threshold must be greater than 0, got: {config.Crap.Threshold}");

        return config;
    }
}

public sealed record Crap4DotNetConfig
{
    public CrapSettings? Crap { get; init; }
}

public sealed record CrapSettings
{
    public int? Threshold { get; init; }
}

public sealed class InvalidConfigException(string code, string filePath, string message) : Exception(message)
{
    public string Code { get; } = code;
    public string FilePath { get; } = filePath;
}
