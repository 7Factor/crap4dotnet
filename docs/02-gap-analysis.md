# Gap Analysis: Java (crap4j) → C# / .NET (crap4dotnet)

> **Version:** 1.1
> **Date:** 2026-03-11
> **Target Consumer:** AI coding agents via CLI

---

## 1. Executive Summary

The CRAP metric formula and aggregation logic are **fully language-agnostic** and port directly.
The gaps fall into two categories: (A) Java-specific implementation details that must be replaced
with .NET equivalents, and (B) .NET-specific features that should be added to make a useful
CLI tool for AI coding agents operating against .NET codebases.

crap4dotnet is **CLI-only** — no IDE plugins, CI/CD pipeline integrations, or web dashboards.
The tool is designed to be invoked by AI agents that parse structured JSON output to make
decisions about code quality, refactoring priorities, and test coverage gaps.

---

## 2. Features to Drop (Too Java-Specific)

| Java Feature | Why It Doesn't Apply | Disposition |
|---|---|---|
| **ASM bytecode analysis** | Analyzes JVM `.class` files using the ASM library to build control flow graphs and compute cyclomatic complexity from bytecode opcodes. .NET uses IL (Intermediate Language), not JVM bytecode. | **Replace** with Roslyn syntax tree analysis |
| **EMMA coverage reader** | EMMA is a Java-only code coverage tool. Its binary data format is proprietary to the JVM ecosystem. | **Replace** with Coverlet/Cobertura reader |
| **Eclipse plugin** | Eclipse is a Java IDE. The plugin API (`org.eclipse.ui`) is entirely Java-specific. | **Replace** with `dotnet tool` CLI (no IDE plugin needed) |
| **JUnit test discovery/execution** | crap4j automatically finds and runs JUnit tests. This is tightly coupled to the JUnit runner and Java classpath. | **Replace** with `dotnet test` integration |
| **Java package name conventions** | Uses `/` to `.` replacement for JVM internal names, `$` for inner classes. | **Replace** with .NET namespace/type resolution |
| **Synthetic/bridge method filtering** | JVM generates synthetic bridge methods for generics and covariant return types. These bytecode artifacts don't exist in .NET IL. | **Drop** — not needed in .NET |
| **Ant build integration** | Crapertura uses Ant tasks. Ant is Java-only. | **Drop** — CLI-only tool, no build system integration |
| **JDOM/dom4j XML parsing** | Java-specific XML libraries. | **Replace** with `System.Xml.Linq` |

---

## 3. Features That Map Directly

| Feature | Java Implementation | .NET Equivalent | Effort |
|---|---|---|---|
| **CRAP formula** | `comp^2 * (1-cov)^3 + comp` | Identical formula | Trivial |
| **CRAP Load formula** | `comp * (1-cov) + comp/threshold` | Identical formula | Trivial |
| **Threshold (default 30)** | Configurable constant | Configurable constant | Trivial |
| **Aggregation statistics** | Mean, median, stddev, counts | Identical (use `MathNet.Numerics` or manual) | Low |
| **Cobertura XML reading** | `CoberturaXMLReportReader` | Same XML format — Coverlet outputs Cobertura XML | Low |
| **XML report output** | `CrapReportWriter` writes `<crap_result>` XML | Same schema, use `System.Xml.Linq` | Low |
| **Method-level granularity** | Methods identified by class + name + signature | Methods identified by type + name + signature | Low |
| **Report output** | Jenkins `crap4j-plugin` reads XML reports | JSON primary format for agent consumption; XML optional | Low |
| **Histogram generation** | Distribution of scores across bins | Same logic | Low |
| **CRAP diff / comparison** | `CrapDataComparer` in Jenkins plugin | Same algorithm | Low |

---

## 4. .NET-Specific Features to ADD

### 4.1 Must-Have Additions

| Feature | Rationale | Implementation Approach |
|---|---|---|
| **Roslyn-based complexity analysis** | .NET's compiler-as-a-service (Roslyn) enables source-level analysis without bytecode. More accurate than bytecode analysis, works with any .NET language. | Use `Microsoft.CodeAnalysis.CSharp` to walk syntax trees and count decision points |
| **Coverlet integration** | Coverlet is the standard .NET code coverage tool, outputs Cobertura XML. Already produces the format we need. | Parse `coverage.cobertura.xml` from `dotnet test --collect:"XPlat Code Coverage"` |
| **`dotnet tool` CLI** | AI agents invoke CLI tools directly. Must be installable via `dotnet tool install -g crap4dotnet` | Package as a .NET Global Tool via NuGet |
| **Structured JSON output** | AI agents parse JSON natively. This is the primary output format. | `System.Text.Json` serialization with well-defined schema |
| **File path + line numbers in output** | AI agents need to navigate directly to problematic methods | Roslyn provides `SyntaxNode.GetLocation()` for source mapping |
| **Non-zero exit codes** | AI agents use exit codes to determine pass/fail without parsing output | Return 0 = clean, 1 = CRAPpy methods found, 2 = error |
| **stdin/stdout piping** | AI agents may pipe coverage data in or chain with other tools | Support `--coverage -` to read from stdin, write JSON to stdout by default |
| **C# property accessor analysis** | C# properties (`get`/`set`) are methods that should be analyzed but are syntactically special. | Roslyn walker handles `AccessorDeclarationSyntax` |
| **async/await awareness** | `async` methods generate state machines. Complexity should reflect the source, not the generated code. | Analyze source-level syntax, not IL |
| **LINQ expression complexity** | LINQ query/method syntax contains hidden complexity (`.Where()`, `.Select()`, query comprehensions). | Configurable: count LINQ lambdas as complexity contributors |
| **Pattern matching complexity** | C# 9+ has complex pattern matching (`switch expressions`, `is` patterns, `and`/`or` combinators). | Count each pattern arm/case as a decision point |
| **Nullable reference type handling** | `?.`, `??`, `??=` operators create implicit branches. | Configurable: count null-conditional operators |
| **Record/struct analysis** | Records have synthesized methods (`Equals`, `GetHashCode`, `ToString`). | Filter generated members unless opted-in |
| **Source generator awareness** | Source generators produce code that shouldn't be attributed to the developer. | Detect `[GeneratedCode]` attribute, exclude by default |

### 4.2 Nice-to-Have Additions

| Feature | Rationale | Implementation Approach |
|---|---|---|
| **Cognitive Complexity** | SonarQube uses cognitive complexity; offer as alternative metric | Implement SonarSource's cognitive complexity algorithm |
| **OpenCover format** | Some .NET projects use OpenCover format | Parse OpenCover XML alongside Cobertura |
| **IL-level analysis** | For scenarios without source (analyzing NuGet packages) | Use `System.Reflection.Metadata` or `Mono.Cecil` |
| **Single-file analysis** | AI agents may want to analyze a single changed file, not the whole project | Accept a file path instead of project/solution |
| **Diff mode (two reports)** | AI agents can compare before/after to verify their refactoring improved scores | Compare two JSON reports, output delta |
| **Method filter by name/pattern** | AI agents may want to check CRAP for a specific method they just modified | `--filter "MyNamespace.MyClass.MyMethod"` glob/regex support |
| **Quiet/machine mode** | Suppress all human-readable output, emit only JSON to stdout | `--quiet` flag for strict machine consumption |

### 4.3 Explicitly Out of Scope

The following features are **not needed** for the AI-agent-only use case:

- IDE plugins (Visual Studio, Rider, ReSharper)
- CI/CD pipeline integrations (Jenkins, GitHub Actions, Azure DevOps)
- HTML dashboards or interactive reports
- MSBuild/build-system integration
- SARIF report format
- NDepend interoperability
- Watch mode / file system monitoring
- Human-oriented color-coded console output

---

## 5. Complexity Calculation: Java vs C#

### 5.1 Java (crap4j original)

crap4j uses **two approaches** to calculate cyclomatic complexity:

1. **Control Flow Graph (CFG) approach** via ASM `Analyzer`:
   - Build CFG from bytecode
   - Complexity = Edges - Nodes + 2
   - Most mathematically correct

2. **Decision counting approach** via `ComplexityMethodVisitor`:
   - Walk bytecode instructions
   - Start at complexity = 1
   - Increment for each conditional branch opcode (`IFEQ`, `IFNE`, `IFLT`, etc.)
   - Increment for each `TABLESWITCH`/`LOOKUPSWITCH` label
   - Produces the same result for most code

### 5.2 C# (crap4dotnet approach)

Use **Roslyn syntax tree walking** to count decision points:

```
Complexity = 1
  + count(IfStatementSyntax)               // includes "else if" — see note below
  + count(ForStatementSyntax)
  + count(ForEachStatementSyntax)
  + count(WhileStatementSyntax)
  + count(DoStatementSyntax)
  + count(CaseSwitchLabelSyntax)           // traditional switch cases
  + count(CasePatternSwitchLabelSyntax)    // pattern switch cases
  + count(non-discard SwitchExpressionArmSyntax)  // exclude DiscardPatternSyntax arms
  + count(CatchClauseSyntax)
  + count(ConditionalExpressionSyntax)     // ternary ?:
  + count(BinaryExpression where kind is LogicalAnd or LogicalOr)  // && ||
  + count(CoalesceExpression)              // ?? (configurable)
  + count(ConditionalAccessExpression)     // ?. (configurable)
```

> **IMPORTANT — `else if` handling:** In the Roslyn AST, `else if (x)` is parsed as an
> `ElseClause` containing a child `IfStatementSyntax`. The `CSharpSyntaxWalker` will
> naturally visit that inner `IfStatementSyntax` when walking the tree. Therefore, counting
> all `IfStatementSyntax` nodes already includes `else if`. Do NOT separately count
> `ElseClause` nodes — this would double-count every `else if` branch.
>
> **IMPORTANT — Switch expression arms:** Count each `SwitchExpressionArmSyntax` whose
> pattern is NOT a `DiscardPatternSyntax`. Do not use "count all arms minus 1" as this
> breaks when there is no discard/default arm.

### 5.3 Key Differences

| Construct | Java (bytecode) | C# (Roslyn) | Notes |
|---|---|---|---|
| `if/else` | Branch opcodes | `IfStatementSyntax` | Equivalent |
| `for/while/do` | Branch opcodes + gotos | Specific syntax nodes | Equivalent |
| `switch/case` | TABLESWITCH/LOOKUPSWITCH | `CaseSwitchLabelSyntax` | Equivalent |
| `try/catch` | Not counted in crap4j | `CatchClauseSyntax` | **Add** — catches are branches |
| `&&` / `||` | Compiled to branch opcodes | `BinaryExpressionSyntax` | Equivalent |
| `?.` (null-conditional) | N/A in Java < 8 | `ConditionalAccessExpression` | **.NET-specific** — configurable |
| `??` (null-coalesce) | N/A | `CoalesceExpression` | **.NET-specific** — configurable |
| `switch expression` | N/A | `SwitchExpressionSyntax` | **C# 8+ specific** |
| Pattern matching | N/A | Various pattern syntaxes | **C# 9+ specific** |
| LINQ queries | N/A | Query/method chains | **.NET-specific** — configurable |
| `async/await` | N/A | State machine generation | Analyze source, not generated |

---

## 6. Coverage Tools: Java vs .NET

| Aspect | Java Ecosystem | .NET Ecosystem |
|---|---|---|
| **Primary tool** | JaCoCo (modern) / EMMA (legacy) / Cobertura | **Coverlet** (standard, cross-platform) |
| **Built-in VS support** | N/A | VS 2026 Enterprise/Community/Professional |
| **Output format** | Cobertura XML, EMMA binary, JaCoCo XML | **Cobertura XML** (default from Coverlet). Use `branch-rate` preferred, `line-rate` fallback for branchless methods |
| **How to generate** | Maven/Gradle plugins | `dotnet test --collect:"XPlat Code Coverage"` |
| **Method-level coverage** | Available in all formats | Available in Cobertura XML |
| **Branch coverage** | Available | Available via Coverlet |
| **Integration command** | Tool-specific | Standardized via `dotnet test` |

**Key insight:** Coverlet already outputs Cobertura XML format, which is the same format that
crapertura reads. This means the XML parsing logic is largely reusable.

---

## 7. Distribution & Packaging: Java vs .NET

| Aspect | Java (crap4j) | .NET (crap4dotnet) |
|---|---|---|
| **Distribution** | Eclipse update site | **NuGet package** (Global Tool) |
| **Installation** | Eclipse plugin manager | `dotnet tool install -g crap4dotnet` |
| **CLI usage** | Not available (GUI only) | `dotnet crap analyze <project>` |
| **Primary output** | Eclipse GUI + XML reports | **JSON to stdout** (machine-readable) |
| **Configuration** | Eclipse project settings | `.crap4dotnet.json` or CLI flags |
| **Build integration** | Ant task (crapertura) | None (CLI-only, invoked by AI agents) |
| **IDE/CI integration** | Eclipse plugin / Jenkins plugin | None (out of scope) |

---

## 8. Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Roslyn API breaking changes | Medium | Pin to stable `Microsoft.CodeAnalysis` versions; use LTS |
| Coverage format fragmentation | Low | Cobertura XML is dominant; add OpenCover as fallback |
| Performance on large solutions | Medium | Incremental analysis; parallel method processing |
| C# language evolution | Medium | Abstract syntax node types behind interfaces; update per C# release |
| Complexity parity with Java | Low | Validate against known codebases with both tools |
| NDepend overlap | Low | Position as free/open-source alternative; interop with NDepend data |
