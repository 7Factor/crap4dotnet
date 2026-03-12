# Specification Review #3: Clean Code & Software Craftsmanship

**Reviewer:** Uncle Bob (Robert C. Martin) perspective — Claude Opus 4.6
**Date:** 2026-03-12
**Scope:** Architecture, SOLID compliance, testability, naming, separation of concerns across all four spec documents

---

## Verdict

This is a well-considered specification suite. The two-project structure, manual composition, syntax-tree-only Roslyn analysis, and JSON-only output are all *disciplined* decisions that show respect for simplicity. The prior architecture review (spec-review-2) caught genuine contradictions that have been resolved. What follows are the craftsmanship concerns that remain — the things that will bite you at implementation time if you don't address them now.

---

## 1. The Missing Orchestrator — A Single Responsibility Problem

**Severity: High**

The spec explicitly states: *"There is no separate AnalysisPipeline abstraction. The CLI layer owns the full analysis pipeline."* The pipeline is described as: parse source -> compute complexity -> read coverage -> match methods -> calculate CRAP -> generate report.

I understand the reasoning — it's a "straight-line sequence with no branching." But here's the problem: that sequence *is* the core use case of your application. It is the *policy*. And you've pushed it into the *mechanism* layer (the CLI).

This violates the **Dependency Rule** from Clean Architecture. Your highest-level policy — "given source code and coverage data, produce a CRAP report" — should live in Core, not in a command handler. The CLI should be a thin delivery mechanism that calls a single method on a use-case object.

Consider a class like:

```csharp
// In Crap4DotNet.Core
public class CrapAnalyzer
{
    public CrapAnalyzer(
        IComplexityAnalyzer complexityAnalyzer,
        ICoverageReader coverageReader,
        MethodCoverageMatcher matcher,
        CrapCalculator calculator) { ... }

    public ProjectCrapData Analyze(AnalysisRequest request) { ... }
}
```

The CLI's `AnalyzeCommand` then becomes a one-liner that constructs the request and calls `Analyze()`. The report *writing* stays in the CLI (it's a delivery concern). But the analysis pipeline? That's policy. It belongs in Core.

**Why this matters for testing:** Without this, your only way to integration-test the full pipeline is through the CLI — process invocation, stdout capture, exit code parsing. That's slow and brittle. With an orchestrator in Core, you write fast in-process tests against `CrapAnalyzer.Analyze()`.

**Recommendation:** Add `CrapAnalyzer` (or `AnalysisPipeline`) to Core. It takes interfaces, returns a model. The CLI calls it and serializes.

---

## 2. JsonReportWriter in Core — Wrong Side of the Boundary

**Severity: Medium**

The project structure places `JsonReportWriter` inside `Crap4DotNet.Core/Reporting/`. But JSON serialization is a *delivery format* concern. Core should produce `ProjectCrapData` (a model). The CLI should serialize it to JSON.

Putting the writer in Core creates a coupling: if you ever add a different output format (SARIF, for instance, which agent tooling increasingly expects), you'd modify Core for a delivery concern. That's an OCP violation.

The `IReportWriter` interface is fine in Core — it's an abstraction. But the `JsonReportWriter` implementation belongs in the CLI project (or a thin Reporting project if you prefer). The Dependency Rule says: source code dependencies point inward. A JSON serializer is an outer-ring detail.

**Recommendation:** Move `JsonReportWriter` to `Crap4DotNet.Cli`. Keep `IReportWriter` and the data models in Core.

---

## 3. MethodIdentityNormalizer — A Name That Hides Complexity

**Severity: Medium**

The spec identifies method identity matching as *"the trickiest part"* and then specifies a normalizer with two methods: `NormalizeFromCobertura()` and `NormalizeFromRoslyn()`. This is a code smell waiting to happen.

Two normalization paths converging to a canonical form means the *canonical form itself* must be precisely defined. The spec lists eight transformation rules (primitive types, generic arity, property accessors, operators, explicit interfaces, nested types, etc.) but doesn't define the canonical format as a grammar or by example for each rule.

More concerning: `MethodIdentityNormalizer` is doing two very different things — parsing Cobertura XML name conventions and parsing Roslyn display strings. That's two reasons to change. **SRP violation.**

**Recommendation:**
- Define the canonical `MethodIdentity` string format explicitly (a grammar or 20+ canonical examples covering every transformation).
- Split into `CoberturaMethodParser` and `RoslynMethodParser`, each returning a `MethodIdentity`. The identity itself handles equality. The parsers are independent and separately testable.

---

## 4. Configurable Complexity Rules — Open/Closed Done Right (Almost)

**Severity: Low-Medium**

The spec's configurable complexity counting (`?.`, `??`, LINQ, pattern match arms, catch blocks) is a good instinct — it's the Open/Closed Principle applied to the complexity walker. But the implementation approach is unspecified. If these end up as boolean flags checked inside `CyclomaticComplexityWalker.VisitIfStatement()`, you'll have a combinatorial mess of conditionals inside your walker.

**Recommendation:** Use a Strategy or Visitor composition pattern. The walker should have a `IReadOnlyList<IDecisionPointRule>` where each rule inspects a `SyntaxNode` and returns whether it's a decision point. Adding a new rule means adding a new class, not modifying the walker. *That's* OCP.

```csharp
public interface IDecisionPointRule
{
    bool IsDecisionPoint(SyntaxNode node);
}
```

The walker iterates rules per node. Configuration selects which rules are active. The walker itself never changes.

---

## 5. Testing Strategy — Good Bones, Missing Muscle

**Severity: Medium**

The testing strategy specifies three levels: formula validation, complexity validation, and E2E. The formula validation is rock-solid — known input/output pairs against crap4j. Good.

But I see gaps:

**5a. No contract tests for the interfaces.** You've defined `IComplexityAnalyzer`, `ICoverageReader`, `IReportWriter`. Where are the contract tests that verify any implementation of these interfaces behaves correctly? When someone writes `OpenCoverReader` in Phase 4, what test suite validates it?

**5b. The MethodCoverageMatcher is undertested in the spec.** Section 6.4.3 has three join scenarios (partial, no, orphaned coverage). But the *matching logic* — the normalization gauntlet of eight transformation rules — needs combinatorial test cases. The spec's section 6.2.1 has five method identity test cases. You need fifty. Every combination of (generic type, nested class, property accessor, operator overload, explicit interface) needs a test pair: "Cobertura says X, Roslyn says Y, they should match."

**5c. No property-based / fuzz testing mentioned.** For a formula-driven tool, property-based tests are gold: "for any complexity >= 1 and 0 <= coverage <= 1, CRAP score >= complexity" is an invariant. "CRAP is monotonically decreasing in coverage for fixed complexity" is another. These catch edge cases that example-based tests miss.

**Recommendation:** Add a "contract test" section for each interface. Expand method identity matching tests to a matrix. Consider property-based tests for the formula (FsCheck or similar).

---

## 6. CrapOptions — A Bag of Settings Waiting to Grow

**Severity: Low**

`CrapOptions` holds threshold, severity bands, complexity configuration, coverage configuration, output configuration, filters, and exit code behavior. That's at least five different concerns in one class. Right now it's manageable because it's v1 with CLI flags only. But the v2 config file schema (already drafted) shows this becoming a God Object.

**Recommendation:** Split early: `ThresholdOptions`, `ComplexityOptions`, `CoverageOptions`, `FilterOptions`. Each is a small value object passed to the component that needs it. `CrapCalculator` takes `ThresholdOptions`. `CyclomaticComplexityWalker` takes `ComplexityOptions`. Nobody takes the whole bag.

---

## 7. Error Handling — Structured and Clean (Praise)

This is worth calling out as **done well**. Structured JSON errors to stderr with error codes, separate warning codes, exit code semantics — this is exactly what a machine-readable tool needs. The prior review caught the empty-project contradiction, and it's been resolved cleanly (SOURCE_NOT_FOUND vs NO_ANALYZABLE_METHODS). The error handling test scenarios in section 8.5 are specific and testable.

One small note: the spec doesn't say what happens when *multiple* errors occur in a single run (e.g., three source files fail to parse out of fifty). Is it fail-fast? Collect-and-report? The answer matters for agent consumers who need to know whether a partial result is trustworthy.

**Recommendation:** Specify fail-fast vs. best-effort semantics. For agent consumers, best-effort with a `"warnings"` array in the output (already specified) plus a `"partialResult": true` flag would be most useful.

---

## 8. Naming Review

Overall naming is strong. A few observations:

| Current Name | Concern | Suggestion |
|---|---|---|
| `MethodCoverageMatcher` | "Matcher" is vague — it performs a left-outer-join | `CoverageJoiner` or `MethodCoverageJoiner` |
| `MethodDiscovery` | Noun, not a verb or agent noun. What does it do? | `MethodFinder` or `MethodEnumerator` |
| `CrapStatistics` | Computes *and* holds statistics — two roles | `StatisticsCalculator` (computes) + data in the model |
| `CrapOptions` | "Options" is generic | Split per concern (see item 6) |
| `MethodCrapData` | Good — says exactly what it is | Keep |
| `CrapCalculator` | Good — single responsibility, clear name | Keep |
| `CyclomaticComplexityWalker` | Good — reveals mechanism and purpose | Keep |

---

## 9. The Two-Project Decision — Exactly Right

I want to explicitly praise the collapse from five projects to two (plus tests). The prior review caught that Complexity, Coverage, and Reporting each had 1-2 public types. Separate assemblies for those would be ceremony masquerading as architecture. Two projects — Core (policy) and Cli (mechanism) — is the *minimum viable boundary* that still enforces the Dependency Rule at compile time.

Manual composition is also the right call. A DI container for a CLI tool that constructs its graph once and runs is like hiring a moving company to rearrange one chair.

---

## 10. What's Missing From the Spec

**10a. No cancellation story.** For large solutions, analysis might take minutes. Is there `CancellationToken` threading? Agent consumers may timeout. This should be a first-class concern.

**10b. No logging/tracing specification.** The spec has warnings in the JSON output, but during analysis, where does diagnostic info go? If the Cobertura XML is malformed on line 4,712, how does the developer (or agent) debug it? Consider structured logging to stderr with `--verbose`.

**10c. No idempotency guarantee.** Given the same inputs, does the tool produce byte-identical output? (Timestamps in the report would break this.) Agent consumers may hash outputs for caching. Specify whether the output is deterministic.

---

## Summary: Priority-Ordered Action Items

| # | Issue | Impact | Effort |
|---|---|---|---|
| 1 | Add `CrapAnalyzer` orchestrator to Core | High — testability, architecture | Small |
| 2 | Move `JsonReportWriter` to Cli | Medium — boundary correctness | Trivial |
| 3 | Split `MethodIdentityNormalizer` into two parsers | Medium — SRP, testability | Small |
| 4 | Define canonical MethodIdentity format explicitly | Medium — correctness | Small |
| 5 | Expand method identity matching test matrix | Medium — correctness | Medium |
| 6 | Use rule composition for configurable complexity | Low-Med — OCP | Small |
| 7 | Split `CrapOptions` into focused value objects | Low — maintainability | Trivial |
| 8 | Specify multi-error semantics (fail-fast vs best-effort) | Low-Med — agent UX | Trivial |
| 9 | Add property-based formula tests | Low — robustness | Small |
| 10 | Add cancellation token support | Low — large solution UX | Small |
| 11 | Specify output determinism | Low — agent caching | Trivial |

---

*The bones are sound. The decisions about what to leave out (DI container, XML output, MSBuild workspace, config files in v1) show more discipline than most specs I see. Now sharpen the boundaries, name the canonical forms, and make sure the testing strategy matches the ambition of the design. Build it clean from the first commit, because you never get a second chance to make a first impression on your codebase.*
