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

**Important:** Coverage MUST come from automated test execution. Manual testing
coverage is explicitly excluded from CRAP analysis.

### 6.3 Method Identification

Each method MUST be uniquely identified by:
- **Namespace/Package** (fully qualified)
- **Type/Class name**
- **Method name**
- **Method signature** (parameter types for overload disambiguation)
- **Full qualified method** (combined display string)

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

## 8. Language-Specific Considerations

### 8.1 Java-Specific Features (from original crap4j)

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

### 8.2 Universally Applicable Features

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

## 9. Extended Metrics (Optional Enhancements)

### 9.1 CRAP Trend Analysis

Track CRAP metrics over time (per build/commit) to identify:
- **Improving trend**: Total CRAP Load decreasing
- **Degrading trend**: New CRAPpy methods being introduced
- **Fixed methods**: Previously CRAPpy methods now below threshold
- **New CRAPpy methods**: Methods that newly exceed the threshold

### 9.2 CRAP Diff (Build Comparison)

Compare two analysis runs to identify:
- New CRAPpy methods (methods that became CRAPpy since last run)
- Fixed CRAPpy methods (methods that were CRAPpy but are now below threshold)
- Changed CRAP scores (methods whose score increased or decreased)

### 9.3 Cognitive Complexity Integration

Optionally support **Cognitive Complexity** (SonarSource, 2016) as an alternative or
supplemental complexity metric. Cognitive complexity better captures human readability
while cyclomatic complexity better captures testability.

```
CRAP_cognitive(m) = cogcomp(m)^2 * (1 - cov(m))^3 + cogcomp(m)
```

### 9.4 Weighted CRAP

Weight CRAP scores by method size (lines of code) to prioritize large complex methods
over small complex ones:

```
WeightedCRAP(m) = CRAP(m) * log2(LOC(m) + 1)
```
