using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Crap4DotNet.Cli.Commands;
using Crap4DotNet.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Cli.Tests;

public sealed class IntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "crap4dotnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private static (int exitCode, string stdout, string stderr) InvokeAnalyze(params string[] args)
    {
        var allArgs = args.Prepend("analyze").ToArray();
        return InvokeCommand(allArgs);
    }

    private static (int exitCode, string stdout, string stderr) InvokeDiff(params string[] args)
    {
        var allArgs = args.Prepend("diff").ToArray();
        return InvokeCommand(allArgs);
    }

    private static (int exitCode, string stdout, string stderr) InvokeCommand(string[] args)
    {
        // Reset exit code before each invocation
        Environment.ExitCode = 0;

        var stdoutWriter = new StringWriter();
        var stderrWriter = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);

            var rootCommand = new RootCommand("CRAP metric analysis tool for .NET")
            {
                AnalyzeCommand.Create(),
                DiffCommand.Create()
            };
            rootCommand.Invoke(args);

            return (Environment.ExitCode, stdoutWriter.ToString(), stderrWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    // === Spec sample source and coverage ===

    private const string SimpleCSharpSource = """
        namespace MyApp;

        public class Calculator
        {
            public int Add(int a, int b)
            {
                return a + b;
            }

            public int Divide(int a, int b)
            {
                if (b == 0)
                {
                    if (a > 0) return int.MaxValue;
                    if (a < 0) return int.MinValue;
                    return 0;
                }
                return a / b;
            }
        }
        """;

    private const string CoberturaCoverageXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.8" branch-rate="0.5" version="1.0" timestamp="1234567890">
          <packages>
            <package name="MyApp" line-rate="0.8" branch-rate="0.5">
              <classes>
                <class name="MyApp.Calculator" filename="Calculator.cs" line-rate="0.8" branch-rate="0.5">
                  <methods>
                    <method name="Add" signature="(System.Int32, System.Int32)" line-rate="1.0" branch-rate="1.0" />
                    <method name="Divide" signature="(System.Int32, System.Int32)" line-rate="0.6" branch-rate="0.3" />
                  </methods>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    // === Full pipeline integration ===

    [Fact]
    public void FullPipeline_AnalyzesSourceWithCoverage()
    {
        var sourcePath = CreateFile("Calculator.cs", SimpleCSharpSource);
        var coveragePath = CreateFile("coverage.cobertura.xml", CoberturaCoverageXml);

        var (exitCode, stdout, stderr) = InvokeAnalyze(sourcePath, "--coverage", coveragePath);

        stderr.Should().BeEmpty();

        var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        root.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        root.GetProperty("methods").GetArrayLength().Should().Be(2);
        root.GetProperty("stats").GetProperty("methodCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public void FullPipeline_ExitCode1_WhenCrappyMethodsExist()
    {
        // Divide has complexity 4, coverage 0.3 (branch-rate)
        // CRAP = 4^2*(1-0.3)^3 + 4 = 16*0.343 + 4 = 5.488 + 4 = 9.488
        // Not crappy with threshold 30.
        // Let's make a more complex method to trigger crappy
        var source = """
            namespace MyApp;
            public class Spaghetti
            {
                public int Mess(int a, int b, int c, int d, int e)
                {
                    if (a > 0) { if (b > 0) { if (c > 0) { if (d > 0) { if (e > 0) return 1; else return 2; } else return 3; } else return 4; } else return 5; } else return 6;
                    if (a < 0) { if (b < 0) { if (c < 0) { if (d < 0) { if (e < 0) return -1; else return -2; } else return -3; } else return -4; } else return -5; } else return -6;
                    return a + b + c + d + e;
                }
            }
            """;
        var coverage = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.1" branch-rate="0.05" version="1.0" timestamp="1234567890">
              <packages>
                <package name="MyApp" line-rate="0.1" branch-rate="0.05">
                  <classes>
                    <class name="MyApp.Spaghetti" filename="Spaghetti.cs" line-rate="0.1" branch-rate="0.05">
                      <methods>
                        <method name="Mess" signature="(System.Int32, System.Int32, System.Int32, System.Int32, System.Int32)" line-rate="0.1" branch-rate="0.05" />
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var sourcePath = CreateFile("Spaghetti.cs", source);
        var coveragePath = CreateFile("coverage.cobertura.xml", coverage);

        var (exitCode, stdout, stderr) = InvokeAnalyze(sourcePath, "--coverage", coveragePath);

        // High complexity + low coverage = CRAPpy method, exit code 1
        exitCode.Should().Be(1);
    }

    [Fact]
    public void FullPipeline_ExitCode0_WhenNoCrappyMethods()
    {
        var source = """
            namespace MyApp;
            public class Simple
            {
                public int Add(int a, int b) => a + b;
            }
            """;
        var coverage = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="1.0" branch-rate="1.0" version="1.0" timestamp="1234567890">
              <packages>
                <package name="MyApp" line-rate="1.0" branch-rate="1.0">
                  <classes>
                    <class name="MyApp.Simple" filename="Simple.cs" line-rate="1.0" branch-rate="1.0">
                      <methods>
                        <method name="Add" signature="(System.Int32, System.Int32)" line-rate="1.0" branch-rate="1.0" />
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;

        var sourcePath = CreateFile("Simple.cs", source);
        var coveragePath = CreateFile("coverage.cobertura.xml", coverage);

        var (exitCode, stdout, stderr) = InvokeAnalyze(sourcePath, "--coverage", coveragePath);

        exitCode.Should().Be(0);
        stderr.Should().BeEmpty();
    }

    [Fact]
    public void FullPipeline_DirectoryInput_FindsCsFiles()
    {
        var sourceDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(sourceDir);
        CreateFile("src/A.cs", "namespace MyApp; public class A { public void M() {} }");
        CreateFile("src/B.cs", "namespace MyApp; public class B { public void N() {} }");
        var coveragePath = CreateFile("coverage.cobertura.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="1.0" version="1.0" timestamp="0">
              <packages><package name="MyApp" line-rate="1.0">
                <classes><class name="MyApp.A" filename="A.cs" line-rate="1.0">
                  <methods><method name="M" signature="()" line-rate="1.0" /></methods>
                </class></classes>
              </package></packages>
            </coverage>
            """);

        var (exitCode, stdout, _) = InvokeAnalyze(sourceDir, "--coverage", coveragePath);

        exitCode.Should().Be(0);
        var doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("methods").GetArrayLength().Should().BeGreaterOrEqualTo(2);
    }

    private string CreateEmptyCoverage() => CreateFile("coverage.cobertura.xml", """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="1.0" version="1.0" timestamp="0"><packages /></coverage>
        """);

    private static (string project, string[] methods) ReportProjectAndMethods(string stdout)
    {
        var root = JsonDocument.Parse(stdout).RootElement;
        return (root.GetProperty("project").GetString()!,
            root.GetProperty("methods").EnumerateArray()
                .Select(m => m.GetProperty("fullName").GetString()!).Order().ToArray());
    }

    [Fact]
    public void Analyze_CsprojPath_AnalyzesItsDirectoryExceptBinAndObj()
    {
        var project = CreateFile("App/App.csproj", "<Project />");
        CreateFile("App/Service.cs", "namespace MyApp; public class Service { public void M() {} }");
        CreateFile("App/obj/Generated.cs", "namespace MyApp; public class Generated { public void G() {} }");
        CreateFile("App/bin/Copied.cs", "namespace MyApp; public class Copied { public void C() {} }");

        var (_, stdout, _) = InvokeAnalyze(project, "--coverage", CreateEmptyCoverage());

        var (projectName, methods) = ReportProjectAndMethods(stdout);
        projectName.Should().Be("App");
        methods.Should().Equal("MyApp.Service.M()");
    }

    [Theory]
    [InlineData("App.sln")]
    [InlineData("App.slnx")]
    public void Analyze_SolutionPath_ExcludesTestDirectories(string solutionFile)
    {
        var solution = CreateFile(solutionFile, "");
        CreateFile("src/Service.cs", "namespace MyApp; public class Service { public void M() {} }");
        CreateFile("tests/ServiceTests.cs", "namespace MyApp; public class ServiceTests { public void T() {} }");

        var (_, stdout, _) = InvokeAnalyze(solution, "--coverage", CreateEmptyCoverage());

        var (projectName, methods) = ReportProjectAndMethods(stdout);
        projectName.Should().Be("App");
        methods.Should().Equal("MyApp.Service.M()");
    }

    [Fact]
    public void Analyze_UnsupportedFileType_Exit2()
    {
        var path = CreateFile("notes.txt", "");

        var (exitCode, _, stderr) = InvokeAnalyze(path, "--coverage", CreateEmptyCoverage());

        exitCode.Should().Be(2);
        JsonDocument.Parse(stderr).RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("SOURCE_NOT_FOUND");
    }

    [Fact]
    public void FindCoverageFiles_ReturnsEveryCoverageFile()
    {
        var first = CreateFile("results/a/coverage.cobertura.xml", "");
        var second = CreateFile("results/b/coverage.cobertura.xml", "");

        AnalyzeCommand.FindCoverageFiles(Path.Combine(_tempDir, "results"))
            .Should().BeEquivalentTo([first, second]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FindCoverageFiles_NoCoverage_ReturnsNull(bool resultsDirExists)
    {
        var resultsDir = Path.Combine(_tempDir, "results");
        if (resultsDirExists)
            Directory.CreateDirectory(resultsDir);

        var stderr = new StringWriter();
        var originalErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            AnalyzeCommand.FindCoverageFiles(resultsDir).Should().BeNull();
        }
        finally
        {
            Console.SetError(originalErr);
        }

        JsonDocument.Parse(stderr.ToString()).RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("COVERAGE_FILE_NOT_FOUND");
    }

    [Fact]
    public void FullPipeline_OutputToFile()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");
        var coveragePath = CreateFile("coverage.cobertura.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="1.0" version="1.0" timestamp="0">
              <packages><package name="MyApp" line-rate="1.0"><classes>
                <class name="MyApp.Simple" filename="Simple.cs" line-rate="1.0">
                  <methods><method name="M" signature="()" line-rate="1.0" /></methods>
                </class></classes></package></packages>
            </coverage>
            """);
        var outputPath = Path.Combine(_tempDir, "report.json");

        var (exitCode, stdout, _) = InvokeAnalyze(sourcePath, "--coverage", coveragePath, "--output", outputPath);

        exitCode.Should().Be(0);
        stdout.Should().Contain("CRAP Report:"); // summary written to stdout
        File.Exists(outputPath).Should().BeTrue();
        var json = File.ReadAllText(outputPath);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
    }

    [Fact]
    public void FullPipeline_CustomThreshold()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public int Add(int a, int b) => a + b; }");
        var coveragePath = CreateFile("coverage.cobertura.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" version="1.0" timestamp="0">
              <packages><package name="MyApp" line-rate="0.5"><classes>
                <class name="MyApp.Simple" filename="Simple.cs" line-rate="0.5">
                  <methods><method name="Add" signature="(System.Int32, System.Int32)" line-rate="0.5" /></methods>
                </class></classes></package></packages>
            </coverage>
            """);

        var (exitCode, stdout, _) = InvokeAnalyze(
            sourcePath, "--coverage", coveragePath, "--threshold", "1");

        // comp=1, cov=0.5 → CRAP = 1*(1-0.5)^3 + 1 = 0.125 + 1 = 1.125, threshold=1 → crappy
        exitCode.Should().Be(1);
    }

    private const double HalfCoveredMethodCrap = 1.125;

    private (string source, string coverage) CreateHalfCoveredMethod()
    {
        var source = CreateFile("src/Simple.cs",
            "namespace MyApp; public class Simple { public int Add(int a, int b) => a + b; }");
        var coverage = CreateFile("src/coverage.cobertura.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.5" version="1.0" timestamp="0">
              <packages><package name="MyApp" line-rate="0.5"><classes>
                <class name="MyApp.Simple" filename="Simple.cs" line-rate="0.5">
                  <methods><method name="Add" signature="(System.Int32, System.Int32)" line-rate="0.5" /></methods>
                </class></classes></package></packages>
            </coverage>
            """);
        return (source, coverage);
    }

    private static int ReportThreshold(string stdout) =>
        JsonDocument.Parse(stdout).RootElement.GetProperty("threshold").GetInt32();

    [Theory]
    [InlineData(null, null, CrapOptions.DefaultThreshold)]
    [InlineData(1, null, 1)]
    [InlineData(1, 50, 50)]
    public void Analyze_Threshold_FlagThenConfigFileThenDefault(int? configThreshold, int? flag, int expected)
    {
        if (configThreshold is not null)
            CreateFile(ConfigFile.FileName, $$"""{ "crap": { "threshold": {{configThreshold}} } }""");
        var (source, coverage) = CreateHalfCoveredMethod();
        var args = new List<string> { source, "--coverage", coverage };
        if (flag is not null)
            args.AddRange(["--threshold", flag.Value.ToString(CultureInfo.InvariantCulture)]);

        var (exitCode, stdout, _) = InvokeAnalyze([.. args]);

        ReportThreshold(stdout).Should().Be(expected);
        exitCode.Should().Be(expected < HalfCoveredMethodCrap ? 1 : 0);
    }

    [Fact]
    public void Analyze_InvalidConfigFile_Exit2()
    {
        var config = CreateFile(ConfigFile.FileName, """{ "crap": { "treshold": 15 } }""");
        var (source, coverage) = CreateHalfCoveredMethod();

        var (exitCode, _, stderr) = InvokeAnalyze(source, "--coverage", coverage);

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr).RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("INVALID_CONFIGURATION");
        error.GetProperty("file").GetString().Should().Be(config);
    }

    [Fact]
    public void FullPipeline_MinCrapFilter()
    {
        var sourcePath = CreateFile("Calculator.cs", SimpleCSharpSource);
        var coveragePath = CreateFile("coverage.cobertura.xml", CoberturaCoverageXml);

        var (exitCode, stdout, _) = InvokeAnalyze(
            sourcePath, "--coverage", coveragePath, "--min-crap", "5.0");

        var doc = JsonDocument.Parse(stdout);
        // Methods with CRAP < 5.0 should be filtered from output
        foreach (var method in doc.RootElement.GetProperty("methods").EnumerateArray())
        {
            method.GetProperty("crap").GetDouble().Should().BeGreaterOrEqualTo(5.0);
        }
    }

    // === Spec 8.5 error scenarios ===

    [Fact]
    public void Error_SourceNotFound()
    {
        var (exitCode, stdout, stderr) = InvokeAnalyze("/nonexistent/path.cs", "--coverage", "dummy.xml");

        exitCode.Should().Be(2);
        stdout.Should().BeEmpty();
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("SOURCE_NOT_FOUND");
    }

    [Fact]
    public void Error_CoverageFileNotFound()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");

        var (exitCode, stdout, stderr) = InvokeAnalyze(sourcePath, "--coverage", "/nonexistent/coverage.xml");

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("COVERAGE_FILE_NOT_FOUND");
    }

    [Fact]
    public void Error_CoverageParseFail()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");
        var coveragePath = CreateFile("bad-coverage.xml", "<<<not xml at all>>>");

        var (exitCode, _, stderr) = InvokeAnalyze(sourcePath, "--coverage", coveragePath);

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("COVERAGE_PARSE_ERROR");
    }

    [Fact]
    public void Error_NoSourceFiles_EmptyDirectory()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);
        var coveragePath = CreateFile("coverage.cobertura.xml", CoberturaCoverageXml);

        var (exitCode, _, stderr) = InvokeAnalyze(emptyDir, "--coverage", coveragePath);

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("NO_SOURCE_FILES");
    }

    [Fact]
    public void Error_InvalidThreshold_Zero()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");

        var (exitCode, _, stderr) = InvokeAnalyze(sourcePath, "--threshold", "0");

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("INVALID_THRESHOLD");
    }

    [Fact]
    public void Error_InvalidThreshold_Negative()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");

        var (exitCode, _, stderr) = InvokeAnalyze(sourcePath, "--threshold", "-5");

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("INVALID_THRESHOLD");
    }

    // === Diff integration ===

    [Fact]
    public void Diff_FullPipeline()
    {
        var beforeReport = new
        {
            schemaVersion = "1.0",
            project = "TestProject",
            timestamp = "2026-03-01T00:00:00Z",
            threshold = 30,
            stats = new { methodCount = 2, crappyMethodCount = 1, totalCrapLoad = 50.0 },
            methods = new[]
            {
                new { fullName = "MyApp.Service.Clean()", crap = 5.0, complexity = 2, coverage = 0.9 },
                new { fullName = "MyApp.Service.Crappy()", crap = 50.0, complexity = 15, coverage = 0.1 }
            }
        };
        var afterReport = new
        {
            schemaVersion = "1.0",
            project = "TestProject",
            timestamp = "2026-03-12T00:00:00Z",
            threshold = 30,
            stats = new { methodCount = 2, crappyMethodCount = 0, totalCrapLoad = 0.0 },
            methods = new[]
            {
                new { fullName = "MyApp.Service.Clean()", crap = 5.0, complexity = 2, coverage = 0.9 },
                new { fullName = "MyApp.Service.Crappy()", crap = 12.0, complexity = 15, coverage = 0.95 }
            }
        };

        var beforePath = CreateFile("before.json", JsonSerializer.Serialize(beforeReport));
        var afterPath = CreateFile("after.json", JsonSerializer.Serialize(afterReport));

        var (exitCode, stdout, stderr) = InvokeDiff(beforePath, afterPath);

        exitCode.Should().Be(0); // no new crappy methods
        stderr.Should().BeEmpty();

        var doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("summary").GetProperty("fixedCrappy").GetInt32().Should().Be(1);
    }

    [Fact]
    public void Diff_ExitCode1_WhenNewCrappyMethods()
    {
        var beforeReport = new
        {
            schemaVersion = "1.0",
            project = "TestProject",
            timestamp = "2026-03-01T00:00:00Z",
            threshold = 30,
            stats = new { methodCount = 1, crappyMethodCount = 0, totalCrapLoad = 0.0 },
            methods = new[]
            {
                new { fullName = "MyApp.Service.M()", crap = 5.0, complexity = 2, coverage = 0.9 }
            }
        };
        var afterReport = new
        {
            schemaVersion = "1.0",
            project = "TestProject",
            timestamp = "2026-03-12T00:00:00Z",
            threshold = 30,
            stats = new { methodCount = 1, crappyMethodCount = 1, totalCrapLoad = 50.0 },
            methods = new[]
            {
                new { fullName = "MyApp.Service.M()", crap = 50.0, complexity = 15, coverage = 0.1 }
            }
        };

        var beforePath = CreateFile("before.json", JsonSerializer.Serialize(beforeReport));
        var afterPath = CreateFile("after.json", JsonSerializer.Serialize(afterReport));

        var (exitCode, stdout, _) = InvokeDiff(beforePath, afterPath);

        exitCode.Should().Be(1);
        var doc = JsonDocument.Parse(stdout);
        doc.RootElement.GetProperty("summary").GetProperty("newCrappy").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("""{ "methods": [] }""", "THRESHOLD_MISSING")]
    [InlineData("""{ "threshold": 15, "methods": [] }""", "THRESHOLD_MISMATCH")]
    public void Diff_Error_ThresholdMissingOrDifferent(string afterJson, string expectedCode)
    {
        var beforePath = CreateFile("before.json", """{ "threshold": 30, "methods": [] }""");
        var afterPath = CreateFile("after.json", afterJson);

        var (exitCode, _, stderr) = InvokeDiff(beforePath, afterPath);

        exitCode.Should().Be(2);
        JsonDocument.Parse(stderr).RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(expectedCode);
    }

    [Fact]
    public void Diff_OutputToFile()
    {
        var report = new
        {
            schemaVersion = "1.0",
            project = "TestProject",
            timestamp = "2026-03-01T00:00:00Z",
            threshold = 30,
            stats = new { methodCount = 0, crappyMethodCount = 0, totalCrapLoad = 0.0 },
            methods = Array.Empty<object>()
        };

        var beforePath = CreateFile("before.json", JsonSerializer.Serialize(report));
        var afterPath = CreateFile("after.json", JsonSerializer.Serialize(report));
        var outputPath = Path.Combine(_tempDir, "diff.json");

        var (exitCode, stdout, _) = InvokeDiff(beforePath, afterPath, "--output", outputPath);

        exitCode.Should().Be(0);
        stdout.Should().BeEmpty();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public void Diff_Error_BeforeFileNotFound()
    {
        var afterPath = CreateFile("after.json", "{}");

        var (exitCode, _, stderr) = InvokeDiff("/nonexistent.json", afterPath);

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("DIFF_FILE_NOT_FOUND");
    }

    [Fact]
    public void Diff_Error_AfterFileNotFound()
    {
        var beforePath = CreateFile("before.json", "{}");

        var (exitCode, _, stderr) = InvokeDiff(beforePath, "/nonexistent.json");

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("DIFF_FILE_NOT_FOUND");
    }

    [Fact]
    public void Diff_Error_InvalidJson()
    {
        var beforePath = CreateFile("before.json", "not json");
        var afterPath = CreateFile("after.json", "{}");

        var (exitCode, _, stderr) = InvokeDiff(beforePath, afterPath);

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("COVERAGE_PARSE_ERROR"); // reuses the parse error code
    }

    // === --run-tests flag ===

    [Fact]
    public void Error_RunTestsAndCoverage_MutuallyExclusive()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");
        var coveragePath = CreateFile("coverage.cobertura.xml", CoberturaCoverageXml);

        var (exitCode, _, stderr) = InvokeAnalyze(
            sourcePath, "--coverage", coveragePath, "--run-tests");

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("INVALID_CONFIGURATION");
        stderr.Should().Contain("mutually exclusive");
    }

    [Fact]
    public void Error_RunTestsWithCsFile()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public void M() {} }");

        var (exitCode, _, stderr) = InvokeAnalyze(sourcePath, "--run-tests");

        exitCode.Should().Be(2);
        var error = JsonDocument.Parse(stderr);
        error.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("INVALID_CONFIGURATION");
        stderr.Should().Contain(".cs file");
    }

    // === JSON schema validation ===

    [Fact]
    public void JsonOutput_HasRequiredSchemaFields()
    {
        var sourcePath = CreateFile("Simple.cs",
            "namespace MyApp; public class Simple { public int Add(int a, int b) => a + b; }");
        var coveragePath = CreateFile("coverage.cobertura.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="1.0" version="1.0" timestamp="0">
              <packages><package name="MyApp" line-rate="1.0"><classes>
                <class name="MyApp.Simple" filename="Simple.cs" line-rate="1.0">
                  <methods><method name="Add" signature="(System.Int32, System.Int32)" line-rate="1.0" /></methods>
                </class></classes></package></packages>
            </coverage>
            """);

        var (_, stdout, _) = InvokeAnalyze(sourcePath, "--coverage", coveragePath);
        var root = JsonDocument.Parse(stdout).RootElement;

        // Required top-level fields per spec 7.1
        root.TryGetProperty("schemaVersion", out _).Should().BeTrue();
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
        root.TryGetProperty("threshold", out _).Should().BeTrue();
        root.TryGetProperty("stats", out _).Should().BeTrue();
        root.TryGetProperty("methods", out _).Should().BeTrue();
        root.TryGetProperty("histogram", out _).Should().BeTrue();
        root.TryGetProperty("hierarchy", out _).Should().BeTrue();

        // Stats fields
        var stats = root.GetProperty("stats");
        stats.TryGetProperty("methodCount", out _).Should().BeTrue();
        stats.TryGetProperty("crappyMethodCount", out _).Should().BeTrue();
        stats.TryGetProperty("crappyMethodPercent", out _).Should().BeTrue();
        stats.TryGetProperty("totalCrap", out _).Should().BeTrue();
        stats.TryGetProperty("totalCrapLoad", out _).Should().BeTrue();
        stats.TryGetProperty("medianCrap", out _).Should().BeTrue();

        // Method fields
        var method = root.GetProperty("methods").EnumerateArray().First();
        method.TryGetProperty("fullName", out _).Should().BeTrue();
        method.TryGetProperty("crap", out _).Should().BeTrue();
        method.TryGetProperty("complexity", out _).Should().BeTrue();
        method.TryGetProperty("coverage", out _).Should().BeTrue();
        method.TryGetProperty("crapLoad", out _).Should().BeTrue();
        method.TryGetProperty("isCrappy", out _).Should().BeTrue();
        method.TryGetProperty("severity", out _).Should().BeTrue();
    }

    // === Error output structure ===

    [Fact]
    public void ErrorOutput_HasStructuredJsonFormat()
    {
        var (_, _, stderr) = InvokeAnalyze("/nonexistent.cs", "--coverage", "dummy.xml");

        var doc = JsonDocument.Parse(stderr);
        var error = doc.RootElement.GetProperty("error");
        error.TryGetProperty("code", out _).Should().BeTrue();
        error.TryGetProperty("message", out _).Should().BeTrue();
    }
}
