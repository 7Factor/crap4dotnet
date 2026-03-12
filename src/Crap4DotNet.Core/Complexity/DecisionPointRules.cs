using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crap4DotNet.Core.Complexity;

/// <summary>
/// Standard decision point rules that are always active.
/// Each rule maps to one or more C# syntax constructs per spec 6.1.
/// </summary>
public static class DecisionPointRules
{
    /// <summary>
    /// Returns the default set of rules per the CRAP specification:
    /// all standard decision points, no optional ones.
    /// </summary>
    public static IReadOnlyList<IDecisionPointRule> Default { get; } =
    [
        new BranchingRule(),
        new LoopRule(),
        new CaseRule(),
        new CatchRule(),
        new TernaryRule(),
        new LogicalBinaryRule(),
    ];

    /// <summary>if statements.</summary>
    public sealed class BranchingRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is IfStatementSyntax;
    }

    /// <summary>for, foreach, while, do loops.</summary>
    public sealed class LoopRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is ForStatementSyntax
                or ForEachStatementSyntax
                or ForEachVariableStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax;
    }

    /// <summary>switch case labels (traditional and pattern-based), switch expression arms (excluding discard).</summary>
    public sealed class CaseRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) => node switch
        {
            CaseSwitchLabelSyntax => true,
            CasePatternSwitchLabelSyntax => true,
            SwitchExpressionArmSyntax arm => arm.Pattern is not DiscardPatternSyntax,
            _ => false
        };
    }

    /// <summary>catch clauses.</summary>
    public sealed class CatchRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is CatchClauseSyntax;
    }

    /// <summary>Ternary conditional expressions (? :).</summary>
    public sealed class TernaryRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is ConditionalExpressionSyntax;
    }

    /// <summary>Logical AND (&&) and OR (||) operators.</summary>
    public sealed class LogicalBinaryRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is BinaryExpressionSyntax binary
            && (binary.IsKind(SyntaxKind.LogicalAndExpression)
                || binary.IsKind(SyntaxKind.LogicalOrExpression));
    }

    // === Optional rules (configurable, default OFF per spec 6.1) ===

    /// <summary>Null-coalescing operator (??).</summary>
    public sealed class CoalesceRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.CoalesceExpression);
    }

    /// <summary>Null-conditional access operator (?.).</summary>
    public sealed class ConditionalAccessRule : IDecisionPointRule
    {
        public bool IsDecisionPoint(SyntaxNode node) =>
            node is ConditionalAccessExpressionSyntax;
    }
}
