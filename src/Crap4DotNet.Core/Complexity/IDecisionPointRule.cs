using Microsoft.CodeAnalysis;

namespace Crap4DotNet.Core.Complexity;

/// <summary>
/// Determines whether a syntax node represents a decision point that
/// increments cyclomatic complexity. Composed at startup for OCP —
/// adding new language constructs requires only a new rule implementation.
/// </summary>
public interface IDecisionPointRule
{
    /// <summary>
    /// Returns true if the node is a decision point per this rule.
    /// </summary>
    bool IsDecisionPoint(SyntaxNode node);
}
