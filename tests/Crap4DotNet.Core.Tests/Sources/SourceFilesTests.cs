using Crap4DotNet.Core.Sources;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Sources;

public sealed class SourceFilesTests : IDisposable
{
    private const string LibraryProject = """<Project Sdk="Microsoft.NET.Sdk" />""";

    private const string TestProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" /></ItemGroup>
        </Project>
        """;

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "crap4dotnet-source-tests", Guid.NewGuid().ToString("N"));

    public SourceFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string relativePath, string content = "")
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Slnx(params string[] projects) =>
        "<Solution>" + string.Concat(projects.Select(p => $"""<Project Path="{p}" />""")) + "</Solution>";

    private string[] RelativeSourceFiles(string solution) =>
        SourceFiles.InSolution(solution)
            .Select(f => Path.GetRelativePath(_root, f).Replace('\\', '/'))
            .Order()
            .ToArray();

    [Fact]
    public void ProjectPaths_Sln_ReadsProjectsAndSkipsSolutionFolders()
    {
        var solution = Write("App.sln", """
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{827E0CD3-B72D-47B6-A68D-7590B98EB39B}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Lib", "src\Lib\Lib.csproj", "{B5981DA5-7143-4580-A3C8-7B9B677BE0E6}"
            EndProject
            """);

        SourceFiles.ProjectPaths(solution).Should().Equal(Path.Combine(_root, "src", "Lib", "Lib.csproj"));
    }

    [Fact]
    public void ProjectPaths_Slnx_ReadsProjectsInsideFolders()
    {
        var solution = Write("App.slnx", """
            <Solution>
              <Folder Name="/src/"><Project Path="src/Lib/Lib.csproj" /></Folder>
              <Project Path="Tool/Tool.csproj" />
            </Solution>
            """);

        SourceFiles.ProjectPaths(solution).Should().Equal(
            Path.Combine(_root, "src", "Lib", "Lib.csproj"),
            Path.Combine(_root, "Tool", "Tool.csproj"));
    }

    [Theory]
    [InlineData("Microsoft.NET.Test.Sdk", true)]
    [InlineData("xunit", true)]
    [InlineData("xunit.v3", true)]
    [InlineData("NUnit", true)]
    [InlineData("MSTest.TestFramework", true)]
    [InlineData("Newtonsoft.Json", false)]
    public void IsTestProject_DecidedByPackageReferences(string package, bool expected)
    {
        var project = Write("P/P.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><PackageReference Include="{package}" Version="1.0.0" /></ItemGroup>
            </Project>
            """);

        SourceFiles.IsTestProject(project).Should().Be(expected);
    }

    [Fact]
    public void IsTestProject_ReadsProjectsWithTheMSBuildNamespace()
    {
        var project = Write("P/P.csproj", """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup><PackageReference Include="xunit" /></ItemGroup>
            </Project>
            """);

        SourceFiles.IsTestProject(project).Should().BeTrue();
    }

    [Fact]
    public void InSolution_ExcludesTestProjectsWhateverTheirFolderName()
    {
        Write("Lib/Lib.csproj", LibraryProject);
        Write("Lib/Calc.cs");
        Write("Lib.Tests/Lib.Tests.csproj", TestProject);
        Write("Lib.Tests/CalcTests.cs");
        var solution = Write("App.slnx", Slnx("Lib/Lib.csproj", "Lib.Tests/Lib.Tests.csproj"));

        RelativeSourceFiles(solution).Should().Equal("Lib/Calc.cs");
    }

    [Fact]
    public void InSolution_KeepsNonTestFilesWithTestInTheirPath()
    {
        Write("src/Testing/Testing.csproj", LibraryProject);
        Write("src/Testing/TestDataBuilder.cs");
        var solution = Write("App.slnx", Slnx("src/Testing/Testing.csproj"));

        RelativeSourceFiles(solution).Should().Equal("src/Testing/TestDataBuilder.cs");
    }

    [Fact]
    public void InSolution_FileBelongsToItsNearestProject()
    {
        Write("App.csproj", LibraryProject);
        Write("Program.cs");
        Write("App.Tests/App.Tests.csproj", TestProject);
        Write("App.Tests/ProgramTests.cs");
        var solution = Write("App.slnx", Slnx("App.csproj", "App.Tests/App.Tests.csproj"));

        RelativeSourceFiles(solution).Should().Equal("Program.cs");
    }

    [Fact]
    public void InSolution_ExcludesFilesOutsideTheSolutionsProjects()
    {
        Write("Lib/Lib.csproj", LibraryProject);
        Write("Lib/Calc.cs");
        Write("Unlisted/Unlisted.csproj", LibraryProject);
        Write("Unlisted/Other.cs");
        Write("scripts/Build.cs");
        var solution = Write("App.slnx", Slnx("Lib/Lib.csproj"));

        RelativeSourceFiles(solution).Should().Equal("Lib/Calc.cs");
    }

    [Fact]
    public void InSolution_MissingProject_Throws()
    {
        var solution = Write("App.slnx", Slnx("Missing/Missing.csproj"));

        var act = () => SourceFiles.InSolution(solution);

        act.Should().Throw<InvalidSourceException>()
            .Where(e => e.Code == "SOURCE_NOT_FOUND" && e.FilePath.EndsWith("Missing.csproj", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("App.slnx", "not xml")]
    [InlineData("Lib/Lib.csproj", "not xml")]
    public void InSolution_UnreadableXml_ThrowsWithTheFile(string brokenFile, string content)
    {
        Write("Lib/Lib.csproj", LibraryProject);
        var solution = Write("App.slnx", Slnx("Lib/Lib.csproj"));
        var broken = Write(brokenFile, content);

        var act = () => SourceFiles.InSolution(solution);

        act.Should().Throw<InvalidSourceException>()
            .Where(e => e.Code == "SOURCE_PARSE_ERROR" && e.FilePath == broken);
    }
}
