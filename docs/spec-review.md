# Specification Review: crap4dotnet

**Reviewer**: .NET Architecture Review
**Date**: 2026-03-12
**Documents Reviewed**:
- `01-crap-metric-specification.md`
- `02-gap-analysis.md`
- `03-implementation-plan.md`

---

## 1. Completeness

### What's Strong
- The core CRAP formula, CRAP Load, aggregation metrics, and severity bands are fully specified with worked examples.
- The gap analysis is excellent — it makes deliberate, well-reasoned decisions about what to drop from Java, what maps directly, and what's .NET-specific.
- The implementation plan has a clear project structure, phased delivery, concrete CLI examples, a config file schema, and performance targets.
- The JSON report schema is concrete enough to implement and parse.

### What's Missing

**M1. No specification for the `diff` command output.** The CLI shows `dotnet crap diff before.json after.json` but there is no schema, example output, or description of what the diff contains. Does it show added/removed methods? Delta CRAP scores? Regressions vs improvements? An AI agent consuming this needs a defined contract.

**M2. No error output schema.** Exit code 2 means "error," but what goes to stderr/stdout? AI agents need structured error output (JSON with error codes?) not just human-readable messages. The spec says "agent-oriented" but only specifies the happy path output.

**M3. Methods with complexity data but no coverage data (and vice versa).** The pipeline joins complexity and coverage by method identity, but the spec never defines what happens when:
- A method has complexity but no coverage entry (test project doesn't cover it)
- A coverage entry exists but the method wasn't found in the source analysis
- The coverage file is for a different version of the code than the source

The config has `"defaultForUncovered": 0.0` which implies a default, but this critical behavior deserves explicit specification, not just a config default.

**M4. No specification for hierarchical aggregation in the JSON output.** Section 5.2 says stats "SHOULD be computable" at class/namespace/assembly/solution levels, but the JSON schema (Section 7.1) only shows a flat `methods` array and project-level `stats`. Where do the class-level and namespace-level aggregations appear? Are they separate objects in the JSON? Omitted? This is an ambiguity that will produce different implementations.

**M5. Multi-project solutions.** The CLI accepts `.sln` files. How are results organized? One report per project? One merged report? How are coverage files mapped to projects when there are multiple test projects? The auto-discovery of `coverage.cobertura.xml` in `TestResults/` gets complicated with multiple test projects.

**M6. No versioning of the JSON schema.** The report should include a `"schemaVersion"` field so that consuming agents can handle format evolution. This is especially important for a tool designed for machine consumption.

---

## 2. Correctness

### Formula Issues

**C1. CRAP Load formula is suspicious and likely wrong for its stated purpose.** The formula is:
```
crapLoad(m) = comp(m) * (1 - cov(m)) + comp(m) / threshold
```

The stated purpose is "effort required to bring a CRAPpy method back under the threshold." But `comp/threshold` is a constant ratio that doesn't relate to the effort of adding tests or reducing complexity. For a method with complexity=60, coverage=0.0, threshold=30:
- `crapLoad = 60 * 1.0 + 60/30 = 62`

But for the same method with coverage=0.5:
- `crapLoad = 60 * 0.5 + 60/30 = 32`

The `comp/threshold` term is just 2.0 in both cases — it doesn't scale with the actual gap. This formula is inherited from crap4j and may be intentionally preserved for compatibility, but it should be documented that it's a heuristic, not a rigorous effort estimate.

**C2. The test validation table has errors.** The table in Section 7.1 of the implementation plan states:

| Complexity | Coverage | Expected CRAP | Expected Load |
|---|---|---|---|
| 5 | 0.0 | 30.0 | 0 (exactly at threshold) |
| 30 | 1.0 | 30.0 | 0 (exactly at threshold) |

Let me verify: `CRAP(5, 0.0) = 5^2 * (1-0)^3 + 5 = 25 + 5 = 30`. Correct, and it equals the threshold, so Load = 0 because the condition is `>=` threshold, meaning it IS CRAPpy. But the table says Load = 0 and "exactly at threshold." If `>=` triggers CRAP Load, then:
- `crapLoad(5, 0.0) = 5 * 1.0 + 5/30 = 5.17` — NOT 0

This is a critical inconsistency. The CRAP Load formula says "if CRAP(m) >= threshold" then compute load. The table says "exactly at threshold" but shows Load = 0. Either the condition should be `>` (strictly greater), or the expected values are wrong. **This must be resolved before implementation.**

**C3. `CRAP(30, 1.0) = 30^2 * 0^3 + 30 = 0 + 30 = 30`.** The observation in section 2.3 says "Complexity of 31+ cannot be saved by coverage alone" because CRAP(31, 1.0) = 31. But this means CRAP(30, 1.0) = 30 which is exactly at threshold. Same ambiguity as C2: is `>=` or `>` the threshold comparison?

---

## 3. Ambiguities

**A1. "else if" double-counting risk.** The complexity formula says count `IfStatementSyntax` and also count `ElseClauseSyntax with if`. In the Roslyn AST, `else if (x)` is parsed as an `ElseClause` containing an `IfStatement`. If you walk the tree and count all `IfStatementSyntax` nodes, the `if` inside an `else` clause is already counted. Adding `ElseClauseSyntax with if` would double-count it. The spec needs to clarify: are you counting `IfStatementSyntax` (which includes `else if`) and then NOT separately counting `ElseClauseSyntax`? Or are you only counting top-level `if` nodes? This will produce wrong complexity scores if misread.

**A2. Switch expression arms "minus 1" is fragile.** The formula says `count(SwitchExpressionArmSyntax) - 1` with the comment "minus default." But:
- What if there is no discard arm (`_`)? Then you're subtracting 1 for a default that doesn't exist.
- What if there are multiple discard-like arms via pattern matching?
- The spec should say "count non-discard arms" rather than "count all arms minus 1."

The testing sample in Section 7.2 confirms the confusion: it says "Expected complexity: 6" for a switch expression with 4 non-default arms, then adds the comment "1 base + 4 non-default arms + 1 for switch = depends on config." If it "depends on config" then the expected value should not be stated as 6 without specifying which config.

**A3. Coverage: branch-rate vs line-rate.** The Cobertura XML example shows both `line-rate` and `branch-rate`. The metric spec says branch coverage is preferred. But the spec never says definitively WHICH field to read from the Cobertura XML, or what to do when branch-rate is 0 but line-rate is 0.8 (common for methods with no branches). Implementers will make different choices here.

**A4. "Configurable" is under-specified.** Several complexity rules are marked "configurable" (`?.`, `??`, LINQ, `default` case) but the interaction between these configurations and the test validation table is not addressed. The validation table assumes specific configuration — which configuration? The default? This needs a note like "all validation values assume the default configuration."

**A5. `--quiet` semantics.** The spec says `--quiet` means "JSON to stdout only, no human-readable text." But what is the default behavior? JSON to stdout WITH human-readable text? Human-readable text to stdout and JSON only with `--format json`? The relationship between `--quiet`, `--format`, and default output behavior is ambiguous.

---

## 4. Edge Cases

**E1. Complexity = 0.** What CRAP score should a method with complexity 0 get? The formula gives `0^2 * X + 0 = 0`. The spec says such methods "should be excluded" but doesn't say what happens if the filter is disabled or the method slips through. Division by zero isn't an issue here, but a CRAP score of 0 is lower than the minimum of 1 stated in section 2.3.

**E2. Coverage > 1.0 or < 0.0.** Malformed coverage data could produce out-of-range values. The spec should specify clamping behavior.

**E3. Extremely high complexity.** `comp=1000, cov=0.0` gives `CRAP = 1,000,000 + 1000 = 1,001,000`. Is this representable? Are there overflow concerns? (Not with `double`, but worth stating.)

**E4. NaN/Infinity.** No division in the CRAP formula, but CRAP Load has `comp/threshold`. If threshold is configured to 0, you get division by zero. The spec should disallow threshold <= 0 or specify behavior.

**E5. Empty projects / no methods found.** What does the JSON output look like for a project with no analyzable methods? Empty `methods` array? Is `methodCount: 0` valid? What are `averageCrap` and `medianCrap` when there are no methods — 0? NaN? null?

**E6. Partial methods (C#).** Partial methods where only the declaration exists (no implementation) have no body. Are they excluded? The spec mentions abstract/interface/empty methods but not partial methods.

**E7. Local functions.** C# supports local functions (functions nested inside methods). Are they analyzed as separate methods? Folded into the parent method's complexity? This is a significant C#-specific construct that the spec doesn't address.

**E8. Top-level statements.** C# 9+ allows top-level statements (no explicit Main method). How is this method identified? What's its name in the report?

---

## 5. Testability

### Strengths
- The formula validation table (Section 7.1 of implementation plan) provides concrete input/output pairs that can be directly turned into parameterized tests — once the threshold boundary issue (C2) is resolved.
- The complexity walker examples in Section 7.2 provide testable C# snippets with expected values.
- Exit codes are clearly defined (0, 1, 2) which makes E2E testing straightforward.

### Weaknesses
- **No test cases for method identity matching.** This is identified as "the trickiest part" but has zero concrete test scenarios. What specific Cobertura XML + Roslyn pairs should match? What about generic methods, nested types, operators?
- **No negative/failure test cases.** What does the tool output for corrupt XML? Missing files? Non-C# files in the solution?
- **The severity bands and histogram have no test scenarios.** No example of what the histogram output looks like.
- **No test scenarios for the diff command at all.**

---

## 6. Architecture

### Strengths
- Clean separation of concerns: Core (formula), Complexity (Roslyn), Coverage (readers), Reporting (writers), CLI.
- Interfaces for the extensibility points that matter (`IComplexityAnalyzer`, `ICoverageReader`, `IReportWriter`).
- The decision to analyze source (Roslyn) instead of IL is correct for C# — it avoids the async state machine problem and matches what developers see.
- Minimal dependency philosophy is good for a CLI tool (fast startup, small binary).
- `System.CommandLine` is the right choice for a .NET CLI tool.

### Concerns

**AR1. `Microsoft.CodeAnalysis.Workspaces.MSBuild` is heavy.** This package pulls in MSBuild and the entire workspace infrastructure. For a tool whose primary use case is "given source files and a coverage XML, compute scores," this is significant overhead. Consider whether the tool can work with syntax-tree-only analysis (no MSBuild workspace) as the default fast path, falling back to workspace loading only when given a `.sln`/`.csproj`. The spec mentions "Lazy Roslyn compilation (only parse syntax trees)" in Phase 4 but this should be the design from the start.

**AR2. No DI container specified.** The implementation plan lists `Crap4DotNet.Core` with interfaces and mentions DI lifetimes but doesn't specify whether to use `Microsoft.Extensions.DependencyInjection` or keep it simple with manual composition. For a CLI tool, manual composition in `Program.cs` (poor-man's DI) is often better — faster startup, fewer dependencies, easier to understand. The spec should state this preference explicitly.

**AR3. The `Crap4DotNet.Coverage` project has `MethodCoverageMatcher` doing the join.** But the join logic requires knowledge of both coverage AND complexity naming conventions. This cross-cutting concern doesn't naturally belong in the Coverage project. Consider a separate `Crap4DotNet.Analysis` or put the matching logic in `Core`.

---

## 7. Executable Specifications

**Yes, I can write executable specifications for this project.**

### Recommended Framework/Approach

**SpecFlow with xUnit** is the most widely supported BDD framework in the .NET ecosystem for Gherkin-style specifications. However, given this project's "minimal dependencies" philosophy and the fact that it targets AI agent consumption, I would recommend one of two approaches:

**Option A: SpecFlow + xUnit (Gherkin features)**
- Most widely adopted .NET BDD framework
- Full Gherkin syntax support (Given/When/Then)
- Generates human-readable living documentation
- Integrates with standard test runners
- NuGet: `SpecFlow.xUnit`

**Option B: xUnit with `[Theory]` + inline test data (no BDD framework)**
- Zero additional dependencies
- Parameterized tests with `[InlineData]` serve as executable specification tables
- Test method names serve as specification statements
- More idiomatic for .NET; avoids the Gherkin-to-C# mapping overhead
- Better suited for a project that explicitly targets non-human consumers

Given the project's philosophy, **Option B is my recommendation** — use well-named xUnit `[Theory]` tests organized into specification-style test classes. The formula validation table is a natural `[Theory]` with `[InlineData]` rows. The complexity walker examples map to parameterized tests with embedded C# source snippets.

### What the Executable Specifications Would Cover

1. **CRAP Formula Specification** — parameterized tests for every row in the validation table, plus edge cases (complexity=0, coverage boundaries, threshold boundaries)
2. **CRAP Load Specification** — same pattern, with explicit tests for the threshold boundary behavior (resolving the `>=` vs `>` ambiguity)
3. **Cyclomatic Complexity Specification** — C# source snippets as test data, each with expected complexity, covering every syntax node type listed in the spec
4. **Configurable Complexity Rules** — same snippets tested with different configuration flags (`countNullCoalesce: true/false`, etc.)
5. **Method Identity Matching Specification** — pairs of (Cobertura XML fragment, Roslyn method symbol string) with expected match/no-match results
6. **Severity Classification Specification** — CRAP scores mapped to expected severity bands
7. **Aggregation Specification** — sets of method results with expected project-level statistics
8. **CLI Integration Specification** — end-to-end tests with sample projects, expected exit codes, expected JSON structure
9. **Diff Command Specification** — pairs of reports with expected diff output
10. **Error Handling Specification** — malformed inputs, missing files, invalid configurations with expected error behavior

---

## Summary Assessment

### Overall Quality
The specifications are **above average for a pre-implementation design document**. The three documents work well together: the metric spec provides the mathematical foundation, the gap analysis makes deliberate architectural choices, and the implementation plan gives concrete technical direction. The project is well-scoped with a clear value proposition (AI agent consumption).

### Top 3 Most Impactful Changes to Prioritize

1. **Resolve the CRAP threshold boundary semantics** (C2/C3). The `>=` vs `>` ambiguity affects the formula validation table, CRAP Load calculation, and the mathematical properties stated in Section 2.3. Every downstream implementation and test depends on this decision. Pick one, state it clearly, and fix the validation table.

2. **Specify method identity matching with concrete test cases** (M3, testability weakness). This is the highest-risk implementation area by the spec's own admission ("the trickiest part"), yet it has the least specification. Provide 10-15 concrete (Cobertura XML, Roslyn symbol) pairs covering generics, nested types, properties, operators, explicit interface implementations, and overloaded methods.

3. **Clarify the "else if" complexity counting** (A1). This will produce incorrect CRAP scores for a large percentage of real-world methods if implementers read the spec differently. Replace the pseudocode with an explicit Roslyn-aware algorithm: "count every `IfStatementSyntax` node regardless of whether it appears inside an `ElseClause`; do NOT separately count `ElseClauseSyntax`."

### Readiness Assessment
The specs are **ready for a knowledgeable implementer to begin Phase 1** (core formula + complexity walker + Cobertura reader), provided the three issues above are resolved first. Phase 2 (CLI) needs more specification work around the diff command, error output, and the quiet/format flag interaction. Phases 3-4 are reasonably scoped as enhancement work on a solid foundation.
