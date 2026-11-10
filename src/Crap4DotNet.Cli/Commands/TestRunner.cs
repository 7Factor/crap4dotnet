using System.Text.Json;
using Crap4DotNet.Core.Configuration;

namespace Crap4DotNet.Cli.Commands;

internal static class TestRunner
{
    private static readonly JsonDocumentOptions GlobalJsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string? FindTestingPlatformGlobalJson(string workingDirectory)
    {
        var globalJson = AncestorFile.Find(workingDirectory, "global.json");
        return globalJson is not null && SelectsTestingPlatform(globalJson) ? globalJson : null;
    }

    private static bool SelectsTestingPlatform(string globalJson)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(globalJson), GlobalJsonOptions);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("test", out var test)
                && test.ValueKind == JsonValueKind.Object
                && test.TryGetProperty("runner", out var runner)
                && runner.ValueKind == JsonValueKind.String
                && runner.GetString() == "Microsoft.Testing.Platform";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
