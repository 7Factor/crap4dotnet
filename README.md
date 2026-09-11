# crap4dotnet

[![NuGet](https://img.shields.io/nuget/v/Crap4DotNet)](https://www.nuget.org/packages/Crap4DotNet)
[![CI](https://github.com/7Factor/crap4dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/7Factor/crap4dotnet/actions/workflows/ci.yml)

A .NET global tool that computes **CRAP (Change Risk Anti-Patterns)** scores for your C# code. CRAP combines cyclomatic complexity with code coverage to identify methods that are both complex and poorly tested — the riskiest code in your project.

Designed for **AI agent consumption**: JSON to stdout, structured errors to stderr, meaningful exit codes.

Inspired by Robert Martin's (Uncle Bob) [crap4clj](https://github.com/unclebob/crap4clj), a port of crap4j to Clojure built to assist with AI-assisted software development. crap4dotnet brings the same capability to the .NET ecosystem.

## What is CRAP?

The CRAP metric was [introduced by Alberto Savoia](http://www.artima.com/weblogs/viewpost.jsp?thread=210575) to answer: *"Is this method too complex for its level of test coverage?"*

```
CRAP(m) = complexity(m)² × (1 - coverage(m))³ + complexity(m)
```

| Complexity | Coverage | CRAP Score | Verdict |
|------------|----------|------------|---------|
| 1 | 100% | 1.0 | Perfect |
| 10 | 80% | 10.8 | Fine |
| 20 | 50% | 70.0 | CRAPpy |
| 20 | 0% | 420.0 | Very CRAPpy |
| 30 | 100% | 30.0 | At threshold (OK) |
| 31 | 100% | 31.0 | CRAPpy even with full coverage |

Methods with a CRAP score above the threshold (default: 30) are **CRAPpy** — they need refactoring, better tests, or both.

## Install

```bash
dotnet tool install -g Crap4DotNet
```

Requires .NET 8.0 or later.

## Quick Start

Coverage is measured by [Coverlet](https://github.com/coverlet-coverage/coverlet), so every
test project needs a `coverlet.collector` package reference. crap4dotnet reads the Cobertura
XML Coverlet produces and pairs it with the complexity it calculates from your source.

```bash
# Option A: let crap4dotnet run `dotnet test` for you and pick up the coverage
dotnet-crap analyze ./MyApp.sln --run-tests

# Option B: Generate coverage yourself, then analyze
dotnet test --collect:"XPlat Code Coverage"
dotnet-crap analyze ./src/MyProject \
  --coverage ./TestResults/**/coverage.cobertura.xml

# View results (JSON to stdout)
# Exit code 0 = clean, 1 = CRAPpy methods found
```

## Commands

### `analyze`

Analyze C# source code for CRAP metrics.

```
dotnet-crap analyze <path> [options]
```

| Argument/Option | Description |
|-----------------|-------------|
| `<path>` | Path to `.cs` file, directory, `.csproj`, or `.sln` |
| `--coverage <path>` | Path(s) to Cobertura XML coverage file(s) |
| `--run-tests` | Run `dotnet test --collect:"XPlat Code Coverage"` and use the coverage it produces |
| `--threshold <n>` | CRAP threshold (default: 30) |
| `--output <path>` | Write JSON to file instead of stdout |
| `--min-crap <n>` | Only include methods with CRAP >= this value |

```bash
# Run tests and analyze in one step
dotnet-crap analyze ./MyApp.sln --run-tests

# Analyze a solution with pre-generated coverage
dotnet-crap analyze ./MyApp.sln --coverage ./coverage.cobertura.xml

# Save report and filter noisy low-CRAP methods
dotnet-crap analyze ./src --coverage ./coverage.xml --output report.json --min-crap 15

# Stricter threshold
dotnet-crap analyze ./src --coverage ./coverage.xml --threshold 15
```

### `diff`

Compare two CRAP analysis reports to track changes over time.

```
dotnet-crap diff <before> <after> [options]
```

| Argument/Option | Description |
|-----------------|-------------|
| `<before>` | Path to the "before" JSON report |
| `<after>` | Path to the "after" JSON report |
| `--output <path>` | Write diff JSON to file instead of stdout |

```bash
# Compare reports before and after refactoring
dotnet-crap diff baseline.json current.json
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All methods are below the CRAP threshold |
| 1 | One or more methods exceed the CRAP threshold |
| 2 | Error (missing files, parse failure, invalid arguments) |

## JSON Output

The `analyze` command outputs a JSON report to stdout:

```json
{
  "schemaVersion": "1.0",
  "project": "MyApp",
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
      "isCrappy": true,
      "severity": "high"
    }
  ]
}
```

Coverage is normalized to 0.0–1.0. The `filePath` and `lineNumber` fields let AI agents navigate directly to problematic methods.

## Diff Reports

The `diff` command classifies each method by what changed:

| Priority | Category | Meaning |
|----------|----------|---------|
| 1 | `added` | New method (in "after" only) |
| 2 | `removed` | Deleted method (in "before" only) |
| 3 | `new_crappy` | Crossed above the CRAP threshold |
| 4 | `fixed` | Crossed below the CRAP threshold |
| 5 | `regressed` | CRAP score increased |
| 6 | `improved` | CRAP score decreased |
| 7 | — | Unchanged (omitted from output) |

Threshold-crossing categories (`new_crappy`, `fixed`) take priority over direction-only categories (`regressed`, `improved`).

## Severity Bands

Each method in the report includes a `severity` classification:

| CRAP Score | Severity | Recommended Action |
|------------|----------|-------------------|
| 1 – 5 | Low | No action needed |
| 6 – 15 | Moderate | Consider adding tests |
| 16 – 30 | Elevated | Prioritize test coverage |
| 31 – 60 | High | Refactor and/or add tests |
| 60+ | Critical | Urgent refactoring required |

## Architecture

The tool is split into two assemblies:

- **Crap4DotNet.Core** — All analysis logic: Roslyn-based cyclomatic complexity walker, Cobertura coverage reader, method matching, CRAP formula, statistics, and JSON report generation. No MSBuild dependency — uses syntax-tree-only analysis for fast startup.
- **Crap4DotNet.Cli** — Thin command-line layer using `System.CommandLine`. Wires up the pipeline and handles I/O.

Complexity analysis is purely syntactic (no semantic model needed), so the tool works without loading MSBuild workspaces or compiling the project.

## Building from Source

```bash
git clone https://github.com/7Factor/crap4dotnet.git
cd crap4dotnet
dotnet build
dotnet test
```

## License

MIT
