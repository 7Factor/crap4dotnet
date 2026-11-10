---
name: crap
description: Analyze C# code for CRAP (Change Risk Anti-Patterns) metrics using dotnet-crap
license: MIT
compatibility: Requires the .NET 8.0 runtime or later. The dotnet-crap global tool must be installed.
metadata:
  author: 7Factor
  version: "0.1.0"
---

You have access to `dotnet-crap`, a .NET global tool that computes CRAP scores for C# code. CRAP combines cyclomatic complexity with code coverage to find methods that are both complex and poorly tested.

## Prerequisites

```bash
dotnet tool install -g Crap4DotNet
```

Verify with `dotnet-crap --version`.

## How to Analyze a Project

### Step 1: Run CRAP Analysis

The simplest approach — let crap4dotnet run tests and generate coverage automatically:

```bash
dotnet-crap analyze <path> --run-tests
```

Or if the user already has Cobertura coverage files:

```bash
dotnet-crap analyze <path> --coverage <coverage-xml-path>
```

- `<path>` can be a `.cs` file, directory, `.csproj`, `.sln`, or `.slnx` (`--run-tests` requires `.csproj`, `.sln`, `.slnx`, or directory)
- `--run-tests` runs `dotnet test` with coverage collection automatically (mutually exclusive with `--coverage`). It does not work when `global.json` selects Microsoft.Testing.Platform as the test runner; see below.
- `--coverage` accepts one or more Cobertura XML paths (globs work)
- `--threshold <n>` sets the CRAP threshold for one run. A project sets its own in `crap4dotnet-config.json` (`{ "crap": { "threshold": 30 } }`). The default is 30.
- `--min-crap <n>` filters out low-scoring methods from output
- `--output <path>` writes JSON to a file instead of stdout

#### Microsoft.Testing.Platform projects

When `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`, `--run-tests` exits 2 with `UNSUPPORTED_TEST_RUNNER`. Generate Cobertura coverage with the `Microsoft.Testing.Extensions.CodeCoverage` package instead, then pass it with `--coverage`:

```bash
dotnet test --project <test-project> --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
dotnet-crap analyze <path> --coverage <test-project>/bin/Debug/<tfm>/TestResults/coverage.cobertura.xml
```

### Step 2: Parse the JSON Output

The tool outputs JSON to stdout. Key fields:

- `stats.crappyMethodCount` — number of methods exceeding the threshold
- `stats.averageCrap` — project-wide average CRAP score
- `methods[]` — array of per-method results, each with:
  - `fullName` — fully qualified method name
  - `filePath` and `lineNumber` — source location (navigate directly)
  - `crap` — the CRAP score
  - `complexity` — cyclomatic complexity
  - `coverage` — code coverage (0.0 to 1.0)
  - `isCrappy` — whether it exceeds the threshold
  - `severity` — one of: `low`, `moderate`, `elevated`, `high`, `critical`

### Step 3: Present Results

Summarize findings for the user:
1. Total methods analyzed and how many are CRAPpy
2. List the worst offenders (highest CRAP scores) with file paths and line numbers
3. For each CRAPpy method, suggest whether to **add tests** (high complexity, low coverage) or **refactor** (very high complexity where even 100% coverage won't help — complexity 31+ is always CRAPpy)

## Comparing Reports (Diff)

To track changes over time:

```bash
dotnet-crap diff <before.json> <after.json>
```

The diff output classifies methods as: `added`, `removed`, `new_crappy`, `fixed`, `regressed`, `improved`. Focus on `new_crappy` (newly risky) and `fixed` (wins to celebrate).

## Exit Codes

- **0** — All methods below threshold
- **1** — One or more CRAPpy methods found
- **2** — Error (missing files, parse failure)

## Severity Bands

| CRAP Score | Severity | Action |
|------------|----------|--------|
| 1 – 5 | Low | No action needed |
| 6 – 15 | Moderate | Consider adding tests |
| 16 – 30 | Elevated | Prioritize test coverage |
| 31 – 60 | High | Refactor and/or add tests |
| 60+ | Critical | Urgent refactoring required |

## Key Insight

A method with complexity >= 31 is **always CRAPpy** regardless of coverage, because `CRAP(31, 1.0) = 31 > 30`. For these methods, the only fix is reducing complexity through refactoring.
