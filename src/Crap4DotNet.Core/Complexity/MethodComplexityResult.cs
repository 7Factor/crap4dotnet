using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Complexity;

/// <summary>
/// Result of complexity analysis for a single method.
/// </summary>
public sealed record MethodComplexityResult
{
    public required MethodIdentity Identity { get; init; }
    public required int Complexity { get; init; }
}
