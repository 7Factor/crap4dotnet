using System.CommandLine;
using System.Diagnostics;
using Crap4DotNet.Core.Analysis;
using Crap4DotNet.Core.Configuration;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Reporting;

namespace Crap4DotNet.Cli.Commands;

internal static class AnalyzeCommand
{
    public static Command Create()
    {
        var pathArg = new Argument<string>("path", "Path to .cs file, directory, .csproj, or .sln");
        var coverageOpt = new Option<string[]>("--coverage", "Path(s) to Cobertura XML coverage file(s)")
        {
            AllowMultipleArgumentsPerToken = true
        };
        var thresholdOpt = new Option<int>("--threshold", () => 30, "CRAP threshold (default: 30)");
        var outputOpt = new Option<string?>("--output", "Write JSON to file instead of stdout");
        var minCrapOpt = new Option<double?>("--min-crap", "Only include methods with CRAP >= this value");
        var runTestsOpt = new Option<bool>("--run-tests", "Run dotnet test to generate coverage before analysis");

        var command = new Command("analyze", "Analyze source code for CRAP metrics")
        {
            pathArg,
            coverageOpt,
            thresholdOpt,
            outputOpt,
            minCrapOpt,
            runTestsOpt
        };

        command.SetHandler(Execute, pathArg, coverageOpt, thresholdOpt, outputOpt, minCrapOpt, runTestsOpt);
        return command;
    }

    private static void Execute(
        string path,
        string[] coveragePaths,
        int threshold,
        string? outputPath,
        double? minCrap,
        bool runTests)
    {
        // Validate --run-tests and --coverage are mutually exclusive
        if (runTests && coveragePaths.Length > 0)
        {
            ErrorOutput.WriteError("INVALID_CONFIGURATION",
                "--run-tests and --coverage are mutually exclusive. Use one or the other.");
            Environment.ExitCode = 2;
            return;
        }

        // Validate --run-tests requires a project-level path
        if (runTests && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            ErrorOutput.WriteError("INVALID_CONFIGURATION",
                "--run-tests requires a .csproj, .sln, or directory path, not a .cs file.");
            Environment.ExitCode = 2;
            return;
        }

        // Validate threshold
        if (threshold <= 0)
        {
            ErrorOutput.WriteError("INVALID_THRESHOLD",
                $"Threshold must be greater than 0, got: {threshold}");
            Environment.ExitCode = 2;
            return;
        }

        var options = new CrapOptions { Threshold = threshold };

        // Resolve source files
        List<string> sourceFiles;
        string projectName;
        try
        {
            (sourceFiles, projectName) = ResolveSourceFiles(path);
        }
        catch (CrapCliException ex)
        {
            ErrorOutput.WriteError(ex.Code, ex.Message, file: ex.FilePath);
            Environment.ExitCode = 2;
            return;
        }

        if (sourceFiles.Count == 0)
        {
            ErrorOutput.WriteError("NO_SOURCE_FILES",
                "No .cs source files found in the specified path",
                file: path);
            Environment.ExitCode = 2;
            return;
        }

        // Run tests to generate coverage if requested
        List<string> resolvedCoveragePaths;
        if (runTests)
        {
            var testCoveragePaths = RunTests(path);
            if (testCoveragePaths is null)
            {
                // RunTests already wrote error output
                Environment.ExitCode = 2;
                return;
            }
            resolvedCoveragePaths = testCoveragePaths;
        }
        else
        {
            // Resolve coverage files
            try
            {
                resolvedCoveragePaths = ResolveCoveragePaths(coveragePaths, path);
            }
            catch (CrapCliException ex)
            {
                ErrorOutput.WriteError(ex.Code, ex.Message, file: ex.FilePath);
                Environment.ExitCode = 2;
                return;
            }
        }

        // Read coverage data
        var coverageEntries = new List<CoberturaMethodCoverage>();
        foreach (var covPath in resolvedCoveragePaths)
        {
            try
            {
                using var stream = File.OpenRead(covPath);
                coverageEntries.AddRange(CoberturaCoverageReader.Read(stream));
            }
            catch (Exception ex)
            {
                ErrorOutput.WriteError("COVERAGE_PARSE_ERROR",
                    $"Failed to parse coverage file: {ex.Message}",
                    file: covPath);
                Environment.ExitCode = 2;
                return;
            }
        }

        // Run analysis
        AnalysisResult result;
        try
        {
            result = CrapAnalyzer.Analyze(sourceFiles, coverageEntries, options, projectName);
        }
        catch (Exception ex)
        {
            ErrorOutput.WriteError("SOURCE_PARSE_ERROR",
                $"Failed to analyze source: {ex.Message}");
            Environment.ExitCode = 2;
            return;
        }

        // Apply min-crap filter to output (analysis still includes all methods for stats)
        var outputData = result.Data;
        if (minCrap.HasValue)
        {
            var filtered = result.Data.Methods
                .Where(m => m.CrapScore >= minCrap.Value)
                .ToList();
            outputData = result.Data with { Methods = filtered };
        }

        // Write report
        var json = JsonReportWriter.Write(outputData, result.Warnings);

        if (outputPath is not null)
        {
            File.WriteAllText(outputPath, json);
        }
        else
        {
            Console.WriteLine(json);
        }

        // Exit code: 1 if any method is CRAPpy, 0 otherwise
        Environment.ExitCode = result.Data.Stats.CrappyMethodCount > 0 ? 1 : 0;
    }

    private static List<string>? RunTests(string path)
    {
        var fullPath = Path.GetFullPath(path);

        // Determine the target for dotnet test
        string target;
        if (fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            target = fullPath;
        }
        else if (Directory.Exists(fullPath))
        {
            target = fullPath;
        }
        else
        {
            ErrorOutput.WriteError("INVALID_CONFIGURATION",
                $"--run-tests requires a .csproj, .sln, or directory path: {path}");
            return null;
        }

        var resultsDir = Path.Combine(
            Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath) ?? ".",
            "TestResults",
            $"crap-{Guid.NewGuid():N}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{target}\" --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            // Forward test output so the user can see what failed
            if (!string.IsNullOrWhiteSpace(stdout))
                Console.Error.Write(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.Error.Write(stderr);

            ErrorOutput.WriteError("TEST_FAILURE",
                $"dotnet test exited with code {process.ExitCode}. Fix failing tests and retry.");
            return null;
        }

        // Find generated coverage files
        if (!Directory.Exists(resultsDir))
        {
            ErrorOutput.WriteError("COVERAGE_FILE_NOT_FOUND",
                "dotnet test completed but no TestResults directory was created. Is the coverlet collector installed?",
                file: resultsDir);
            return null;
        }

        var coverageFiles = Directory.GetFiles(resultsDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
            .ToList();

        if (coverageFiles.Count == 0)
        {
            ErrorOutput.WriteError("COVERAGE_FILE_NOT_FOUND",
                "dotnet test completed but no coverage.cobertura.xml was generated. "
                + "Ensure the coverlet.collector NuGet package is referenced in your test project(s).",
                file: resultsDir);
            return null;
        }

        return coverageFiles;
    }

    private static (List<string> files, string projectName) ResolveSourceFiles(string path)
    {
        if (!Path.Exists(path))
            throw new CrapCliException("SOURCE_NOT_FOUND",
                $"Source path not found: {path}", path);

        // Single .cs file
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            return ([Path.GetFullPath(path)], Path.GetFileNameWithoutExtension(path));

        // Directory
        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToList();
            return (files, Path.GetFileName(Path.GetFullPath(path)));
        }

        // .csproj — find .cs files in its directory
        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToList();
            return (files, Path.GetFileNameWithoutExtension(path));
        }

        // .sln — find .cs files in solution directory, excluding test projects
        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}test", StringComparison.OrdinalIgnoreCase)
                         && !f.Contains($"{Path.DirectorySeparatorChar}tests", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return (files, Path.GetFileNameWithoutExtension(path));
        }

        throw new CrapCliException("SOURCE_NOT_FOUND",
            $"Unsupported source path: {path}", path);
    }

    private static List<string> ResolveCoveragePaths(string[] coveragePaths, string sourcePath)
    {
        // Explicit coverage paths provided
        if (coveragePaths.Length > 0)
        {
            foreach (var p in coveragePaths)
            {
                if (!File.Exists(p))
                    throw new CrapCliException("COVERAGE_FILE_NOT_FOUND",
                        $"Coverage file not found: {p}", p);
            }
            return coveragePaths.Select(Path.GetFullPath).ToList();
        }

        // Auto-discovery per spec 6.5.2
        var searchDir = Directory.Exists(sourcePath)
            ? sourcePath
            : Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".";

        var discovered = Directory.GetFiles(searchDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
            .Where(f => f.Contains("TestResults"))
            .ToList();

        if (discovered.Count == 0)
        {
            throw new CrapCliException("COVERAGE_FILE_NOT_FOUND",
                "No coverage files found. Run 'dotnet test --collect:\"XPlat Code Coverage\"' to generate coverage data.",
                searchDir);
        }

        return discovered;
    }
}

internal sealed class CrapCliException(string code, string message, string? filePath = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public string? FilePath { get; } = filePath;
}
