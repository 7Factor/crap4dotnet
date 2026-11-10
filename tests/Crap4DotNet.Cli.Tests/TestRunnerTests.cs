using Crap4DotNet.Cli.Commands;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Cli.Tests;

public sealed class TestRunnerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "crap4dotnet-runner-tests", Guid.NewGuid().ToString("N"));

    public TestRunnerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteGlobalJson(string json)
    {
        var path = Path.Combine(_root, "global.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void GlobalJsonSelectingTestingPlatform_IsReturned()
    {
        var globalJson = WriteGlobalJson("""{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

        TestRunner.FindTestingPlatformGlobalJson(_root).Should().Be(globalJson);
    }

    [Fact]
    public void GlobalJsonInAParentDirectory_IsFound()
    {
        var globalJson = WriteGlobalJson("""{ "test": { "runner": "Microsoft.Testing.Platform" } }""");
        var subdirectory = Directory.CreateDirectory(Path.Combine(_root, "src", "App")).FullName;

        TestRunner.FindTestingPlatformGlobalJson(subdirectory).Should().Be(globalJson);
    }

    [Theory]
    [InlineData("""{ "sdk": { "version": "10.0.100" } }""")]
    [InlineData("""{ "test": { "runner": "VSTest" } }""")]
    [InlineData("""{ "test": "Microsoft.Testing.Platform" }""")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void GlobalJsonNotSelectingTestingPlatform_ReturnsNull(string json)
    {
        WriteGlobalJson(json);

        TestRunner.FindTestingPlatformGlobalJson(_root).Should().BeNull();
    }
}
