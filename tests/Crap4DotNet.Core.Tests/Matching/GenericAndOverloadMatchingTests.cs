using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Matching;

/// <summary>
/// Coverage matching for generic methods, parameter modifiers, nullable annotations, and overload sets.
/// </summary>
public sealed class GenericAndOverloadMatchingTests
{
    private static MethodComplexityResult Source(string methodName, string signature, string className = "Helper") =>
        new()
        {
            Identity = new MethodIdentity
            {
                Namespace = "MyApp",
                ClassName = className,
                MethodName = methodName,
                Signature = signature,
                FullName = $"MyApp.{className}.{methodName}{signature}",
                FilePath = "Test.cs",
                LineNumber = 1
            },
            Complexity = 10
        };

    private static CoberturaMethodCoverage Cover(string methodName, string signature, double coverage,
        string className = "MyApp.Helper") =>
        new() { ClassName = className, MethodName = methodName, Signature = signature, Coverage = coverage };

    [Fact]
    public void GenericMethod_WithByRefOutParameter_TakesItsCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("TryConvertToNumeric<T>", "(string, out T)")],
            [Cover("TryConvertToNumeric", "(System.String,T&)", 1.0)]);

        result.Methods.Should().HaveCount(1);
        result.Methods[0].Coverage.Should().Be(1.0);
    }

    [Fact]
    public void GenericMethod_NoParameters_TakesItsCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("GetObjectTypeName<T>", "()")],
            [Cover("GetObjectTypeName", "()", 0.75)]);

        result.Methods[0].Coverage.Should().Be(0.75);
    }

    [Fact]
    public void NullableReferenceAnnotation_DoesNotBlockTheMatch()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("BuildLink", "(string, string, int, string?)")],
            [Cover("BuildLink", "(System.String,System.String,System.Int32,System.String)", 0.5)]);

        result.Methods[0].Coverage.Should().Be(0.5);
    }

    [Fact]
    public void OverloadSet_PairsEachOverloadWithItsOwnCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [
                Source("BuildLink", "(string, string, int, string?)"),
                Source("BuildLink", "(EcsDtoBase, string, string?)")
            ],
            [
                Cover("BuildLink", "(System.String,System.String,System.Int32,System.String)", 0.25),
                Cover("BuildLink", "(MyApp.Dto.EcsDtoBase,System.String,System.String)", 0.75)
            ]);

        result.Methods.Should().HaveCount(2);
        result.Methods[0].Coverage.Should().Be(0.25);
        result.Methods[1].Coverage.Should().Be(0.75);
    }

    [Fact]
    public void NullableValueTypeOverloads_KeepTheirOwnCoverage()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int)"), Source("Foo", "(int?)")],
            [
                Cover("Foo", "(System.Int32)", 0.2),
                Cover("Foo", "(System.Nullable`1<System.Int32>)", 0.8)
            ]);

        result.Methods[0].Coverage.Should().Be(0.2);
        result.Methods[1].Coverage.Should().Be(0.8);
    }

    [Fact]
    public void RelaxedMatch_SharedBySeveralSourceMethods_Refuses()
    {
        // Foo<T>(int) and Foo(int) share the relaxed key Foo(int).
        var result = MethodCoverageMatcher.Match(
            [Source("Foo", "(int)"), Source("Foo<T>", "(int)")],
            [Cover("Foo", "(System.Int32)", 0.6)]);

        result.Methods[0].Coverage.Should().Be(0.6);
        result.Methods[1].Coverage.Should().Be(0.0);
    }

    [Fact]
    public void GenuinelyUncoveredMethod_StillReportsZero()
    {
        var result = MethodCoverageMatcher.Match(
            [Source("Untested<T>", "(int)")],
            [Cover("SomethingElse", "()", 1.0)]);

        result.Methods[0].Coverage.Should().Be(0.0);
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
    }
}
