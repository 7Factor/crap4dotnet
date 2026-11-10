using Crap4DotNet.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Configuration;

public sealed class ConfigFileTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "crap4dotnet-config-tests", Guid.NewGuid().ToString("N"));

    public ConfigFileTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteConfig(string relativeDirectory, string json)
    {
        var directory = Path.Combine(_root, relativeDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ConfigFile.FileName);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Find_ConfigInTheAnalyzedDirectory()
    {
        var config = WriteConfig("", "{}");

        ConfigFile.Find(_root).Should().Be(config);
    }

    [Fact]
    public void Find_ConfigInAParentOfTheAnalyzedFile()
    {
        var config = WriteConfig("", "{}");
        var source = Path.Combine(_root, "src", "App", "Service.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "");

        ConfigFile.Find(source).Should().Be(config);
    }

    [Fact]
    public void Find_NearestConfigWins()
    {
        WriteConfig("", "{}");
        var nearer = WriteConfig("src", "{}");

        ConfigFile.Find(Path.Combine(_root, "src")).Should().Be(nearer);
    }

    [Fact]
    public void Find_NoConfig_ReturnsNull()
    {
        ConfigFile.Find(_root).Should().BeNull();
    }

    [Fact]
    public void Load_ReadsCrapThreshold()
    {
        var path = WriteConfig("", """{ "crap": { "threshold": 15 } }""");

        ConfigFile.Load(path).Crap!.Threshold.Should().Be(15);
    }

    [Fact]
    public void Load_EmptyObject_HasNoSettings()
    {
        var path = WriteConfig("", "{}");

        ConfigFile.Load(path).Crap.Should().BeNull();
    }

    [Theory]
    [InlineData("not json", "INVALID_CONFIGURATION")]
    [InlineData("null", "INVALID_CONFIGURATION")]
    [InlineData("""{ "crap": { "treshold": 15 } }""", "INVALID_CONFIGURATION")]
    [InlineData("""{ "threshold": 15 }""", "INVALID_CONFIGURATION")]
    [InlineData("""{ "crap": { "threshold": "15" } }""", "INVALID_CONFIGURATION")]
    [InlineData("""{ "crap": { "threshold": 0 } }""", "INVALID_THRESHOLD")]
    [InlineData("""{ "crap": { "threshold": -5 } }""", "INVALID_THRESHOLD")]
    public void Load_InvalidConfig_ThrowsWithThePath(string json, string expectedCode)
    {
        var path = WriteConfig("", json);

        var act = () => ConfigFile.Load(path);

        act.Should().Throw<InvalidConfigException>()
            .Where(e => e.FilePath == path && e.Code == expectedCode);
    }
}
