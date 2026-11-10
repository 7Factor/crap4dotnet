using Crap4DotNet.Core.Coverage;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Coverage;

public sealed class CoverageFilePathTests
{
    [Fact]
    public void DifferentSourceRoots_ResolveTheSameFileToOnePath()
    {
        var fromCoreTests = CoverageFilePath.Resolve("Coverage/Foo.cs", ["/repo/src/Crap4DotNet.Core/"]);
        var fromCliTests = CoverageFilePath.Resolve("Crap4DotNet.Core/Coverage/Foo.cs", ["/repo/src/"]);

        fromCoreTests.Should().Be("/repo/src/Crap4DotNet.Core/Coverage/Foo.cs").And.Be(fromCliTests);
    }

    [Fact]
    public void WindowsSourceRoots_ResolveTheSameFileToOnePath()
    {
        var fromCoreTests = CoverageFilePath.Resolve(@"Coverage\Foo.cs", [@"C:\repo\src\Crap4DotNet.Core\"]);
        var fromCliTests = CoverageFilePath.Resolve(@"Crap4DotNet.Core\Coverage\Foo.cs", [@"C:\repo\src\"]);

        fromCoreTests.Should().Be("C:/repo/src/Crap4DotNet.Core/Coverage/Foo.cs").And.Be(fromCliTests);
    }

    [Fact]
    public void ParentSegments_AreResolved()
    {
        CoverageFilePath.Resolve("../Core/Foo.cs", ["/repo/src/Cli/"]).Should().Be("/repo/src/Core/Foo.cs");
    }

    [Fact]
    public void RootedFileName_IgnoresTheSourceRoot()
    {
        CoverageFilePath.Resolve(@"C:\repo\Foo.cs", ["/elsewhere/"]).Should().Be("C:/repo/Foo.cs");
    }

    [Fact]
    public void NoSourceRoot_KeepsTheRelativeName()
    {
        CoverageFilePath.Resolve("Foo.cs", []).Should().Be("Foo.cs");
    }

    [Fact]
    public void SeveralSourceRoots_UseTheRootThatHasTheFile()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var withFile = Directory.CreateDirectory(Path.Combine(root.FullName, "b"));
            File.WriteAllText(Path.Combine(withFile.FullName, "Foo.cs"), "");
            var withoutFile = Path.Combine(root.FullName, "a");

            CoverageFilePath.Resolve("Foo.cs", [withoutFile, withFile.FullName])
                .Should().Be(CoverageFilePath.Normalize(Path.Combine(withFile.FullName, "Foo.cs")));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void SeveralSourceRoots_WithoutTheFileOnDisk_UseTheFirstRoot()
    {
        CoverageFilePath.Resolve("Foo.cs", ["/missing/a/", "/missing/b/"]).Should().Be("/missing/a/Foo.cs");
    }
}
