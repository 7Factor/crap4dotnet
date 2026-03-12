using Crap4DotNet.Core.Complexity;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Complexity;

public sealed class CyclomaticComplexityWalkerTests
{
    private static IReadOnlyList<MethodComplexityResult> Analyze(string code) =>
        CyclomaticComplexityWalker.Analyze(code, "Test.cs");

    // === Basic method complexity ===

    [Fact]
    public void SimpleMethod_ComplexityOne()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork() { }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(1);
    }

    [Fact]
    public void MethodWithIf_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(bool flag)
    {
        if (flag) { }
    }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    [Fact]
    public void MethodWithIfElseIf_ComplexityThree()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(int x)
    {
        if (x > 0) { }
        else if (x < 0) { }
        else { }
    }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(3);
    }

    // === Loops ===

    [Fact]
    public void MethodWithFor_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork()
    {
        for (int i = 0; i < 10; i++) { }
    }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    [Fact]
    public void MethodWithForEach_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(int[] items)
    {
        foreach (var item in items) { }
    }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    [Fact]
    public void MethodWithWhile_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork()
    {
        while (true) { break; }
    }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    [Fact]
    public void MethodWithDoWhile_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork()
    {
        do { } while (false);
    }
}");
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    // === Switch ===

    [Fact]
    public void MethodWithSwitchCases_CountsEachCase()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(int x)
    {
        switch (x)
        {
            case 1: break;
            case 2: break;
            case 3: break;
            default: break;
        }
    }
}");
        // base(1) + 3 case labels (default not counted) = 4
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(4);
    }

    [Fact]
    public void MethodWithSwitchExpression_CountsNonDiscardArms()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public string DoWork(int x)
    {
        return x switch
        {
            1 => ""one"",
            2 => ""two"",
            _ => ""other""
        };
    }
}");
        // base(1) + 2 non-discard arms = 3
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(3);
    }

    [Fact]
    public void MethodWithPatternMatchSwitch()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(object obj)
    {
        switch (obj)
        {
            case int i: break;
            case string s: break;
            default: break;
        }
    }
}");
        // base(1) + 2 case pattern labels = 3
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(3);
    }

    // === Logical operators ===

    [Fact]
    public void MethodWithLogicalAnd_ComplexityThree()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(bool a, bool b)
    {
        if (a && b) { }
    }
}");
        // base(1) + if(1) + &&(1) = 3
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(3);
    }

    [Fact]
    public void MethodWithLogicalOr_ComplexityThree()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork(bool a, bool b)
    {
        if (a || b) { }
    }
}");
        // base(1) + if(1) + ||(1) = 3
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(3);
    }

    // === Other decision points ===

    [Fact]
    public void MethodWithTernary_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public int DoWork(bool flag) => flag ? 1 : 0;
}");
        // base(1) + ternary(1) = 2
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    [Fact]
    public void MethodWithCatch_ComplexityTwo()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork()
    {
        try { }
        catch (Exception) { }
    }
}");
        // base(1) + catch(1) = 2
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(2);
    }

    [Fact]
    public void MethodWithMultipleCatches_ComplexityThree()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork()
    {
        try { }
        catch (InvalidOperationException) { }
        catch (Exception) { }
    }
}");
        // base(1) + 2 catches = 3
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(3);
    }

    // === Null-coalescing NOT counted by default ===

    [Fact]
    public void NullCoalescing_NotCountedByDefault()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public string DoWork(string? s) => s ?? ""default"";
}");
        // base(1) only — ?? is OFF by default per spec
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(1);
    }

    // === Complex method with known complexity ===

    [Fact]
    public void ComplexMethod_KnownComplexity()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public int Process(int x, bool flag)
    {
        if (x > 0 && flag)       // if(+1) + &&(+1)
        {
            for (int i = 0; i < x; i++)  // for(+1)
            {
                if (i % 2 == 0)   // if(+1)
                    continue;
            }
        }
        else if (x < 0)          // else if(+1)
        {
            try { }
            catch (Exception) { } // catch(+1)
        }
        return flag ? x : -x;    // ternary(+1)
    }
}");
        // base(1) + 7 = 8
        results.Should().ContainSingle()
            .Which.Complexity.Should().Be(8);
    }

    // === Local functions ===

    [Fact]
    public void LocalFunction_AnalyzedSeparately()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork()
    {
        if (true) { }

        void LocalHelper()
        {
            if (true) { }
            if (true) { }
        }
    }
}");
        results.Should().HaveCount(2);

        var outer = results.First(r => r.Identity.MethodName == "DoWork");
        outer.Complexity.Should().Be(2); // base(1) + if(1), local function NOT counted

        var local = results.First(r => r.Identity.MethodName == "LocalHelper");
        local.Complexity.Should().Be(3); // base(1) + 2 ifs
    }

    // === Partial methods ===

    [Fact]
    public void PartialMethodWithoutBody_Excluded()
    {
        var results = Analyze(@"
namespace MyApp;
public partial class Service
{
    partial void OnInit();
    public void DoWork() { }
}");
        results.Should().ContainSingle()
            .Which.Identity.MethodName.Should().Be("DoWork");
    }

    // === Constructors ===

    [Fact]
    public void Constructor_Analyzed()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public Service(string name)
    {
        if (name == null) throw new ArgumentNullException();
    }
}");
        results.Should().ContainSingle();
        results[0].Identity.MethodName.Should().Be("Service");
        results[0].Complexity.Should().Be(2);
    }

    // === Property accessors ===

    [Fact]
    public void PropertyAccessorWithBody_Analyzed()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    private int _value;
    public int Value
    {
        get { return _value; }
        set { if (value >= 0) _value = value; }
    }
}");
        results.Should().HaveCount(2);
        var getter = results.First(r => r.Identity.MethodName.Contains("get"));
        getter.Complexity.Should().Be(1);
        var setter = results.First(r => r.Identity.MethodName.Contains("set"));
        setter.Complexity.Should().Be(2);
    }

    [Fact]
    public void ExpressionBodiedProperty_AnalyzedAsGetter()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public int Value => 42;
}");
        results.Should().ContainSingle()
            .Which.Identity.MethodName.Should().Contain("get");
    }

    [Fact]
    public void AutoProperty_NotAnalyzed()
    {
        var results = Analyze(@"
namespace MyApp;
public class Service
{
    public int Value { get; set; }
}");
        results.Should().BeEmpty();
    }

    // === Identity building ===

    [Fact]
    public void Identity_FullyQualified()
    {
        var results = Analyze(@"
namespace MyApp.Services;
public class UserService
{
    public void Validate(string name) { }
}");
        var identity = results.Single().Identity;
        identity.Namespace.Should().Be("MyApp.Services");
        identity.ClassName.Should().Be("UserService");
        identity.MethodName.Should().Be("Validate");
        identity.Signature.Should().Be("(string)");
        identity.FullName.Should().Be("MyApp.Services.UserService.Validate(string)");
    }

    [Fact]
    public void Identity_NestedType()
    {
        var results = Analyze(@"
namespace MyApp;
public class Outer
{
    public class Inner
    {
        public void DoWork() { }
    }
}");
        var identity = results.Single().Identity;
        identity.ClassName.Should().Be("Outer.Inner");
        identity.FullName.Should().Be("MyApp.Outer.Inner.DoWork()");
    }

    [Fact]
    public void Identity_GenericType()
    {
        var results = Analyze(@"
namespace MyApp;
public class Cache<T>
{
    public T Get(string key) { return default; }
}");
        var identity = results.Single().Identity;
        identity.ClassName.Should().Be("Cache<T>");
        identity.FullName.Should().Be("MyApp.Cache<T>.Get(string)");
    }

    [Fact]
    public void Identity_GenericMethod()
    {
        var results = Analyze(@"
namespace MyApp;
public class Repo
{
    public T Find<T>(string id) { return default; }
}");
        var identity = results.Single().Identity;
        identity.MethodName.Should().Be("Find<T>");
        identity.FullName.Should().Be("MyApp.Repo.Find<T>(string)");
    }

    [Fact]
    public void Identity_FileScopedNamespace()
    {
        var results = Analyze(@"
namespace MyApp.Services;

public class Svc
{
    public void Run() { }
}");
        results.Single().Identity.Namespace.Should().Be("MyApp.Services");
    }

    [Fact]
    public void Identity_FilePathAndLineNumber()
    {
        var results = CyclomaticComplexityWalker.Analyze(@"
namespace MyApp;
public class Service
{
    public void DoWork() { }
}", "src/Service.cs");

        var identity = results.Single().Identity;
        identity.FilePath.Should().Be("src/Service.cs");
        identity.LineNumber.Should().BeGreaterThan(0);
    }

    // === Top-level statements ===

    [Fact]
    public void TopLevelStatements_AnalyzedAsProgramMain()
    {
        var results = Analyze(@"
System.Console.WriteLine(""Hello"");
if (args.Length > 0) System.Console.WriteLine(args[0]);
");
        var main = results.First(r => r.Identity.MethodName == "<Main>$");
        main.Identity.ClassName.Should().Be("Program");
        main.Identity.FullName.Should().Be("Program.<Main>$(string[])");
        main.Complexity.Should().Be(2); // base(1) + if(1)
    }

    // === Multiple classes ===

    [Fact]
    public void MultipleClasses_AllMethodsFound()
    {
        var results = Analyze(@"
namespace MyApp;
public class Alpha
{
    public void A() { }
}
public class Beta
{
    public void B() { }
    public void C() { }
}");
        results.Should().HaveCount(3);
        results.Select(r => r.Identity.MethodName).Should()
            .Contain("A").And.Contain("B").And.Contain("C");
    }
}
