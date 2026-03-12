# Specification Review #2: Architecture & Implementation Readiness

**Reviewer:** Claude Opus 4.6 (senior .NET architect review)
**Date:** 2026-03-12
**Scope:** All four spec documents + beads issue tracking alignment

---

## 1. Critical Issues

### C1. Threshold boundary semantics remain ambiguous despite prior review

The spec-review.md (item C2) identified the `>=` vs `>` ambiguity for CRAP threshold. The updated spec in section 3.1 now states CRAP(30, 1.0) = 30 "exactly equals the threshold and is NOT CRAPpy," implying strict `>`. However, the CRAP Load formula section 4.1 says Load is "only computed for methods exceeding the CRAP threshold" without stating whether "exceeding" means `>` or `>=`. The implementation plan validation table (section 7.1) has rows showing CRAP=30.0 with Load=0 and notes "(exactly at threshold)" but never states the comparison operator.

**Impact:** Every downstream consumer (calculator, classifier, diff engine) depends on this. An implementer will have to guess.

**Recommendation:** Add a single authoritative statement in section 3.1: "A method is CRAPpy if and only if `crap(m) > threshold` (strict greater-than). Methods exactly at the threshold are NOT CRAPpy and have no CRAP Load."

### C2. NO_METHODS_FOUND is exit code 2 but empty projects are exit code 0 -- contradictory

Section 2.4.5 says a project with zero analyzable methods after filtering exits with code 0 (vacuously true). Section 8.5's error handling table says "No methods after filtering" with "Project with only interfaces" produces exit 2 with `NO_METHODS_FOUND`. These directly contradict each other. A project containing only interfaces has zero analyzable methods after filtering -- is it exit 0 or exit 2?

**Recommendation:** Distinguish "no source files found at all" (exit 2, genuine error) from "source files found but all methods filtered out" (exit 0 with warning, valid empty result).

### C3. Diff classification has overlapping/ambiguous categories

The diff classification rules (10.2.2) do not specify priority order. A method that was NOT CRAPpy (score 25) and is now CRAPpy (score 35) satisfies both "New CRAPpy" and "Regressed" (score increased without crossing... but it did cross). The table implies threshold-crossing categories take priority, but this is not stated. Also, a method that was CRAPpy (score 50) and is still CRAPpy but improved (score 35) is neither "Fixed" nor cleanly "Improved" by the stated rules -- it improved but didn't cross the threshold.

**Recommendation:** State explicit priority order: Added/Removed first, then threshold-crossing (New CRAPpy / Fixed), then direction (Regressed / Improved), then Unchanged.

---

## 2. Significant Concerns

### S1. JSON report writer issue (crap4dotnet-czz) depends on MethodCoverageMatcher (crap4dotnet-5m7) but not CrapCalculator (crap4dotnet-qso)

The dependency graph has czz depending on 5m7 and 8fd, but NOT on qso (CrapCalculator). Yet the report writer needs CRAP scores to write the report. The CLI issue (4yp) depends on both czz and qso. This means the actual orchestration (join complexity+coverage, calculate CRAP, generate report) lives entirely in the CLI layer rather than having a proper pipeline abstraction.

**Recommendation:** Either add a pipeline/orchestrator issue that wires Calculator output into the ReportWriter, or clarify that the CLI issue owns orchestration.

### S2. No XML report writer tracked in beads

The spec (section 7.2) defines XML report output for legacy crap4j compatibility. The gap analysis (section 3) lists it as a feature that maps directly. But there is no beads issue for implementing it. The JSON writer issue (czz) only mentions JSON.

**Recommendation:** Either create an issue for XML report writing or explicitly defer it with a note in the implementation plan.

### S3. Configuration file loading is untracked

The implementation plan section 5 defines a `.crap4dotnet.json` configuration file with detailed schema. No beads issue covers configuration file discovery, parsing, validation, or merging with CLI flags. The CLI issue mentions flags but not config file support.

**Recommendation:** Create a beads issue for configuration file support, or scope it out of v1.

### S4. Coverage field selection logic needs more precision

The spec says "branch-rate preferred, line-rate fallback" but the decision table in section 6.2 has six scenarios. The exact semantics for "branchless method" detection are not specified. Does the tool check `conditions="0"` in the Cobertura XML? Or does it check whether `branch-rate` is absent vs present-but-zero? Coverlet may emit `branch-rate="0"` for methods with no branches (meaning "not applicable") vs methods with branches where none are covered (meaning "0% covered").

**Recommendation:** Add a concrete decision tree: if `branch-rate` attribute is missing, use `line-rate`. If `branch-rate` is present and `conditions > 0`, use `branch-rate`. If `branch-rate` is present but `conditions == 0`, use `line-rate` (branch-rate is meaningless for branchless methods).

### S5. Unit test issues are missing from beads

The implementation plan section 7 defines a testing strategy with formula validation, complexity validation, and E2E validation. The only test-related beads issue is crap4dotnet-qzg (integration tests, P2). Unit tests are mentioned in individual issue descriptions ("Unit tests against known crap4j values") but there is no dedicated issue for the formula validation test suite or the complexity walker test corpus described in section 7.2. This risks tests being treated as an afterthought rather than specification-first.

---

## 3. Improvements

### I1. Five-project solution structure may be over-engineered

The plan calls for Core, Complexity, Coverage, Reporting, and Cli -- five projects plus test projects (10+ total). For a CLI tool of this scope, this is a lot of assembly boundaries. The Complexity and Coverage projects are leaf dependencies with 1-2 public types each. Consider collapsing to three projects: Crap4DotNet.Core (models + calculator + complexity + coverage), Crap4DotNet.Cli (entry point), plus test projects.

### I2. `--filter` flag is mentioned but not specified

The CLI issue (crap4dotnet-4yp) lists `--filter` as a flag but neither the spec nor the plan defines what it filters on (namespace glob? class name regex? method name?), what the syntax is, or how multiple filters interact.

### I3. Diff command lives in a Phase 3/4 section but is tracked as P1

The implementation plan puts diff in "Phase 3: Agent-Oriented Features (Weeks 5-6)" and extended analysis in Phase 4, yet the CLI issue (crap4dotnet-4yp) is P1 and includes "DiffCommand." Clarify whether diff ships in v1 or is deferred.

### I4. `stdin` piping support is unspecified

The gap analysis lists "stdin/stdout piping" as must-have and mentions `--coverage -` to read from stdin. No beads issue covers this, and the Cobertura reader issue describes file-based parsing only.

---

## 4. Issue Tracking Alignment

### Beads issues (11 total) vs. spec/plan coverage:

| Spec Feature | Tracked? | Issue |
|---|---|---|
| Solution setup | Yes | crap4dotnet-mnl |
| Core models | Yes | crap4dotnet-gfd |
| Complexity walker | Yes | crap4dotnet-zsa |
| Coverage reader | Yes | crap4dotnet-3c8 |
| Method matching | Yes | crap4dotnet-5m7 |
| CRAP calculator | Yes | crap4dotnet-qso |
| Statistics/histogram | Yes | crap4dotnet-8fd |
| JSON report | Yes | crap4dotnet-czz |
| CLI (analyze+diff) | Yes | crap4dotnet-4yp |
| NuGet packaging | Yes | crap4dotnet-86y |
| Integration tests | Yes | crap4dotnet-qzg |
| XML report | **No** | -- |
| Config file loading | **No** | -- |
| stdin piping | **No** | -- |
| OpenCover XML support | **No** | -- |
| `--filter` flag semantics | **No** | -- |
| Unit test suites | **Partial** | Mentioned in descriptions but no dedicated issues |

### Dependency graph is sound

The dependency chain is logical: mnl -> gfd -> {3c8, zsa, qso, 8fd} -> {5m7} -> czz -> 4yp -> {86y, qzg}. No circular dependencies. The only structural concern is S1 above (report writer doesn't depend on calculator).

---

## 5. Outstanding Decisions Before Implementation

1. **Threshold comparison operator:** `>` or `>=`? (C1)
2. **Empty project vs no-source-found exit code semantics** (C2)
3. **Diff classification priority order** (C3)
4. **Is XML report in scope for v1?** (S2)
5. **Is config file loading in scope for v1?** (S3)
6. **Branchless method detection in Cobertura XML** (S4)
7. **What does `--filter` filter on?** (I2)
8. **Is `diff` command P1 or deferred?** (I3)
9. **Is stdin piping in scope for v1?** (I4)
10. **Should the `hierarchy` object in JSON be opt-in or always present?** (spec says "included by default" but the JSON schema example in 7.1 doesn't show it)

---

## 6. Summary Assessment

The specifications are well above average for a pre-implementation project. The CRAP formula, complexity counting rules, coverage field selection, error codes, and JSON schemas are all specified with enough detail to build against. The prior spec review caught real issues and the specs were meaningfully updated -- the `else if` clarification and switch expression arm counting fix in the gap analysis (section 5.2) are correct and directly address the original ambiguities A1 and A2.

**Top 3 priorities before writing code:**
1. Resolve the threshold boundary semantics with a single authoritative statement (C1)
2. Reconcile the empty-project vs no-methods-found exit code contradiction (C2)
3. Add the 5 untracked features to beads as either issues or explicit "out of scope for v1" notes

**Readiness:** Phase 1 (core engine) can begin once C1 and C2 are resolved. The beads dependency graph correctly sequences the work. Phase 2 (CLI) needs decisions on I2, I3, and I4 before the CLI issue can be fully implemented.
