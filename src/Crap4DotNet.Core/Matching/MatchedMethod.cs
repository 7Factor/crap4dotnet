using Crap4DotNet.Core.Complexity;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// A complexity result paired with its coverage value after the join.
/// </summary>
public sealed record MatchedMethod
{
    public required MethodComplexityResult Complexity { get; init; }
    public required double Coverage { get; init; }
}
