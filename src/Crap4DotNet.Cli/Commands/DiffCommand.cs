using System.CommandLine;
using System.Text.Json;
using Crap4DotNet.Core.Analysis;
using Crap4DotNet.Core.Reporting;

namespace Crap4DotNet.Cli.Commands;

internal static class DiffCommand
{
    public static Command Create()
    {
        var beforeArg = new Argument<string>("before", "Path to the 'before' JSON report");
        var afterArg = new Argument<string>("after", "Path to the 'after' JSON report");
        var outputOpt = new Option<string?>("--output", "Write diff JSON to file instead of stdout");

        var command = new Command("diff", "Compare two CRAP analysis reports")
        {
            beforeArg,
            afterArg,
            outputOpt
        };

        command.SetHandler(Execute, beforeArg, afterArg, outputOpt);
        return command;
    }

    private static void Execute(string beforePath, string afterPath, string? outputPath)
    {
        if (!File.Exists(beforePath))
        {
            ErrorOutput.WriteError("DIFF_FILE_NOT_FOUND",
                $"Before report not found: {beforePath}", file: beforePath);
            Environment.ExitCode = 2;
            return;
        }

        if (!File.Exists(afterPath))
        {
            ErrorOutput.WriteError("DIFF_FILE_NOT_FOUND",
                $"After report not found: {afterPath}", file: afterPath);
            Environment.ExitCode = 2;
            return;
        }

        JsonDocument beforeDoc;
        JsonDocument afterDoc;
        try
        {
            beforeDoc = JsonDocument.Parse(File.ReadAllText(beforePath));
            afterDoc = JsonDocument.Parse(File.ReadAllText(afterPath));
        }
        catch (JsonException ex)
        {
            ErrorOutput.WriteError("COVERAGE_PARSE_ERROR",
                $"Failed to parse JSON report: {ex.Message}");
            Environment.ExitCode = 2;
            return;
        }

        var result = DiffEngine.Compare(beforeDoc, afterDoc);
        var json = DiffReportWriter.Write(result);

        if (outputPath is not null)
        {
            File.WriteAllText(outputPath, json);
        }
        else
        {
            Console.WriteLine(json);
        }

        // Exit 1 if there are new CRAPpy methods, 0 otherwise
        Environment.ExitCode = result.Summary.NewCrappy > 0 ? 1 : 0;
    }
}
