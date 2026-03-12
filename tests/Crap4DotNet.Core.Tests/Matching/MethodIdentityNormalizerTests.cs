using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Matching;

public sealed class MethodIdentityNormalizerTests
{
    private static MethodIdentity MakeRoslynIdentity(
        string ns = "MyApp",
        string className = "Service",
        string methodName = "DoWork",
        string signature = "()",
        string? fullName = null,
        string? filePath = null) =>
        new()
        {
            Namespace = ns,
            ClassName = className,
            MethodName = methodName,
            Signature = signature,
            FullName = fullName ?? $"{(string.IsNullOrEmpty(ns) ? "" : ns + ".")}{className}.{methodName}{signature}",
            FilePath = filePath,
            LineNumber = 1
        };

    private static CoberturaMethodCoverage MakeCobertura(
        string className = "MyApp.Service",
        string methodName = "DoWork",
        string signature = "()",
        double coverage = 0.5) =>
        new()
        {
            ClassName = className,
            MethodName = methodName,
            Signature = signature,
            Coverage = coverage
        };

    // === NormalizeFromRoslyn ===

    [Fact]
    public void Roslyn_SimpleMethod()
    {
        var identity = MakeRoslynIdentity();
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Service.DoWork()");
    }

    [Fact]
    public void Roslyn_MethodWithParams()
    {
        var identity = MakeRoslynIdentity(signature: "(string, int)");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Service.DoWork(string, int)");
    }

    [Fact]
    public void Roslyn_GenericType_StripsParamNames()
    {
        var identity = MakeRoslynIdentity(
            className: "Cache<T>",
            methodName: "Get",
            signature: "(string)",
            fullName: "MyApp.Cache<T>.Get(string)");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Cache<>.Get(string)");
    }

    [Fact]
    public void Roslyn_GenericMethod_StripsParamNames()
    {
        var identity = MakeRoslynIdentity(
            methodName: "Find<T>",
            signature: "(string)",
            fullName: "MyApp.Service.Find<T>(string)");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Service.Find<>(string)");
    }

    [Fact]
    public void Roslyn_MultipleGenericParams()
    {
        var identity = MakeRoslynIdentity(
            className: "Dict<TKey, TValue>",
            methodName: "Add",
            signature: "(string, int)",
            fullName: "MyApp.Dict<TKey, TValue>.Add(string, int)");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Dict<,>.Add(string, int)");
    }

    [Fact]
    public void Roslyn_PropertyAccessor_AddsParens()
    {
        var identity = MakeRoslynIdentity(
            methodName: "Value.get",
            signature: "",
            fullName: "MyApp.Service.Value.get");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Service.Value.get()");
    }

    [Fact]
    public void Roslyn_NestedType()
    {
        var identity = MakeRoslynIdentity(
            className: "Outer.Inner",
            methodName: "Run",
            signature: "()",
            fullName: "MyApp.Outer.Inner.Run()");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Outer.Inner.Run()");
    }

    [Fact]
    public void Roslyn_Constructor()
    {
        var identity = MakeRoslynIdentity(
            methodName: "Service",
            signature: "(string)",
            fullName: "MyApp.Service.Service(string)");
        RoslynMethodParser.ToCanonicalKey(identity)
            .Should().Be("MyApp.Service.Service(string)");
    }

    // === NormalizeFromCobertura ===

    [Fact]
    public void Cobertura_SimpleMethod()
    {
        var cov = MakeCobertura();
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork()");
    }

    [Fact]
    public void Cobertura_PrimitiveTypes_Normalized()
    {
        var cov = MakeCobertura(signature: "(System.String, System.Int32)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork(string, int)");
    }

    [Fact]
    public void Cobertura_AllPrimitiveTypes()
    {
        var cov = MakeCobertura(signature: "(System.Boolean, System.Int64, System.Double, System.Decimal)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork(bool, long, double, decimal)");
    }

    [Fact]
    public void Cobertura_GenericType_BacktickNormalized()
    {
        var cov = MakeCobertura(
            className: "MyApp.Cache`1",
            methodName: "Get",
            signature: "(System.String)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Cache<>.Get(string)");
    }

    [Fact]
    public void Cobertura_GenericMethod_BacktickNormalized()
    {
        var cov = MakeCobertura(
            methodName: "Find`1",
            signature: "(System.String)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.Find<>(string)");
    }

    [Fact]
    public void Cobertura_MultipleGenericParams()
    {
        var cov = MakeCobertura(
            className: "MyApp.Dict`2",
            methodName: "Add",
            signature: "(System.String, System.Int32)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Dict<,>.Add(string, int)");
    }

    [Fact]
    public void Cobertura_NestedType_SlashNormalized()
    {
        var cov = MakeCobertura(
            className: "MyApp.Outer/Inner",
            methodName: "Run",
            signature: "()");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Outer.Inner.Run()");
    }

    [Fact]
    public void Cobertura_Constructor_MappedToClassName()
    {
        var cov = MakeCobertura(
            className: "MyApp.Service",
            methodName: ".ctor",
            signature: "(System.String)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.Service(string)");
    }

    [Fact]
    public void Cobertura_GenericTypeConstructor()
    {
        var cov = MakeCobertura(
            className: "MyApp.Cache`1",
            methodName: ".ctor",
            signature: "(System.Int32)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Cache<>.Cache(int)");
    }

    [Fact]
    public void Cobertura_PropertyGetter()
    {
        var cov = MakeCobertura(
            methodName: "get_Value",
            signature: "()");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.Value.get()");
    }

    [Fact]
    public void Cobertura_PropertySetter()
    {
        var cov = MakeCobertura(
            methodName: "set_Value",
            signature: "(System.Int32)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.Value.set(int)");
    }

    [Fact]
    public void Cobertura_OperatorAddition()
    {
        var cov = MakeCobertura(
            className: "MyApp.Number",
            methodName: "op_Addition",
            signature: "(MyApp.Number, MyApp.Number)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Number.operator +(Number, Number)");
    }

    [Fact]
    public void Cobertura_OperatorEquality()
    {
        var cov = MakeCobertura(
            className: "MyApp.Value",
            methodName: "op_Equality",
            signature: "(MyApp.Value, MyApp.Value)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Value.operator ==(Value, Value)");
    }

    [Fact]
    public void Cobertura_GenericParameterType()
    {
        var cov = MakeCobertura(
            signature: "(System.Collections.Generic.List`1<System.String>)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork(List<string>)");
    }

    [Fact]
    public void Cobertura_NullableType()
    {
        var cov = MakeCobertura(
            signature: "(System.Nullable`1<System.Int32>)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork(int?)");
    }

    [Fact]
    public void Cobertura_ArrayType()
    {
        var cov = MakeCobertura(signature: "(System.String[])");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork(string[])");
    }

    [Fact]
    public void Cobertura_ByRefType()
    {
        var cov = MakeCobertura(signature: "(System.Int32&)");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork(ref int)");
    }

    [Fact]
    public void Cobertura_EmptySignature()
    {
        var cov = MakeCobertura(signature: "");
        CoberturaMethodParser.ToCanonicalKey(cov)
            .Should().Be("MyApp.Service.DoWork()");
    }

    // === Cross-format matching (Roslyn canonical == Cobertura canonical) ===

    [Fact]
    public void Match_SimpleMethod()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity());
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura());
        roslyn.Should().Be(cobertura);
    }

    [Fact]
    public void Match_MethodWithPrimitiveParams()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity(signature: "(string, int)"));
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura(signature: "(System.String, System.Int32)"));
        roslyn.Should().Be(cobertura);
    }

    [Fact]
    public void Match_GenericType()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity(
                className: "Cache<T>",
                methodName: "Get",
                signature: "(string)",
                fullName: "MyApp.Cache<T>.Get(string)"));
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura(
                className: "MyApp.Cache`1",
                methodName: "Get",
                signature: "(System.String)"));
        roslyn.Should().Be(cobertura);
    }

    [Fact]
    public void Match_GenericMethod()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity(
                methodName: "Find<T>",
                signature: "(string)",
                fullName: "MyApp.Service.Find<T>(string)"));
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura(
                methodName: "Find`1",
                signature: "(System.String)"));
        roslyn.Should().Be(cobertura);
    }

    [Fact]
    public void Match_Constructor()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity(
                methodName: "Service",
                signature: "(string)",
                fullName: "MyApp.Service.Service(string)"));
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura(
                methodName: ".ctor",
                signature: "(System.String)"));
        roslyn.Should().Be(cobertura);
    }

    [Fact]
    public void Match_NestedType()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity(
                className: "Outer.Inner",
                methodName: "Run",
                signature: "()",
                fullName: "MyApp.Outer.Inner.Run()"));
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura(
                className: "MyApp.Outer/Inner",
                methodName: "Run",
                signature: "()"));
        roslyn.Should().Be(cobertura);
    }

    [Fact]
    public void Match_PropertyGetter()
    {
        var roslyn = RoslynMethodParser.ToCanonicalKey(
            MakeRoslynIdentity(
                methodName: "Value.get",
                signature: "",
                fullName: "MyApp.Service.Value.get"));
        var cobertura = CoberturaMethodParser.ToCanonicalKey(
            MakeCobertura(
                methodName: "get_Value",
                signature: "()"));
        roslyn.Should().Be(cobertura);
    }

    // === GetNameOnlyKey ===

    [Fact]
    public void NameOnlyKey_StripsSignature()
    {
        MethodKeyHelper.GetNameOnlyKey("MyApp.Service.DoWork(string, int)")
            .Should().Be("MyApp.Service.DoWork");
    }

    [Fact]
    public void NameOnlyKey_EmptyParens()
    {
        MethodKeyHelper.GetNameOnlyKey("MyApp.Service.DoWork()")
            .Should().Be("MyApp.Service.DoWork");
    }

    [Fact]
    public void NameOnlyKey_NoParens_ReturnsAsIs()
    {
        MethodKeyHelper.GetNameOnlyKey("MyApp.Service.DoWork")
            .Should().Be("MyApp.Service.DoWork");
    }
}
