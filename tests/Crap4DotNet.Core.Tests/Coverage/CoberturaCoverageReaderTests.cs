using Crap4DotNet.Core.Coverage;
using FluentAssertions;
using Xunit;

namespace Crap4DotNet.Core.Tests.Coverage;

public sealed class CoberturaCoverageReaderTests
{
    private static string WrapInCobertura(string methodXml, string className = "MyApp.Service") =>
        $@"<?xml version=""1.0"" encoding=""utf-8""?>
<coverage line-rate=""0"" branch-rate=""0"" version=""1.0"">
  <packages>
    <package name=""MyApp"">
      <classes>
        <class name=""{className}"" filename=""Service.cs"" line-rate=""0"" branch-rate=""0"">
          <methods>
            {methodXml}
          </methods>
        </class>
      </classes>
    </package>
  </packages>
</coverage>";

    private static string MakeMethod(
        string name = "DoWork",
        string signature = "()",
        string? branchRate = null,
        string? lineRate = null,
        int conditionCount = 0)
    {
        var brAttr = branchRate is not null ? $@" branch-rate=""{branchRate}""" : "";
        var lrAttr = lineRate is not null ? $@" line-rate=""{lineRate}""" : "";

        var lines = "";
        if (conditionCount > 0)
        {
            var conditions = string.Join("\n",
                Enumerable.Range(0, conditionCount)
                    .Select(i => $@"<condition number=""{i}"" type=""jump"" coverage=""50%""/>"));

            lines = $@"<lines>
                <line number=""10"" hits=""1"" branch=""True"">
                    <conditions>{conditions}</conditions>
                </line>
            </lines>";
        }
        else
        {
            lines = @"<lines><line number=""10"" hits=""1"" branch=""False""/></lines>";
        }

        return $@"<method name=""{name}"" signature=""{signature}""{brAttr}{lrAttr}>
            {lines}
        </method>";
    }

    // === Spec 6.2 table — all 8 coverage selection scenarios ===

    [Fact]
    public void HasBranches_UsesBranchRate()
    {
        // branch-rate=0.75, line-rate=0.90, conditions=3 → 0.75
        var xml = WrapInCobertura(MakeMethod(branchRate: "0.75", lineRate: "0.90", conditionCount: 3));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.75);
    }

    [Fact]
    public void Branchless_UsesLineRate()
    {
        // branch-rate=0.0, line-rate=1.0, conditions=0 → 1.0
        var xml = WrapInCobertura(MakeMethod(branchRate: "0", lineRate: "1"));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(1.0);
    }

    [Fact]
    public void HasBranches_NoneCovered_UseBranchRateZero()
    {
        // branch-rate=0.0, line-rate=0.5, conditions=2 → 0.0
        var xml = WrapInCobertura(MakeMethod(branchRate: "0", lineRate: "0.5", conditionCount: 2));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.0);
    }

    [Fact]
    public void HasBranches_NoneCovered_LinesFullyCovered()
    {
        // branch-rate=0.0, line-rate=1.0, conditions=4 → 0.0
        var xml = WrapInCobertura(MakeMethod(branchRate: "0", lineRate: "1", conditionCount: 4));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.0);
    }

    [Fact]
    public void BranchRateAbsent_FallbackToLineRate()
    {
        // branch-rate=absent, line-rate=0.80 → 0.80
        var xml = WrapInCobertura(MakeMethod(lineRate: "0.80"));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.80);
    }

    [Fact]
    public void HasBranches_UseBranchRate_EvenWhenLineRateZero()
    {
        // branch-rate=0.50, line-rate=0.0, conditions=1 → 0.50
        var xml = WrapInCobertura(MakeMethod(branchRate: "0.50", lineRate: "0", conditionCount: 1));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.50);
    }

    [Fact]
    public void BothAbsent_ReturnsZero()
    {
        // branch-rate=absent, line-rate=absent → 0.0
        var xml = WrapInCobertura(MakeMethod());
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.0);
    }

    [Fact]
    public void Branchless_BranchRateOneIsNoise_UsesLineRate()
    {
        // branch-rate=1.0, line-rate=0.80, conditions=0 → 0.80
        var xml = WrapInCobertura(MakeMethod(branchRate: "1", lineRate: "0.80"));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.80);
    }

    // === Identity parsing ===

    [Fact]
    public void ParsesClassName()
    {
        var xml = WrapInCobertura(MakeMethod(lineRate: "0.5"), "MyApp.Services.UserService");
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.ClassName.Should().Be("MyApp.Services.UserService");
    }

    [Fact]
    public void ParsesMethodNameAndSignature()
    {
        var xml = WrapInCobertura(
            MakeMethod(name: "Validate", signature: "(System.String)", lineRate: "0.5"));
        var result = CoberturaCoverageReader.Read(xml).Single();
        result.MethodName.Should().Be("Validate");
        result.Signature.Should().Be("(System.String)");
    }

    [Fact]
    public void ParsesFileName()
    {
        var xml = WrapInCobertura(MakeMethod(lineRate: "0.5"));
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().ContainSingle().Which.FileName.Should().Be("Service.cs");
    }

    // === Multiple methods and classes ===

    [Fact]
    public void MultipleMethods_AllParsed()
    {
        var methods = MakeMethod(name: "A", lineRate: "0.5") + "\n" +
                      MakeMethod(name: "B", lineRate: "0.8") + "\n" +
                      MakeMethod(name: "C", lineRate: "1.0");
        var xml = WrapInCobertura(methods);
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().HaveCount(3);
        results.Select(r => r.MethodName).Should().Equal("A", "B", "C");
    }

    [Fact]
    public void MultipleClasses_AllParsed()
    {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<coverage>
  <packages>
    <package name=""MyApp"">
      <classes>
        <class name=""MyApp.Alpha"" filename=""Alpha.cs"">
          <methods>
            <method name=""Go"" signature=""()"" line-rate=""0.5"">
              <lines><line number=""1"" hits=""1"" branch=""False""/></lines>
            </method>
          </methods>
        </class>
        <class name=""MyApp.Beta"" filename=""Beta.cs"">
          <methods>
            <method name=""Run"" signature=""()"" line-rate=""0.9"">
              <lines><line number=""1"" hits=""1"" branch=""False""/></lines>
            </method>
          </methods>
        </class>
      </classes>
    </package>
  </packages>
</coverage>";
        var results = CoberturaCoverageReader.Read(xml);
        results.Should().HaveCount(2);
        results.Select(r => r.ClassName).Should().Contain("MyApp.Alpha").And.Contain("MyApp.Beta");
    }

    // === Stream overload ===

    [Fact]
    public void ReadFromStream_Works()
    {
        var xml = WrapInCobertura(MakeMethod(lineRate: "0.75"));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var results = CoberturaCoverageReader.Read(stream);
        results.Should().ContainSingle().Which.Coverage.Should().Be(0.75);
    }

    // === Empty/edge cases ===

    [Fact]
    public void EmptyXml_NoMethods()
    {
        var xml = @"<?xml version=""1.0""?><coverage><packages></packages></coverage>";
        CoberturaCoverageReader.Read(xml).Should().BeEmpty();
    }

    private static CoberturaLine Line(int number, bool hit, int branchesCovered = 0, int branchesTotal = 0) =>
        new() { Number = number, Hit = hit, BranchesCovered = branchesCovered, BranchesTotal = branchesTotal };

    [Fact]
    public void Lines_ReadHitsAndBranchCounts()
    {
        var xml = WrapInCobertura(@"<method name=""DoWork"" signature=""()"" line-rate=""0.5"" branch-rate=""0.5"">
            <lines>
                <line number=""10"" hits=""3"" branch=""True"" condition-coverage=""50% (1/2)"">
                    <conditions><condition number=""0"" type=""jump"" coverage=""50%""/></conditions>
                </line>
                <line number=""11"" hits=""0"" branch=""False""/>
            </lines>
        </method>");

        CoberturaCoverageReader.Read(xml).Single().Lines.Should().Equal(Line(10, true, 1, 2), Line(11, false));
    }

    [Fact]
    public void Lines_AreOmittedWhenABranchLineHasNoCounts()
    {
        var xml = WrapInCobertura(MakeMethod(branchRate: "0.5", lineRate: "1.0", conditionCount: 2));

        CoberturaCoverageReader.Read(xml).Single().Lines.Should().BeEmpty();
    }

    [Fact]
    public void Lines_IgnoreBranchCountsWhenBranchRateIsMissing()
    {
        var xml = WrapInCobertura(@"<method name=""DoWork"" signature=""()"" line-rate=""1"">
            <lines>
                <line number=""10"" hits=""1"" branch=""True"" condition-coverage=""50% (1/2)"">
                    <conditions><condition number=""0"" type=""jump"" coverage=""50%""/></conditions>
                </line>
            </lines>
        </method>");

        CoberturaCoverageReader.Read(xml).Single().Lines.Should().Equal(Line(10, true));
    }

    [Fact]
    public void Lines_AcceptHitCountsAboveIntMaxValue()
    {
        var xml = WrapInCobertura(@"<method name=""DoWork"" signature=""()"" line-rate=""1"">
            <lines><line number=""10"" hits=""3000000000"" branch=""False""/></lines>
        </method>");

        CoberturaCoverageReader.Read(xml).Single().Lines.Should().Equal(Line(10, true));
    }

    [Fact]
    public void Lines_FromRealCoverletOutput_ReproduceTheReportedRate()
    {
        // Taken from Coverlet output for this repo; the reported branch-rate is 0.5.
        var xml = WrapInCobertura(@"<method name=""Scan"" signature=""(System.ReadOnlySpan`1&lt;System.Char&gt;)"" line-rate=""0.41659999999999997"" branch-rate=""0.5"" complexity=""8"">
              <lines>
                <line number=""159"" hits=""6"" branch=""False"" />
                <line number=""161"" hits=""6"" branch=""True"" condition-coverage=""66.66% (4/6)"">
                  <conditions>
                    <condition number=""78"" type=""jump"" coverage=""50%"" />
                    <condition number=""44"" type=""jump"" coverage=""100%"" />
                    <condition number=""53"" type=""jump"" coverage=""50%"" />
                  </conditions>
                </line>
                <line number=""162"" hits=""6"" branch=""False"" />
                <line number=""163"" hits=""6"" branch=""False"" />
                <line number=""164"" hits=""0"" branch=""False"" />
                <line number=""165"" hits=""0"" branch=""False"" />
                <line number=""166"" hits=""0"" branch=""True"" condition-coverage=""0% (0/2)"">
                  <conditions>
                    <condition number=""25"" type=""jump"" coverage=""0%"" />
                  </conditions>
                </line>
                <line number=""167"" hits=""0"" branch=""False"" />
                <line number=""168"" hits=""0"" branch=""False"" />
                <line number=""169"" hits=""0"" branch=""False"" />
                <line number=""170"" hits=""0"" branch=""False"" />
                <line number=""171"" hits=""6"" branch=""False"" />
              </lines>
            </method>");

        var entry = CoberturaCoverageReader.Read(xml).Single();

        CoverageRate.FromLines(entry.Lines).Should().Be(entry.Coverage);
    }

    [Fact]
    public void FileName_IsResolvedAgainstTheSourceRoot()
    {
        var xml = @"<?xml version=""1.0""?>
<coverage>
  <sources><source>/repo/src/</source></sources>
  <packages><package name=""MyApp""><classes>
    <class name=""MyApp.Service"" filename=""Core/Service.cs"">
      <methods><method name=""DoWork"" signature=""()"" line-rate=""1""><lines/></method></methods>
    </class>
  </classes></package></packages>
</coverage>";

        CoberturaCoverageReader.Read(xml).Single().FileName.Should().Be("/repo/src/Core/Service.cs");
    }
}
