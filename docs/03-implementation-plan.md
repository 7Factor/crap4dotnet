# Implementation Plan: crap4dotnet

> **Version:** 1.2
> **Date:** 2026-03-12
> **Target Framework:** .NET 8+ (LTS)
> **License:** MIT (matching open-source spirit of original crap4j)
> **Target Consumer:** AI coding agents via CLI (no IDE/CI integrations)

---

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        crap4dotnet CLI                          │
│                    (dotnet global tool)                         │
│                  dotnet crap analyze <path>                     │
└──────────────┬──────────────────────────────┬───────────────────┘
               │                              │
    ┌──────────▼──────────┐       ┌───────────▼───────────┐
    │  Complexity Engine   │       │   Coverage Reader     │
    │  (Roslyn-based)      │       │   (Cobertura XML)     │
    │                      │       │   (OpenCover XML)     │
    │  - Parses .cs files  │       │                       │
    │  - Walks syntax tree │       │  - Parses XML files   │
    │  - No MSBuild needed │       │  - Maps to methods    │
    └──────────┬───────────┘       └───────────┬───────────┘
               │                               │
    ┌──────────▼───────────────────────────────▼───────────┐
    │                  CRAP Calculator                      │
    │                                                       │
    │  - Joins complexity + coverage by method identity     │
    │  - Computes CRAP score per method                     │
    │  - Computes CRAP Load per method                      │
    │  - Aggregates to class/namespace/project              │
    └──────────────────────┬───────────────────────────────┘
                           │
    ┌──────────────────────▼───────────────────────────────┐
    │                  Report Generator                     │
    │                                                       │
    │  - JSON (primary — structured for AI agents)          │
    │  - XML (optional — crap4j-compatible legacy)          │
    │  - Console (minimal — exit codes + summary line)      │
    └──────────────────────────────────────────────────────┘
```

---

## 2. Project Structure

> **Design decision:** Collapsed from 5 projects to 3. The original Complexity, Coverage, and
> Reporting projects each had 1-2 public types — not enough to justify separate assemblies.
> Fewer projects = simpler build, faster tests, less ceremony.

```
crap4dotnet/
├── src/
│   ├── Crap4DotNet.Core/                  # All analysis logic (net8.0)
│   │   ├── Models/
│   │   │   ├── MethodIdentity.cs          # Fully-qualified method identification
│   │   │   ├── MethodCrapData.cs          # CRAP score, load, complexity, coverage per method
│   │   │   ├── TypeCrapData.cs            # Aggregated type-level stats
│   │   │   ├── NamespaceCrapData.cs       # Aggregated namespace-level stats
│   │   │   └── ProjectCrapData.cs         # Aggregated project-level stats
│   │   ├── Calculation/
│   │   │   ├── CrapCalculator.cs          # Core CRAP formula: comp^2 * (1-cov)^3 + comp
│   │   │   ├── CrapLoadCalculator.cs      # CRAP Load formula
│   │   │   └── CrapStatistics.cs          # Aggregation (mean, median, stddev, histogram)
│   │   ├── Configuration/
│   │   │   └── CrapOptions.cs             # Threshold, severity bands, configurable rules
│   │   ├── Matching/
│   │   │   ├── MethodCoverageMatcher.cs   # Join complexity + coverage by method identity
│   │   │   └── MethodIdentityNormalizer.cs # Normalize Cobertura/Roslyn names to canonical form
│   │   ├── Complexity/
│   │   │   ├── CyclomaticComplexityWalker.cs    # SyntaxWalker counting decision points
│   │   │   └── MethodDiscovery.cs               # Find all methods in a compilation
│   │   ├── Coverage/
│   │   │   └── CoberturaCoverageReader.cs       # Parse coverage.cobertura.xml
│   │   ├── Abstractions/
│   │   │   ├── IComplexityAnalyzer.cs           # Interface for complexity providers
│   │   │   ├── ICoverageReader.cs               # Interface for coverage data providers
│   │   │   └── IReportWriter.cs                 # Interface for report output
│   │   └── CrapAnalyzer.cs                      # Orchestrator: the analysis pipeline
│   │
│   └── Crap4DotNet.Cli/                  # CLI application (net8.0)
│       ├── Program.cs                     # Entry point + manual composition
│       ├── Commands/
│       │   ├── AnalyzeCommand.cs          # Main analysis command
│       │   └── DiffCommand.cs             # Compare two JSON reports
│       ├── Reporting/
│       │   └── JsonReportWriter.cs              # IReportWriter impl (delivery detail)
│       └── Crap4DotNet.Cli.csproj         # Packed as dotnet global tool
│
├── tests/
│   ├── Crap4DotNet.Core.Tests/
│   │   ├── CrapCalculatorTests.cs         # Formula validation with known values
│   │   ├── CrapLoadCalculatorTests.cs
│   │   ├── CrapStatisticsTests.cs
│   │   ├── CyclomaticComplexityWalkerTests.cs   # Test against known C# samples
│   │   ├── CoberturaCoverageReaderTests.cs
│   │   ├── JsonReportWriterTests.cs
│   │   ├── Samples/                              # C# files with known complexity
│   │   └── TestData/                              # Sample coverage XML files
│   └── Crap4DotNet.Cli.Tests/
│       └── EndToEndTests.cs                       # Full pipeline integration tests
│
├── samples/
│   ├── SampleProject/                     # A small C# project for demo/testing
│   └── SampleCoverageData/                # Pre-generated coverage XML
│
├── docs/
│   ├── 01-crap-metric-specification.md
│   ├── 02-gap-analysis.md
│   └── 03-implementation-plan.md          # This file
│
├── crap4dotnet.sln
├── Directory.Build.props                  # Shared MSBuild properties
└── Directory.Packages.props               # Central package management
```

---

## 3. Key Dependencies

| Package | Purpose | Version |
|---|---|---|
| `Microsoft.CodeAnalysis.CSharp` | Roslyn compiler APIs for syntax-tree analysis | 4.x (latest stable) |
| `System.CommandLine` | CLI argument parsing | 2.x (or `System.CommandLine.DragonFruit` if simpler) |
| `System.Text.Json` | JSON serialization | Built-in (.NET 8) |

> **Minimal dependency footprint.** No commercial dependencies. Statistical calculations
> (median, stddev) will be implemented directly — they are simple enough to avoid pulling
> in MathNet.Numerics for just two functions.

> **No DI container.** The CLI uses manual composition ("poor-man's DI") in `Program.cs`.
> All dependencies are constructed explicitly and wired together at the composition root.
> This means:
> - No `Microsoft.Extensions.DependencyInjection` dependency
> - Faster startup (no container reflection/scanning)
> - Easier to understand the object graph
> - Interfaces (`IComplexityAnalyzer`, `ICoverageReader`, `IReportWriter`) are still used
>   for testability and swappability — they're just constructed manually, not registered
>   in a container.
>
> This is the right choice for a CLI tool that constructs its object graph once per run.

---

## 4. Implementation Phases

### Phase 1: Core Engine (Weeks 1-2)

**Goal:** Calculate CRAP scores for a single C# project given pre-existing coverage data.

**Deliverables:**
1. `Crap4DotNet.Core` — Models, CRAP formula, CRAP Load, statistics, complexity walker, coverage reader, matching, JSON writer
2. Unit tests validating formula, complexity walker, and coverage reader against known values

**Tasks:**
- [ ] Set up solution structure with `Directory.Build.props`
- [ ] Implement `MethodIdentity` with full qualification and equality semantics
- [ ] Implement `CrapCalculator` with formula: `comp^2 * (1-cov)^3 + comp`
- [ ] Implement `CrapLoadCalculator` with formula: `comp * (1-cov) + comp/threshold`
- [ ] Implement `CrapStatistics` with all aggregation metrics
- [ ] Implement `CyclomaticComplexityWalker` as a Roslyn `CSharpSyntaxWalker`
- [ ] Implement `CoberturaCoverageReader` with branch-rate preferred / line-rate fallback (spec 6.2)
- [ ] Implement `MethodCoverageMatcher` in Core with left-outer-join semantics (spec 6.4)
- [ ] Implement `MethodIdentityNormalizer` in Core with `NormalizeFromCobertura()` and `NormalizeFromRoslyn()`
- [ ] Implement coverage clamping to [0.0, 1.0] and threshold validation >0 (spec 2.4)
- [ ] Implement empty-project handling: null for averages, exit 0 (spec 2.4.5)
- [ ] Implement severity band classification (spec 3.3)
- [ ] Implement histogram generation with half-open interval bins (spec 3.4)
- [ ] Handle local functions as separate methods (spec gap-analysis 5.3)
- [ ] Handle partial methods: exclude declaration-only (spec gap-analysis 5.3)
- [ ] Handle top-level statements: report as `Program.<Main>$` (spec gap-analysis 5.3)
- [ ] Write comprehensive unit tests for formula edge cases
- [ ] Write complexity walker tests against C# samples with known complexity
- [ ] Write join behavior tests: partial/no/orphaned coverage (spec 6.4.3)
- [ ] Write coverage field selection tests (spec 6.2 table)
- [ ] Validate Cobertura reader against real Coverlet output

**Key Design Decisions:**
- **Syntax-tree-only analysis (no MSBuild workspace):** The complexity walker uses
  `CSharpSyntaxTree.ParseText()` on individual `.cs` files — it does NOT load MSBuild
  workspaces or compile the project. This means:
  - No `Microsoft.CodeAnalysis.Workspaces.MSBuild` dependency (saves ~50MB of transitive deps)
  - Fast startup: parsing is ~10x faster than loading a workspace
  - Works without the .NET SDK's MSBuild targets being available
  - File discovery: when given a `.csproj`, read the `<Compile>` items or glob `**/*.cs`
    (excluding `obj/`, `bin/`). When given a `.sln`, parse it for project paths.
  - Trade-off: no semantic model (can't resolve types across files). This is acceptable
    because cyclomatic complexity is purely syntactic — it counts decision points in syntax
    trees without needing type resolution.
- Coverage reader produces `Dictionary<MethodIdentity, double>` for O(1) lookup
- All calculations use `double` precision (matching crap4j)

### Phase 2: CLI Tool (Weeks 3-4)

**Goal:** Working `dotnet crap` CLI tool that AI agents can invoke and parse.

> **Orchestration:** A `CrapAnalyzer` class in Core owns the analysis pipeline: parse source
> → compute complexity → read coverage → match methods → calculate CRAP → compute stats.
> It accepts `IComplexityAnalyzer` and `ICoverageReader` and returns `ProjectCrapData`.
> The CLI layer composes the object graph and calls `CrapAnalyzer.Analyze()`, then passes
> the result to `IReportWriter`. This keeps the highest-level policy in Core (Dependency Rule)
> and enables in-process integration tests without shelling out to the CLI.

**Deliverables:**
1. `Crap4DotNet.Cli` — Global tool with `analyze` and `diff` commands
2. `Crap4DotNet.Reporting` — JSON output for AI agent consumption
3. End-to-end tests running against sample projects

**Tasks:**
- [ ] Implement `AnalyzeCommand`: accept project/solution path + coverage file
- [ ] Implement JSON report writer with `schemaVersion: "1.0"` (spec 7.1)
- [ ] Implement hierarchical aggregation in JSON output (spec 5.2.1)
- [ ] Implement `warnings` array in JSON report (spec 8.3)
- [ ] Implement structured error JSON to stderr (spec 8.1, 8.2)
- [ ] Implement `DiffCommand` with full diff schema (spec 10.2.1) and classification rules (spec 10.2.2)
- [ ] Implement multi-project solution handling: single merged report (spec 6.5)
- [ ] Implement coverage auto-discovery for solutions (spec 6.5.2)
- [ ] Package as `dotnet tool` with NuGet packaging
- [ ] Write E2E tests: run `dotnet crap analyze` against sample project
- [ ] Write diff command tests against all 9 test scenarios (spec 10.2.3)
- [ ] Write error handling tests for all 10 error scenarios (spec 8.5)
- [ ] Add `--threshold`, `--format`, `--output`, `--min-crap` CLI options
- [ ] Add `--coverage` to specify coverage data path (multiple allowed for solutions)
- [ ] Implement exit codes: 0 = clean, 1 = CRAPpy methods found, 2 = error
- [ ] Support `--quiet` flag (suppress stderr warnings/progress)
- [ ] Support `--filter` for method name glob/regex matching

**CLI Output Mode Defaults:**
- **Default behavior (no flags):** JSON report to **stdout**. No human-readable text.
  All diagnostic messages and warnings go to **stderr**. This is the "agent-first" design —
  `dotnet crap analyze` is equivalent to `dotnet crap analyze --quiet --format json`.
- **`--quiet` flag:** Suppresses all stderr output (warnings, progress). Only JSON to stdout.
- **`--format json`:** Output format. Only JSON is supported (XML legacy format removed — no consumer).
- **`--output <path>`:** Write report to file instead of stdout. When specified, a brief
  human-readable summary is written to stdout (method count, CRAPpy count, exit status).
- **Errors always go to stderr** as structured JSON (see spec section 8).

**CLI Design:**
```bash
# Basic usage — JSON to stdout (default for AI agent consumption)
dotnet crap analyze ./src/MyApp.sln --coverage ./TestResults/coverage.cobertura.xml

# Save to file
dotnet crap analyze ./src/MyApp.csproj \
  --threshold 30 \
  --output ./reports/crap-report.json \
  --min-crap 15

# Filter to specific methods
dotnet crap analyze ./src/MyApp.sln \
  --coverage ./coverage.cobertura.xml \
  --filter "MyApp.Services.*"

# Compare two reports (e.g., before/after refactoring)
dotnet crap diff ./reports/before.json ./reports/after.json

# Quiet mode: only JSON, exit code indicates pass/fail
dotnet crap analyze ./src/MyApp.sln --quiet --threshold 30
echo $?  # 0 = no CRAPpy methods, 1 = CRAPpy methods found
```

**`--filter` flag semantics:**
- **Matches against `fullName`** — the fully-qualified method name (e.g., `MyApp.Services.UserService.GetById(int)`)
- **Glob syntax** — uses `*` (any characters) and `?` (single character). No regex.
- **Multiple filters** — `--filter` can be specified multiple times. Filters combine with OR (a method matches if it matches ANY filter).
- **Include semantics** — `--filter` is an include filter. Only methods matching at least one filter are analyzed. Without `--filter`, all methods are included.
- **Applies to analyze only** — the diff command compares full reports; filtering happens at analysis time, not diff time.
- Examples:
  - `--filter "MyApp.Services.*"` — all methods in the `MyApp.Services` namespace (and children)
  - `--filter "*.GetById*"` — any method named `GetById` in any class
  - `--filter "MyApp.Core.*" --filter "MyApp.Services.*"` — methods in either namespace

### Phase 3: Agent-Oriented Features (Weeks 5-6)

> **Phasing note:** The `diff` command ships in Phase 2 (it's a core CLI command, not an
> extension). Phase 3 covers convenience features that enhance agent ergonomics but aren't
> required for the core analyze+diff workflow.

**Goal:** Features that make the tool maximally useful for AI coding agents.

**Deliverables:**
1. Exit code semantics for programmatic pass/fail decisions
2. Single-file analysis mode
3. Stdin/stdout piping for coverage data
4. ~~Configuration file support~~ (deferred to v2)

**Tasks:**
- [ ] Implement single-file analysis: `dotnet crap analyze --file ./src/MyService.cs`
- [ ] Support reading coverage data from stdin: `--coverage -` (enables `dotnet test | dotnet crap analyze` piping)
- [ ] ~~Add configuration file (`.crap4dotnet.json`) for per-project defaults~~ (deferred to v2)
- [ ] Add `--top N` flag to return only the N worst methods (most useful for agents)
- [ ] Add `--sort-by crap|complexity|coverage|crapLoad` for result ordering
- [ ] Include `filePath` and `lineNumber` in all JSON method entries
- [ ] Add `--include-clean` flag (by default, only emit methods above `--min-crap`)
- [ ] Ensure all error messages go to stderr, all data goes to stdout
- [ ] Performance optimization: parallel file analysis using `Parallel.ForEach`

### Phase 4: Extended Analysis (Weeks 7-8)

**Goal:** Additional complexity metrics and coverage format support.

**Deliverables:**
1. Cognitive complexity support (optional alternative metric)
2. OpenCover format support
3. Configurable complexity rules for C#-specific constructs

**Tasks:**
- [ ] Implement `CognitiveComplexityWalker` (SonarSource algorithm)
- [ ] Implement OpenCover XML reader
- [ ] Add configurable complexity rules (count `?.`, `??`, LINQ, etc.)
- [ ] Add source generator / `[GeneratedCode]` attribute exclusion
- [ ] Add method-level `[SuppressCrap]` attribute support
- [ ] Pre-filter files by coverage data (skip files with no coverage entries)

---

## 5. Configuration File Format (Deferred to v2)

> **v1 scope:** CLI flags only. Configuration file support is deferred to v2. For AI agent
> usage, CLI flags are sufficient — agents construct the full command line programmatically
> and don't benefit from persisted config files.

`.crap4dotnet.json` (v2 — reference schema for future implementation):
```json
{
  "threshold": 30,
  "severityBands": {
    "low": [1, 5],
    "moderate": [6, 15],
    "elevated": [16, 29],
    "high": [30, 60],
    "critical": [61, null]
  },
  "complexity": {
    "mode": "cyclomatic",
    "countNullConditional": false,
    "countNullCoalesce": false,
    "countLinqExpressions": false,
    "countCatchBlocks": true,
    "countPatternMatchArms": true
  },
  "coverage": {
    "format": "auto",
    "paths": ["**/coverage.cobertura.xml"],
    "defaultForUncovered": 0.0
  },
  "output": {
    "format": "json",
    "directory": "./crap-reports"
  },
  "filters": {
    "excludeGenerated": true,
    "excludePatterns": ["*.Designer.cs", "*.g.cs"],
    "excludeAttributes": ["GeneratedCode", "CompilerGenerated"],
    "minComplexity": 1,
    "includeProperties": true,
    "includeConstructors": true
  },
  "exitCodes": {
    "failOnCrappy": true
  }
}
```

---

## 6. Key Implementation Details

### 6.1 Cyclomatic Complexity Walker

The heart of the system. A Roslyn `CSharpSyntaxWalker` that visits each method body:

```csharp
// Pseudo-code for the walker
public class CyclomaticComplexityWalker : CSharpSyntaxWalker
{
    public int Complexity { get; private set; } = 1; // Start at 1

    // NOTE: IfStatementSyntax is visited for EVERY "if" keyword, including
    // those inside "else if" chains. In Roslyn, "else if (x)" is an ElseClause
    // containing a child IfStatementSyntax. The walker naturally visits it.
    // Do NOT add a separate VisitElseClause — that would double-count.
    public override void VisitIfStatement(IfStatementSyntax node)
    {
        Complexity++;
        base.VisitIfStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        Complexity++;
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        Complexity++;
        base.VisitForEachStatement(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        Complexity++;
        base.VisitWhileStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        Complexity++;
        base.VisitDoStatement(node);
    }

    public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
    {
        Complexity++;
        base.VisitCaseSwitchLabel(node);
    }

    public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
    {
        Complexity++;
        base.VisitCasePatternSwitchLabel(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        Complexity++;
        base.VisitCatchClause(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Complexity++;
        base.VisitConditionalExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
            node.IsKind(SyntaxKind.LogicalOrExpression))
        {
            Complexity++;
        }
        // Configurable: CoalesceExpression for ??
        base.VisitBinaryExpression(node);
    }

    public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
    {
        // Count each arm EXCEPT the discard pattern (default).
        // Do NOT use "total arms - 1" — that breaks when there is no default.
        if (node.Pattern is not DiscardPatternSyntax)
        {
            Complexity++;
        }
        base.VisitSwitchExpressionArm(node);
    }
}
```

### 6.2 Method Identity Matching

The trickiest part: matching Roslyn method symbols to Coverlet coverage entries.

**Coverlet (Cobertura XML)** identifies methods as:
```xml
<class name="MyApp.Services.UserService" filename="Services/UserService.cs">
  <methods>
    <method name="ValidateUser" signature="(System.String, System.String)" line-rate="0.75" branch-rate="0.50">
```

**Roslyn** identifies methods as:
```csharp
IMethodSymbol.ToDisplayString() → "MyApp.Services.UserService.ValidateUser(string, string)"
```

**Strategy:** Normalize both sides to a canonical form for matching:
- Normalize primitive type names (`string` ↔ `System.String`, `int` ↔ `System.Int32`, etc.)
- Strip generic arity markers (`List`1` → `List`)
- Handle property accessors (`get_Name` / `set_Name` ↔ `Name.get` / `Name.set`)
- Handle operator overloads (`op_Addition` ↔ `operator +`)
- Handle explicit interface implementations (prefixed with interface name)
- Handle nested types (`Outer+Inner` ↔ `Outer.Inner`)

#### 6.2.1 Method Identity Matching Test Cases

The following concrete pairs MUST match. Each row shows the Cobertura XML representation
and the Roslyn representation that should resolve to the same method:

| # | Scenario | Cobertura class | Cobertura method | Cobertura signature | Roslyn display string |
|---|---|---|---|---|---|
| 1 | Simple method | `MyApp.UserService` | `Validate` | `(System.String)` | `MyApp.UserService.Validate(string)` |
| 2 | Primitive types | `MyApp.MathHelper` | `Add` | `(System.Int32, System.Int32)` | `MyApp.MathHelper.Add(int, int)` |
| 3 | Nullable value type | `MyApp.Parser` | `TryParse` | `(System.String, System.Nullable{System.Int32})` | `MyApp.Parser.TryParse(string, int?)` |
| 4 | Generic method | `MyApp.Repo` | `Find` | `(System.Func{T,System.Boolean})` | `MyApp.Repo.Find<T>(Func<T, bool>)` |
| 5 | Generic class | `MyApp.Cache`1` | `Get` | `(System.String)` | `MyApp.Cache<T>.Get(string)` |
| 6 | Nested type | `MyApp.Outer+Inner` | `DoWork` | `()` | `MyApp.Outer.Inner.DoWork()` |
| 7 | Property getter | `MyApp.Config` | `get_Timeout` | `()` | `MyApp.Config.Timeout.get` |
| 8 | Property setter | `MyApp.Config` | `set_Timeout` | `(System.Int32)` | `MyApp.Config.Timeout.set` |
| 9 | Indexer getter | `MyApp.Collection` | `get_Item` | `(System.Int32)` | `MyApp.Collection.this[int].get` |
| 10 | Operator overload | `MyApp.Money` | `op_Addition` | `(MyApp.Money, MyApp.Money)` | `MyApp.Money.operator +(Money, Money)` |
| 11 | Explicit interface | `MyApp.MyList` | `System.IDisposable.Dispose` | `()` | `MyApp.MyList.System.IDisposable.Dispose()` |
| 12 | Overloaded method (A) | `MyApp.Logger` | `Log` | `(System.String)` | `MyApp.Logger.Log(string)` |
| 13 | Overloaded method (B) | `MyApp.Logger` | `Log` | `(System.String, System.Exception)` | `MyApp.Logger.Log(string, Exception)` |
| 14 | Constructor | `MyApp.Service` | `.ctor` | `(System.String)` | `MyApp.Service.Service(string)` |
| 15 | Static constructor | `MyApp.Service` | `.cctor` | `()` | `MyApp.Service.Service()` (static) |
| 16 | Async method | `MyApp.ApiClient` | `FetchAsync` | `(System.String)` | `MyApp.ApiClient.FetchAsync(string)` |
| 17 | Array parameter | `MyApp.Processor` | `Process` | `(System.Byte[])` | `MyApp.Processor.Process(byte[])` |
| 18 | ref/out parameters | `MyApp.Parser` | `TryGet` | `(System.String, System.Int32&)` | `MyApp.Parser.TryGet(string, out int)` |

> **Implementation note:** The matching algorithm should normalize BOTH sides to a common
> canonical form (e.g., always use CLR type names like `System.String`, use `.` for nested
> types, strip generic arity markers) rather than trying to parse one format into the other.
> A `MethodIdentityNormalizer` class should encapsulate this logic with separate
> `NormalizeFromCobertura()` and `NormalizeFromRoslyn()` methods that produce the same
> canonical string for equivalent methods.

### 6.3 Coverage Data Pipeline (Agent Workflow)

A typical AI agent workflow:

```bash
# Step 1: Agent runs tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Step 2: Agent runs CRAP analysis, captures JSON output
CRAP_REPORT=$(dotnet crap analyze ./src/MyApp.sln --quiet)

# Step 3: Agent parses JSON to decide what to refactor
# The JSON contains filePath + lineNumber for each method,
# so the agent can navigate directly to problematic code.

# Step 4: After refactoring, agent re-runs to verify improvement
dotnet crap diff ./reports/before.json ./reports/after.json
```

---

## 7. Testing Strategy

### 7.1 Formula Validation

Test the CRAP formula against known values from the original crap4j:

| Complexity | Coverage | Expected CRAP | CRAPpy? | Expected Load (threshold=30) |
|---|---|---|---|---|
| 1 | 1.0 | 1.0 | No | 0 |
| 1 | 0.0 | 2.0 | No | 0 |
| 5 | 0.0 | 30.0 | No (exactly at threshold, not over) | 0 |
| 5 | 0.0 | 30.0 | No | 0 |
| 6 | 0.0 | 42.0 | Yes | 6.2 |
| 10 | 0.0 | 110.0 | Yes | 10.33 |
| 10 | 0.42 | ~29.5 | No | 0 |
| 30 | 1.0 | 30.0 | No (exactly at threshold, not over) | 0 |
| 30 | 0.0 | 930.0 | Yes | 31.0 |
| 31 | 1.0 | 31.0 | Yes (cannot be saved by coverage) | 1.03 |

> **Threshold semantics:** CRAPpy is defined as `CRAP > threshold` (strictly greater than).
> CRAP Load formula: `comp * (1 - cov) + comp / threshold`. The result is a `double`,
> not truncated to `int` (diverges from original crap4j which used `(int)` cast).

### 7.2 Complexity Validation

Test complexity walker against C# samples with known values:

```csharp
// Expected complexity: 1 (no decisions)
void SimpleMethod() { Console.WriteLine("hello"); }

// Expected complexity: 2 (1 base + 1 if)
void OneIf(bool x) { if (x) Console.WriteLine("yes"); }

// Expected complexity: 3 (1 base + 1 if + 1 else-if)
// NOTE: "else if" counts as one IfStatementSyntax — no double-counting
void IfElseIf(int x) {
    if (x > 0) Console.WriteLine("positive");
    else if (x < 0) Console.WriteLine("negative");
}

// Expected complexity: 4 (1 base + 1 if + 2 &&)
// NOTE: each && and || is a separate decision point
void MultipleConditions(int x) {
    if (x > 0 && x < 100 && x != 42) Console.WriteLine("in range");
}

// Expected complexity: 4 (1 base + 1 if + 1 && + 1 else-if)
void ElseIfWithLogical(int x) {
    if (x > 0 && x < 100) Console.WriteLine("in range");
    else if (x < 0) Console.WriteLine("negative");
}

// Expected complexity: 5 (1 base + 4 non-discard arms)
// NOTE: only arms whose pattern is NOT DiscardPatternSyntax are counted
int SwitchExpression(int x) => x switch {
    1 => 10,
    2 => 20,
    3 => 30,
    4 => 40,
    _ => 0      // discard pattern — NOT counted
};

// Expected complexity: 4 (1 base + 3 non-discard arms, NO discard arm present)
int SwitchNoDefault(int x) => x switch {
    1 => 10,
    2 => 20,
    int n when n > 2 => 30,
};
```

### 7.3 End-to-End Validation

Create a sample project with:
- Methods of varying complexity (1 to 50+)
- Varying coverage levels (0%, 50%, 100%)
- All C# constructs (async, LINQ, patterns, properties, etc.)
- Verify complete pipeline: `dotnet test` → coverage XML → `dotnet crap` → report

---

## 8. Performance Targets

| Metric | Target |
|---|---|
| Small project (< 100 files) | < 5 seconds |
| Medium project (100-500 files) | < 15 seconds |
| Large project (500-2000 files) | < 60 seconds |
| Very large solution (2000+ files) | < 3 minutes |

**Optimization strategies:**
- Parallel file analysis using `Parallel.ForEach`
- Lazy Roslyn compilation (only parse syntax trees, don't compile)
- Pre-filter files by coverage data (skip files with no coverage entries)
- Incremental analysis (cache complexity scores, only recompute changed files)

---

## 9. Release Plan

| Version | Scope | Target |
|---|---|---|
| **0.1.0-alpha** | Core formula + Roslyn complexity + Cobertura reader + JSON output | Phase 1 complete |
| **0.5.0-beta** | Full CLI tool + diff command + exit codes + single-file mode | Phase 2-3 complete |
| **1.0.0** | Config file + cognitive complexity + OpenCover + performance tuning | Phase 4 complete |

---

## 10. Success Criteria

1. **Correctness:** CRAP scores match crap4j for equivalent Java/C# code
2. **Agent-friendly:** JSON output parseable by any AI agent; exit codes for pass/fail
3. **Ecosystem fit:** Installable as `dotnet tool`, works with `dotnet test` + Coverlet
4. **Performance:** Analyzes a 1000-file solution in under 60 seconds
5. **Minimal footprint:** Few dependencies, fast startup, small binary
6. **Navigability:** Every method in output includes `filePath` and `lineNumber`
