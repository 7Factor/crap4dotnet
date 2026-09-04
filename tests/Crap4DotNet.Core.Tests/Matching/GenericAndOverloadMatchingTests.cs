using Crap4DotNet.Core.Complexity;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Matching;

/// <summary>
/// Coverage is silently discarded for two shapes that are ordinary in real code:
/// generic methods and overload sets. Every case below is taken verbatim from a
/// coverlet run over a production solution, where the Cobertura reported
/// branch-rate="1" for the method while the report scored it 0.0.
///
/// Three independent mismatches cause it, and each needs its own case because
/// fixing one still leaves the others failing:
///   * the Roslyn key carries method-level generic arity (Foo&lt;&gt;) that a
///     Cobertura method name never has;
///   * Roslyn writes C# parameter modifiers (out T) where the CLR signature
///     writes by-ref (T&amp;);
///   * Roslyn carries nullable-reference annotations (string?) that the CLR
///     signature cannot express at all.
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
        // The name-only fallback deliberately refuses an ambiguous set, so an overload
        // set can only be resolved by the exact-signature pass. That is what makes
        // signature normalization load-bearing rather than cosmetic.
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
    public void GenuinelyUncoveredMethod_StillReportsZero()
    {
        // The repair must not invent coverage: a source method with no coverage entry
        // at all still defaults to 0.0 and is still reported as unmatched.
        var result = MethodCoverageMatcher.Match(
            [Source("Untested<T>", "(int)")],
            [Cover("SomethingElse", "()", 1.0)]);

        result.Methods[0].Coverage.Should().Be(0.0);
        result.Warnings.Should().Contain(w => w.Code == "UNMATCHED_METHODS");
    }
}
