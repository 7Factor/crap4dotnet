using Crap4DotNet.Core.Matching;
using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Analysis;

/// <summary>
/// Complete result of a CRAP analysis run, including diagnostic warnings.
/// </summary>
public sealed record AnalysisResult
{
    public required ProjectCrapData Data { get; init; }
    public required IReadOnlyList<DiagnosticWarning> Warnings { get; init; }
}
