using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Models;

public sealed class MethodIdentityTests
{
    private static MethodIdentity CreateIdentity(
        string fullName,
        string? ns = "MyApp",
        string? className = "Service",
        string? methodName = "DoWork",
        string? signature = "()",
        string? filePath = null,
        int? lineNumber = null) => new()
    {
        Namespace = ns!,
        ClassName = className!,
        MethodName = methodName!,
        Signature = signature!,
        FullName = fullName,
        FilePath = filePath,
        LineNumber = lineNumber
    };

    [Fact]
    public void Equals_SameFullName_ReturnsTrue()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()");
        var b = CreateIdentity("MyApp.Service.DoWork()");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentFullName_ReturnsFalse()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()");
        var b = CreateIdentity("MyApp.Service.DoOther()");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_SameFullName_DifferentMetadata_ReturnsTrue()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()", filePath: "src/A.cs", lineNumber: 10);
        var b = CreateIdentity("MyApp.Service.DoWork()", filePath: "src/B.cs", lineNumber: 99);

        a.Should().Be(b);
    }

    [Fact]
    public void Equals_SameFullName_DifferentComponentFields_ReturnsTrue()
    {
        // Equality is FullName-only; even if component fields differ, same FullName means equal
        var a = CreateIdentity("MyApp.Service.DoWork()", ns: "MyApp", className: "Service");
        var b = CreateIdentity("MyApp.Service.DoWork()", ns: "Different", className: "Other");

        a.Should().Be(b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()");

        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameFullName_SameHash()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()");
        var b = CreateIdentity("MyApp.Service.DoWork()");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentFullName_DifferentHash()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()");
        var b = CreateIdentity("MyApp.Service.DoOther()");

        // Not guaranteed but extremely likely for different strings
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void DictionaryLookup_ByFullName()
    {
        var key = CreateIdentity("MyApp.Service.DoWork()");
        var lookup = CreateIdentity("MyApp.Service.DoWork()", filePath: "different.cs");

        var dict = new Dictionary<MethodIdentity, double> { [key] = 0.85 };

        dict.Should().ContainKey(lookup);
        dict[lookup].Should().Be(0.85);
    }

    [Fact]
    public void Equals_CaseSensitive()
    {
        var a = CreateIdentity("MyApp.Service.DoWork()");
        var b = CreateIdentity("myapp.service.dowork()");

        a.Should().NotBe(b);
    }
}
