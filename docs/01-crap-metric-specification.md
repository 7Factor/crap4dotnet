# CRAP Metric Specification (Language-Agnostic)

> **Version:** 1.1
> **Date:** 2026-03-11
> **Based on:** crap4j by Alberto Savoia & Bob Evans (2007)
> **Sources:** crap4j.org, Google Testing Blog, Artima Weblogs, GMetrics, NDepend

---

## 1. Overview

### 1.1 Purpose

The **C.R.A.P.** (Change Risk Anti-Patterns) metric identifies code that is risky to modify due to a
hazardous combination of high complexity and insufficient automated test coverage. It answers:
*"How likely am I to introduce a defect if I change this method?"*

### 1.2 Scope

CRAP operates at the **method level** (functions, procedures, property accessors). It is computed
per-method and then aggregated upward to class, namespace/package, and project levels.

### 1.3 Target Consumer

The primary consumer of crap4dotnet is **AI coding agents** operating via CLI. The tool is designed
to be invoked programmatically, producing structured machine-readable output (JSON) that agents can
parse to make decisions about which code to refactor, where to add tests, and how to prioritize
maintenance work. Human-oriented UIs (IDE plugins, HTML dashboards, CI badge integrations) are
explicitly out of scope.

### 1.4 Academic Foundation

- **Cyclomatic Complexity** (Thomas McCabe, 1976): Measures the number of linearly independent
  paths through a method's control flow graph. Defined as `E - N + 2` where E = edges, N = nodes
  in the CFG. Equivalently: `1 + number of decision points`.
- **Code Coverage by Automated Tests**: The percentage of a method's code paths exercised by
  automated (not manual) test suites.
- **Correlation Studies**: Multiple studies demonstrate a definite correlation between excessive code
  complexity and increased probability of introducing defects during maintenance.

---

## 2. Core Formula

### 2.1 The CRAP Formula (v0.1)

Given a method `m`:

```
CRAP(m) = comp(m)^2 * (1 - cov(m))^3 + comp(m)
```

Where:
- **`comp(m)`** = cyclomatic complexity of method `m` (integer >= 1)
- **`cov(m)`** = test code coverage of method `m` as a decimal (0.0 to 1.0)

> **Note:** In the original crap4j documentation, coverage is expressed as a percentage (0-100),
> making the formula: `comp(m)^2 * (1 - cov(m)/100)^3 + comp(m)`. The formulas are equivalent;
> this spec normalizes coverage to 0.0-1.0.

### 2.2 Formula Properties

| Scenario | Complexity | Coverage | CRAP Score |
|---|---|---|---|
| Simplest possible method, fully tested | 1 | 1.0 | 1 |
| Simple method, no tests | 1 | 0.0 | 2 |
| Moderate method, fully tested | 10 | 1.0 | 10 |
| Moderate method, no tests | 10 | 0.0 | 110 |
| Complex method, fully tested | 30 | 1.0 | 30 |
| Complex method, half tested | 30 | 0.5 | 142.5 |
| Complex method, no tests | 30 | 0.0 | 930 |

### 2.3 Key Mathematical Observations

1. **Minimum CRAP score is 1** (complexity=1, coverage=100%).
2. **No maximum** — grows with the square of complexity for uncovered code.
3. **Coverage has diminishing returns**: Cubed term means each additional percentage of coverage
   matters more for high-complexity methods.
4. **Complexity of 31+ cannot be "saved" by coverage alone** — even at 100% coverage,
   CRAP(31, 1.0) = 31 which exceeds the threshold of 30.
5. **CRAP(30, 1.0) = 30 exactly equals the threshold** and is NOT CRAPpy (see Section 3.1).

---

## 3. Threshold and Classification

### 3.1 CRAP Threshold

The standard threshold for "crappy" code is **CRAP score > 30** (strictly greater than).

A method is CRAPpy when `CRAP(m) > threshold`. A method exactly at the threshold is NOT CRAPpy.

> **Design note:** The original crap4j source code uses `>=` in the `calculateCrapLoad` method,
> but the original documentation and FAQ tables consistently describe CRAP=30 as "below CRAPpy
> threshold" (e.g., complexity 0-5 with 0% coverage produces CRAP=30 and is listed as requiring
> 0% coverage to stay below the threshold). We follow the documentation semantics, not the
> likely-buggy `>=` in the source. This also means `CRAP(30, 1.0) = 30` is NOT CRAPpy,
> consistent with the coverage table showing complexity 26-30 as achievable with 100% coverage.

### 3.2 Required Coverage by Complexity

| Cyclomatic Complexity | Min Coverage to Stay Below 30 |
|---|---|
| 0 – 5 | 0% |
| 6 – 10 | 42% |
| 11 – 15 | 57% |
| 16 – 20 | 71% |
| 21 – 25 | 80% |
| 26 – 30 | 100% |
| 31+ | **Impossible** — must refactor to reduce complexity |

### 3.3 Recommended Severity Bands (Extension)

| CRAP Score | Risk Level | Recommended Action |
|---|---|---|
| 1 – 5 | Low | No action needed |
| 6 – 15 | Moderate | Consider adding tests |
| 16 – 30 | Elevated | Prioritize test coverage |
| 31 – 60 | High (CRAPpy) | Refactor and/or add tests |
| 60+ | Critical | Urgent refactoring required |

---

## 4. CRAP Load

### 4.1 Definition

CRAP Load quantifies the **effort required** to bring a CRAPpy method back under the threshold.
It is only computed for methods exceeding the CRAP threshold.

### 4.2 Formula

```
if CRAP(m) > threshold:
    crapLoad(m) = comp(m) * (1 - cov(m)) + comp(m) / threshold
else:
    crapLoad(m) = 0
```

> **Note:** The comparison is strictly greater than (`>`). A method with CRAP exactly equal
> to the threshold has a CRAP Load of 0 and is not considered CRAPpy.

### 4.3 Interpretation

Higher CRAP Load = more work needed (combination of uncovered complexity and absolute complexity).
The total CRAP Load of a project is the sum of all method CRAP Loads.

---

## 5. Aggregation Metrics

### 5.1 Project-Level Statistics

The following statistics MUST be computed across all methods in the analyzed codebase:

| Metric | Definition |
|---|---|
| **Method Count** | Total number of analyzed methods |
| **Total CRAP** | Sum of all method CRAP scores |
| **Average CRAP** | Total CRAP / Method Count |
| **Median CRAP** | Median of all method CRAP scores |
| **Standard Deviation** | Std dev of all method CRAP scores |
| **CRAPpy Method Count** | Number of methods with CRAP > threshold |
| **CRAPpy Method Percent** | CRAPpy Method Count / Method Count * 100 |
| **Total CRAP Load** | Sum of all method CRAP Loads |
| **CRAP Threshold** | The configured threshold (default: 30) |

### 5.2 Hierarchical Aggregation

Statistics SHOULD be computable at multiple levels:
- **Method** → individual CRAP score
- **Class/Type** → aggregate of all methods in the type
- **Namespace/Package** → aggregate of all types in the namespace
- **Assembly/Project** → aggregate of all namespaces
- **Solution** → aggregate of all assemblies

#### 5.2.1 Hierarchical Aggregation in JSON Output

The JSON report includes an optional `hierarchy` object that groups methods by
namespace and class. Each group carries the same stats schema as the top-level `stats`.
This structure allows AI agents to identify the worst namespace or class without
post-processing the flat `methods` array.

```json
{
  "hierarchy": {
    "namespaces": [
      {
        "name": "MyApp.Services",
        "stats": {
          "methodCount": 25,
          "totalCrap": 450.0,
          "averageCrap": 18.0,
          "medianCrap": 12.0,
          "standardDeviation": 14.2,
          "crappyMethodCount": 5,
          "crappyMethodPercent": 20.0,
          "totalCrapLoad": 120
        },
        "classes": [
          {
            "name": "UserService",
            "stats": {
              "methodCount": 8,
              "totalCrap": 180.0,
              "averageCrap": 22.5,
              "medianCrap": 15.0,
              "standardDeviation": 18.3,
              "crappyMethodCount": 3,
              "crappyMethodPercent": 37.5,
              "totalCrapLoad": 85
            }
          }
        ]
      }
    ]
  }
}
```

> **Design note:** The `hierarchy` object is included by default. Agents that only need
> the flat `methods` array can ignore it. The `methods` array remains the canonical
> source of per-method data; `hierarchy` provides pre-computed rollups for convenience.

### 5.3 CRAP Histogram

A histogram SHOULD be generated showing the distribution of CRAP scores across configurable bins
(e.g., 0-5, 5-10, 10-15, 15-20, 20-25, 25-30, 30-40, 40-50, 50-75, 75-100, 100+).

---

## 6. Input Data Requirements

### 6.1 Cyclomatic Complexity

**Required Input:** For each method, an integer cyclomatic complexity score.

**Calculation Method (Language-Agnostic):**
Cyclomatic complexity = 1 + the count of the following control flow constructs:
- `if` statements (each `if` keyword counts once, including those in `else if` chains)
- Conditional/ternary expressions (`?:`)
- `for` / `foreach` / `while` / `do-while` loops
- `case` labels in switch/match statements (not the switch itself, not the default)
- Switch expression arms (excluding the discard/default arm)
- `catch` blocks
- Logical AND (`&&`) and OR (`||`) operators (short-circuit evaluation creates branch points)
- Null-coalescing operators — configurable (language-specific)

**Excluded from complexity count:**
- `else` (the `if` keyword already counts the branch; `else if` is counted by its `if`)
- `finally` blocks
- `default` / discard arms in switch (these are the fallthrough, not a decision)
- Abstract/interface methods (complexity = 0, should be excluded from CRAP)
- Empty methods (complexity = 0, should be excluded)

### 6.2 Code Coverage

**Required Input:** For each method, a coverage percentage (0.0 to 1.0).

**Coverage Types (in order of preference):**
1. **Branch coverage** — most closely aligned with cyclomatic complexity
2. **Line coverage** — most commonly available
3. **Basis path coverage** — original crap4j used this, but rarely available in practice

**Cobertura XML Field Selection:**
Cobertura XML provides both `branch-rate` and `line-rate` per method. The tool MUST use
this selection logic:

1. **Prefer `branch-rate`** — branch coverage is most closely aligned with cyclomatic
   complexity (both measure decision paths).
2. **Fall back to `line-rate`** — when `branch-rate` is absent, zero, or the method has
   no branches (e.g., a linear method where `branch-rate="0"` but `line-rate="1.0"`
   because all lines are executed). Specifically: if `branch-rate` is `0` AND
   `line-rate` is `> 0` AND the method has `0` branch conditions (no `<condition>` elements),
   use `line-rate` because the method is branchless and fully executed.
3. **Use 0.0** if neither field is present.

| `branch-rate` | `line-rate` | Has `<condition>` elements? | Selected coverage |
|---|---|---|---|
| 0.75 | 0.90 | Yes | 0.75 (branch-rate preferred) |
| 0.0 | 1.0 | No | 1.0 (branchless method, use line-rate) |
| 0.0 | 0.5 | Yes | 0.0 (has branches, none covered) |
| absent | 0.80 | N/A | 0.80 (fallback to line-rate) |
| 0.50 | 0.0 | Yes | 0.50 (branch-rate preferred) |
| absent | absent | N/A | 0.0 (no data) |

**Important:** Coverage MUST come from automated test execution. Manual testing
coverage is explicitly excluded from CRAP analysis.

### 6.3 Method Identification

Each method MUST be uniquely identified by:
- **Namespace/Package** (fully qualified)
- **Type/Class name**
- **Method name**
- **Method signature** (parameter types for overload disambiguation)
- **Full qualified method** (combined display string)

### 6.4 Coverage-Complexity Data Join Behavior

The CRAP calculator joins two data sets: complexity data (from source analysis) and coverage
data (from test execution). These data sets may not align perfectly. The join rules are:

#### 6.4.1 Join Semantics

The join is a **left outer join from complexity to coverage**: every method found in source
analysis produces a CRAP result, whether or not it has a matching coverage entry.

| Source method has complexity? | Coverage entry exists? | Behavior |
|---|---|---|
| Yes | Yes | Normal: use both values to compute CRAP |
| Yes | No | Use `coverage = 0.0` (uncovered). Emit `UNMATCHED_METHODS` warning |
| No | Yes | **Ignore** the orphaned coverage entry. Emit `ORPHANED_COVERAGE` warning |
| No | No | N/A — method does not exist |

#### 6.4.2 Rationale

- **Complexity without coverage** (most common mismatch): The method exists in source but has
  no test coverage data. This typically means the method is untested. Treating it as 0.0
  coverage is the safe, conservative default. The `coverage.defaultForUncovered` config option
  allows overriding this (e.g., `null` to exclude from analysis), but the default MUST be `0.0`.

- **Coverage without complexity** (orphaned coverage): The coverage XML references a method not
  found in source analysis. This happens when:
  - Coverage data is from a different version of the code than the source being analyzed
  - The method was generated (source generators, compiler-generated state machines)
  - The method was filtered out by exclude patterns or attribute filters

  These entries are silently ignored (not included in results) with a diagnostic warning.

- **Version mismatch detection**: If more than 20% of coverage entries are orphaned, the tool
  SHOULD emit a `COVERAGE_STALE` warning suggesting the coverage data may be from a different
  build than the source.

#### 6.4.3 Join Test Scenarios

| Scenario | Complexity methods | Coverage entries | Expected |
|---|---|---|---|
| Perfect match | A, B, C | A, B, C | All methods have real coverage |
| Partial coverage | A, B, C | A, B | C gets coverage=0.0, warning |
| No coverage file methods | A, B, C | (empty) | All get coverage=0.0, warning |
| Extra coverage entries | A, B | A, B, C, D | C, D ignored, warning |
| Complete mismatch | A, B | X, Y | A, B get 0.0; X, Y ignored; stale warning |
| Empty source | (empty) | A, B | No results, `NO_METHODS_FOUND` error |

### 6.5 Multi-Project Solution Handling

When the tool is given a `.sln` (solution) file, it analyzes all non-test projects in the
solution and produces a **single merged report**.

#### 6.5.1 Behavior

1. **Source analysis**: All `.cs` files from all non-test projects in the solution are analyzed.
   Test projects (those referencing `Microsoft.NET.Test.Sdk` or containing `xunit`/`nunit`/`mstest`
   package references) are excluded from analysis by default.
2. **Coverage merging**: If multiple `--coverage` files are specified, or if auto-discovery finds
   multiple coverage files, all coverage data is merged into a single lookup. If the same method
   appears in multiple coverage files, the **highest coverage value** is used (best-case from
   any test project).
3. **Single report**: The output is one JSON report with all methods from all projects. The
   `hierarchy` section groups by namespace (which naturally separates projects since .NET projects
   typically use distinct root namespaces).
4. **Project field**: The top-level `project` field uses the solution name (without `.sln`).

#### 6.5.2 Coverage Auto-Discovery for Solutions

When `--coverage` is not specified, the tool searches for coverage files:
```
<solution-dir>/**/TestResults/**/coverage.cobertura.xml
```

All matching files are merged. If no files are found, exit with `COVERAGE_FILE_NOT_FOUND`.

#### 6.5.3 Design Rationale

A single merged report (rather than per-project reports) is simpler for AI agents to consume —
one JSON document, one exit code, one set of stats. Agents that need per-project breakdown can
use the `hierarchy.namespaces` grouping or filter the `methods` array by namespace prefix.

---

## 7. Report Format

### 7.1 JSON Report Structure (Primary)

JSON is the primary output format, designed for machine consumption by AI coding agents.
The structure is optimized for programmatic parsing:

```json
{
  "project": "ProjectName",
  "timestamp": "2026-03-11T12:00:00Z",
  "threshold": 30,
  "stats": {
    "methodCount": 100,
    "totalCrap": 1234.56,
    "averageCrap": 12.34,
    "medianCrap": 8.5,
    "standardDeviation": 15.67,
    "crappyMethodCount": 12,
    "crappyMethodPercent": 12.0,
    "totalCrapLoad": 456
  },
  "methods": [
    {
      "namespace": "MyApp.Services",
      "className": "UserService",
      "methodName": "ValidateUser",
      "signature": "(string, string) : bool",
      "fullName": "MyApp.Services.UserService.ValidateUser(string, string) : bool",
      "filePath": "src/Services/UserService.cs",
      "lineNumber": 42,
      "crap": 45.3,
      "complexity": 12,
      "coverage": 0.35,
      "crapLoad": 9,
      "isCrappy": true
    }
  ]
}
```

> **Note:** `filePath` and `lineNumber` are included to allow AI agents to navigate directly
> to problematic methods. Coverage is normalized to 0.0-1.0 (not percentage).

### 7.2 XML Report Structure (Legacy Compatibility)

An XML format following the original crap4j `<crap_result>` schema MAY be supported for
interoperability with existing tooling that consumes crap4j reports.

---

## 8. Error Output Specification

### 8.1 Error Output Format

When the tool encounters an error (exit code 2), it MUST write structured JSON to **stderr**.
Stdout MUST remain empty or contain only a partial valid JSON report (for streaming errors).
This ensures AI agents can always parse error information programmatically.

```json
{
  "error": {
    "code": "COVERAGE_FILE_NOT_FOUND",
    "message": "Coverage file not found: ./TestResults/coverage.cobertura.xml",
    "details": "Run 'dotnet test --collect:\"XPlat Code Coverage\"' to generate coverage data.",
    "file": "./TestResults/coverage.cobertura.xml"
  }
}
```

### 8.2 Error Codes

| Code | Exit Code | Description |
|---|---|---|
| `SOURCE_NOT_FOUND` | 2 | The specified .cs/.csproj/.sln file does not exist |
| `COVERAGE_FILE_NOT_FOUND` | 2 | The specified or auto-discovered coverage file does not exist |
| `COVERAGE_PARSE_ERROR` | 2 | Coverage XML is malformed or uses an unsupported format |
| `SOURCE_PARSE_ERROR` | 2 | C# source file has syntax errors preventing analysis |
| `NO_METHODS_FOUND` | 2 | No analyzable methods found in the specified source |
| `INVALID_CONFIGURATION` | 2 | Configuration file or CLI flags contain invalid values |
| `INVALID_THRESHOLD` | 2 | Threshold is <= 0 or not a valid number |
| `DIFF_SCHEMA_MISMATCH` | 2 | Diff input files have incompatible schema versions |
| `DIFF_FILE_NOT_FOUND` | 2 | One or both diff input files do not exist |
| `IO_ERROR` | 2 | File system error (permissions, disk full, etc.) |

### 8.3 Warning Output

Non-fatal issues are reported as warnings in the JSON report's top-level `warnings` array.
Warnings do NOT change the exit code.

```json
{
  "warnings": [
    {
      "code": "COVERAGE_STALE",
      "message": "Coverage data is older than source files",
      "details": "coverage.cobertura.xml was modified 2026-03-01, source was modified 2026-03-11"
    },
    {
      "code": "UNMATCHED_METHODS",
      "message": "3 methods in source had no coverage data (assumed 0.0 coverage)",
      "details": "MyApp.NewService.DoWork(), MyApp.NewService.Init(), MyApp.Utils.Format()"
    }
  ]
}
```

### 8.4 Warning Codes

| Code | Description |
|---|---|
| `COVERAGE_STALE` | Coverage file is older than source files |
| `UNMATCHED_METHODS` | Methods in source had no matching coverage entry |
| `ORPHANED_COVERAGE` | Coverage entries had no matching source method |
| `COVERAGE_CLAMPED` | Coverage values outside 0.0-1.0 were clamped |
| `EMPTY_NAMESPACE` | A namespace contained no analyzable methods after filtering |

### 8.5 Error Handling Test Scenarios

| Scenario | Input | Expected |
|---|---|---|
| Missing source file | `dotnet crap analyze /nonexistent.sln` | Exit 2, `SOURCE_NOT_FOUND` |
| Missing coverage file | `--coverage /nonexistent.xml` | Exit 2, `COVERAGE_FILE_NOT_FOUND` |
| Corrupt coverage XML | Truncated XML file | Exit 2, `COVERAGE_PARSE_ERROR` |
| C# syntax errors | File with `class {{{` | Exit 2, `SOURCE_PARSE_ERROR` |
| No methods after filtering | Project with only interfaces | Exit 2, `NO_METHODS_FOUND` |
| Threshold = 0 | `--threshold 0` | Exit 2, `INVALID_THRESHOLD` |
| Threshold = -5 | `--threshold -5` | Exit 2, `INVALID_THRESHOLD` |
| Diff with missing file | `dotnet crap diff a.json /missing.json` | Exit 2, `DIFF_FILE_NOT_FOUND` |
| Valid but stale coverage | Coverage 10 days older than source | Exit 0, warning `COVERAGE_STALE` |
| Methods without coverage | 3 source methods not in coverage XML | Exit 0/1, warning `UNMATCHED_METHODS` |

---

## 9. Language-Specific Considerations

### 9.1 Java-Specific Features (from original crap4j)

These are features specific to the Java implementation that may not directly translate:

| Feature | Java Implementation | Notes |
|---|---|---|
| **Bytecode analysis** | Uses ASM library to analyze `.class` files | Java-specific; other languages analyze source or IL |
| **EMMA coverage** | Reads EMMA coverage data format | Java-specific coverage tool |
| **Cobertura coverage** | Reads Cobertura XML format | Cross-language (also used in .NET via Coverlet) |
| **JUnit integration** | Automatically discovers/runs JUnit tests | Java-specific test framework |
| **Eclipse plugin** | IDE integration via Eclipse plugin API | Java IDE-specific |
| **Package structure** | Uses Java package naming conventions | Language has equivalent (namespace, module) |
| **Inner classes** | Handles `$` separator in class names | Java-specific |
| **Synthetic methods** | Filters bridge/synthetic methods | JVM-specific bytecode artifacts |

### 9.2 Universally Applicable Features

These features should be implemented in ANY language port:
- Core CRAP formula calculation
- CRAP Load calculation
- Configurable threshold
- All aggregation statistics (mean, median, stddev, percentages)
- Structured report output (JSON preferred for machine consumption)
- Method-level granularity with hierarchical aggregation
- Coverage data ingestion from standard formats
- Filtering of abstract/empty/generated methods

---

## 10. Extended Metrics (Optional Enhancements)

### 10.1 CRAP Trend Analysis

Track CRAP metrics over time (per build/commit) to identify:
- **Improving trend**: Total CRAP Load decreasing
- **Degrading trend**: New CRAPpy methods being introduced
- **Fixed methods**: Previously CRAPpy methods now below threshold
- **New CRAPpy methods**: Methods that newly exceed the threshold

### 10.2 CRAP Diff (Build Comparison)

Compare two analysis runs to identify:
- New CRAPpy methods (methods that became CRAPpy since last run)
- Fixed CRAPpy methods (methods that were CRAPpy but are now below threshold)
- Changed CRAP scores (methods whose score increased or decreased)
- Added methods (present in "after" but not "before")
- Removed methods (present in "before" but not "after")

#### 10.2.1 Diff JSON Output Schema

```json
{
  "schemaVersion": "1.0",
  "type": "diff",
  "before": {
    "project": "MyApp",
    "timestamp": "2026-03-10T12:00:00Z"
  },
  "after": {
    "project": "MyApp",
    "timestamp": "2026-03-11T12:00:00Z"
  },
  "summary": {
    "totalMethodsBefore": 100,
    "totalMethodsAfter": 102,
    "crappyMethodsBefore": 12,
    "crappyMethodsAfter": 10,
    "totalCrapLoadBefore": 456,
    "totalCrapLoadAfter": 320,
    "newCrappy": 1,
    "fixedCrappy": 3,
    "added": 5,
    "removed": 3,
    "improved": 8,
    "regressed": 2,
    "unchanged": 87
  },
  "methods": {
    "newCrappy": [
      {
        "fullName": "MyApp.Services.OrderService.ProcessOrder(Order) : bool",
        "crap": 45.3,
        "complexity": 15,
        "coverage": 0.20,
        "status": "new_crappy"
      }
    ],
    "fixedCrappy": [
      {
        "fullName": "MyApp.Services.UserService.ValidateUser(string, string) : bool",
        "crapBefore": 45.3,
        "crapAfter": 12.0,
        "complexityBefore": 12,
        "complexityAfter": 12,
        "coverageBefore": 0.35,
        "coverageAfter": 0.95,
        "status": "fixed"
      }
    ],
    "regressed": [
      {
        "fullName": "MyApp.Data.Repository.Query(string) : IEnumerable",
        "crapBefore": 15.0,
        "crapAfter": 22.0,
        "delta": 7.0,
        "status": "regressed"
      }
    ],
    "improved": [
      {
        "fullName": "MyApp.Utils.StringHelper.Parse(string) : Result",
        "crapBefore": 25.0,
        "crapAfter": 10.0,
        "delta": -15.0,
        "status": "improved"
      }
    ],
    "added": [
      {
        "fullName": "MyApp.Services.NewService.DoWork() : void",
        "crap": 5.0,
        "isCrappy": false,
        "status": "added"
      }
    ],
    "removed": [
      {
        "fullName": "MyApp.Legacy.OldService.Run() : void",
        "crap": 85.0,
        "wasCrappy": true,
        "status": "removed"
      }
    ]
  }
}
```

#### 10.2.2 Diff Classification Rules

Methods are matched by `fullName` across the two reports:

| Condition | Category | `status` value |
|---|---|---|
| In "after" only | Added | `added` |
| In "before" only | Removed | `removed` |
| Was NOT CRAPpy, now IS CRAPpy | New CRAPpy | `new_crappy` |
| Was CRAPpy, now NOT CRAPpy | Fixed | `fixed` |
| CRAP score increased (but not crossing threshold) | Regressed | `regressed` |
| CRAP score decreased (but not crossing threshold) | Improved | `improved` |
| CRAP score unchanged (within ±0.01) | Unchanged | (omitted from output) |

> **Design note:** Unchanged methods are omitted from the `methods` section to keep
> diff output compact. The `summary.unchanged` count tracks how many were omitted.

#### 10.2.3 Diff Test Scenarios

| Scenario | Before | After | Expected |
|---|---|---|---|
| Method added | not present | CRAP=5 | `added`, `isCrappy: false` |
| Method removed | CRAP=85 | not present | `removed`, `wasCrappy: true` |
| Method becomes CRAPpy | CRAP=25 | CRAP=45 | `new_crappy` |
| Method fixed | CRAP=45 | CRAP=12 | `fixed` |
| CRAP worsened but still clean | CRAP=5 | CRAP=15 | `regressed`, `delta: 10` |
| CRAP improved but still CRAPpy | CRAP=80 | CRAP=50 | `improved`, `delta: -30` |
| No change | CRAP=10 | CRAP=10 | omitted (unchanged) |
| Empty before report | 0 methods | 50 methods | all `added` |
| Empty after report | 50 methods | 0 methods | all `removed` |

### 10.3 Cognitive Complexity Integration

Optionally support **Cognitive Complexity** (SonarSource, 2016) as an alternative or
supplemental complexity metric. Cognitive complexity better captures human readability
while cyclomatic complexity better captures testability.

```
CRAP_cognitive(m) = cogcomp(m)^2 * (1 - cov(m))^3 + cogcomp(m)
```

### 10.4 Weighted CRAP

Weight CRAP scores by method size (lines of code) to prioritize large complex methods
over small complex ones:

```
WeightedCRAP(m) = CRAP(m) * log2(LOC(m) + 1)
```
