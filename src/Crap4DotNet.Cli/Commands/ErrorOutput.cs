using System.Text.Json;

namespace Crap4DotNet.Cli.Commands;

/// <summary>
/// Writes structured error JSON to stderr per spec 8.1.
/// </summary>
internal static class ErrorOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void WriteError(string code, string message, string? details = null, string? file = null)
    {
        var error = new
        {
            Error = new
            {
                Code = code,
                Message = message,
                Details = details,
                File = file
            }
        };

        Console.Error.WriteLine(JsonSerializer.Serialize(error, Options));
    }
}
