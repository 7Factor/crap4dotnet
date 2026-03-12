# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

crap4dotnet calculates CRAP (Change Risk Anti-Patterns) metrics for .NET code. It combines Roslyn-based cyclomatic complexity analysis with Cobertura code coverage data to score methods: `CRAP = complexity² × (1 - coverage)³ + complexity`. The CLI tool is `dotnet-crap`.

## Commands

```bash
dotnet build                              # build all projects
dotnet test                               # run all tests (217 core + 23 CLI)
dotnet test --filter "FullyQualifiedName~SummaryReportWriter"  # run tests matching a name
dotnet test tests/Crap4DotNet.Core.Tests  # run one test project
dotnet test tests/Crap4DotNet.Cli.Tests   # run CLI integration tests

# install tool locally for manual testing
dotnet pack src/Crap4DotNet.Cli -o ./nupkg
dotnet tool install -g Crap4DotNet --add-source ./nupkg

# run the tool
dotnet-crap analyze ./crap4dotnet.sln --run-tests --output report.json
dotnet-crap diff before.json after.json
```

## Architecture

```
CLI (System.CommandLine)              Core Library
┌──────────────────────┐    ┌────────────────────────────────────┐
│ AnalyzeCommand       │───▶│ CrapAnalyzer (orchestrator)        │
│ DiffCommand          │    │   ├─ RoslynMethodParser            │
│ ErrorOutput          │    │   │   (syntax tree → complexity)   │
│ Program (entry)      │    │   ├─ CoberturaCoverageReader       │
└──────────────────────┘    │   │   (XML → coverage entries)     │
                            │   ├─ MethodCoverageMatcher         │
                            │   │   (joins source + coverage)    │
                            │   └─ CrapCalculator                │
                            │       (CRAP formula per method)    │
                            │                                    │
                            │ Reporting/                         │
                            │   ├─ JsonReportWriter              │
                            │   ├─ SummaryReportWriter           │
                            │   └─ DiffEngine                    │
                            │                                    │
                            │ Models/                            │
                            │   ProjectCrapData → MethodCrapData │
                            │   CrapStatistics, MethodIdentity   │
                            └────────────────────────────────────┘
```

**Data flow:** source files → Roslyn parses complexity → Cobertura XML parsed → methods matched by identity → CRAP calculated per method → statistics aggregated → report written (JSON to file, summary to stdout).

**Two commands:** `analyze` (main workflow) and `diff` (compare two JSON reports).

## Build Configuration

- .NET 8.0, C# latest, nullable enabled, implicit usings
- **Warnings are errors** — all projects via `Directory.Build.props`
- Analyzer level: `latest-recommended` (CA1305 etc. enforced — use `CultureInfo.InvariantCulture` for all formatting)
- xUnit + FluentAssertions + NSubstitute for tests

## Key Conventions

- Report writers are **static classes** with a `Write` method (see `JsonReportWriter`, `SummaryReportWriter`)
- `AnalyzeCommand` handles all CLI validation, file resolution, and orchestration — it's the glue between CLI and Core
- Models are **records** with `required` properties
- The `--output` flag changes behavior: with it, JSON goes to file and summary to stdout; without it, JSON goes to stdout (for piping)
- Exit code 1 = CRAPpy methods found, exit code 2 = configuration/parse error

## Workflow

1. **Work from GitHub issues.** Every change should map to a GitHub issue. If one doesn't exist for the task at hand, offer to create one before starting work.
2. **Work on a branch.** Branch naming: `<github-handle>/<issue-number>/<short-description>`, e.g. `goneflyin/1/publish-to-nuget`.
3. **Submit via pull request.** Push the branch to GitHub and open a PR. Don't merge directly to main.

## Specs

Detailed specifications live in `docs/`:
- `01-crap-metric-specification.md` — language-agnostic CRAP definition
- `02-gap-analysis.md` — what was missing vs the spec
- `03-implementation-plan.md` — original build plan
