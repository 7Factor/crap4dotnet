using Crap4DotNet.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crap4DotNet.Core.Complexity;

/// <summary>
/// Analyzes C# source code for cyclomatic complexity using syntax-tree-only parsing.
/// No MSBuild workspace or semantic model required.
/// </summary>
public static class CyclomaticComplexityWalker
{
    public static IReadOnlyList<MethodComplexityResult> Analyze(string sourceCode, string? filePath = null)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath ?? string.Empty);
        var finder = new MethodFinder(filePath);
        finder.Visit(tree.GetRoot());
        return finder.Results;
    }

    /// <summary>
    /// Finds all method-like declarations and computes complexity for each.
    /// Tracks namespace/type context for building MethodIdentity.
    /// </summary>
    private sealed class MethodFinder : CSharpSyntaxWalker
    {
        private readonly string? _filePath;
        private readonly List<MethodComplexityResult> _results = [];
        private readonly Stack<string> _namespaceStack = new();
        private readonly Stack<string> _typeStack = new();

        public IReadOnlyList<MethodComplexityResult> Results => _results;

        public MethodFinder(string? filePath) => _filePath = filePath;

        private string CurrentNamespace =>
            _namespaceStack.Count > 0 ? string.Join(".", _namespaceStack.Reverse()) : string.Empty;

        private string CurrentType =>
            _typeStack.Count > 0 ? string.Join(".", _typeStack.Reverse()) : string.Empty;

        // === Namespace tracking ===

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            _namespaceStack.Push(node.Name.ToString());
            base.VisitNamespaceDeclaration(node);
            _namespaceStack.Pop();
        }

        public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            _namespaceStack.Push(node.Name.ToString());
            base.VisitFileScopedNamespaceDeclaration(node);
            _namespaceStack.Pop();
        }

        // === Type tracking ===

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            _typeStack.Push(GetTypeIdentifier(node));
            base.VisitClassDeclaration(node);
            _typeStack.Pop();
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            _typeStack.Push(GetTypeIdentifier(node));
            base.VisitStructDeclaration(node);
            _typeStack.Pop();
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            _typeStack.Push(GetTypeIdentifier(node));
            base.VisitRecordDeclaration(node);
            _typeStack.Pop();
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            _typeStack.Push(GetTypeIdentifier(node));
            base.VisitInterfaceDeclaration(node);
            _typeStack.Pop();
        }

        // === Method-like declarations ===

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Skip partial/abstract/interface methods without body
            if (node.Body is null && node.ExpressionBody is null)
                return;

            var methodName = node.Identifier.Text;
            if (node.TypeParameterList is { Parameters.Count: > 0 } tpl)
                methodName += "<" + string.Join(", ", tpl.Parameters.Select(p => p.Identifier.Text)) + ">";

            var signature = BuildSignature(node.ParameterList);
            RecordMethod(methodName, signature, node);
            base.VisitMethodDeclaration(node); // find local functions
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node.Body is null && node.ExpressionBody is null) return;
            var signature = BuildSignature(node.ParameterList);
            RecordMethod(node.Identifier.Text, signature, node);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            RecordMethod("~" + node.Identifier.Text, "()", node);
            base.VisitDestructorDeclaration(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            var operatorName = "operator " + node.OperatorToken.Text;
            var signature = BuildSignature(node.ParameterList);
            RecordMethod(operatorName, signature, node);
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            var kind = node.ImplicitOrExplicitKeyword.Text;
            var operatorName = kind + " operator " + node.Type;
            var signature = BuildSignature(node.ParameterList);
            RecordMethod(operatorName, signature, node);
            base.VisitConversionOperatorDeclaration(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var methodName = node.Identifier.Text;
            if (node.TypeParameterList is { Parameters.Count: > 0 } tpl)
                methodName += "<" + string.Join(", ", tpl.Parameters.Select(p => p.Identifier.Text)) + ">";

            var signature = BuildSignature(node.ParameterList);
            RecordMethod(methodName, signature, node);
            base.VisitLocalFunctionStatement(node); // find nested local functions
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (node.Body is null && node.ExpressionBody is null) return;

            var accessorKind = node.Keyword.Text;
            var memberName = GetAccessorMemberName(node);
            RecordMethod(memberName + "." + accessorKind, string.Empty, node);
            base.VisitAccessorDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // Expression-bodied property (e.g., public int Foo => expr;) is a getter
            if (node.ExpressionBody is not null && node.AccessorList is null)
                RecordMethod(node.Identifier.Text + ".get", string.Empty, node);

            base.VisitPropertyDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (node.ExpressionBody is not null && node.AccessorList is null)
            {
                var indexerParams = BuildBracketSignature(node.ParameterList);
                RecordMethod("this" + indexerParams + ".get", string.Empty, node);
            }

            base.VisitIndexerDeclaration(node);
        }

        // === Top-level statements ===

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var globalStatements = node.Members.OfType<GlobalStatementSyntax>().ToList();
            if (globalStatements.Count > 0)
            {
                var counter = new DecisionPointCounter();
                foreach (var gs in globalStatements)
                    counter.Visit(gs);

                var firstGlobal = globalStatements[0];
                var lineNumber = firstGlobal.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                _results.Add(new MethodComplexityResult
                {
                    Identity = new MethodIdentity
                    {
                        Namespace = string.Empty,
                        ClassName = "Program",
                        MethodName = "<Main>$",
                        Signature = "(string[])",
                        FullName = "Program.<Main>$(string[])",
                        FilePath = _filePath,
                        LineNumber = lineNumber
                    },
                    Complexity = counter.Count
                });
            }

            base.VisitCompilationUnit(node);
        }

        // === Helpers ===

        private void RecordMethod(string methodName, string signature, SyntaxNode declarationNode)
        {
            var body = GetMethodBody(declarationNode);
            var counter = new DecisionPointCounter();
            if (body is not null)
                counter.Visit(body);

            var ns = CurrentNamespace;
            var type = CurrentType;
            var fullName = BuildFullName(ns, type, methodName, signature);
            var lineNumber = declarationNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            _results.Add(new MethodComplexityResult
            {
                Identity = new MethodIdentity
                {
                    Namespace = ns,
                    ClassName = type,
                    MethodName = methodName,
                    Signature = signature,
                    FullName = fullName,
                    FilePath = _filePath,
                    LineNumber = lineNumber
                },
                Complexity = counter.Count
            });
        }

        private static SyntaxNode? GetMethodBody(SyntaxNode node) => node switch
        {
            MethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody,
            ConstructorDeclarationSyntax c => (SyntaxNode?)c.Body ?? c.ExpressionBody,
            DestructorDeclarationSyntax d => (SyntaxNode?)d.Body ?? d.ExpressionBody,
            OperatorDeclarationSyntax o => (SyntaxNode?)o.Body ?? o.ExpressionBody,
            ConversionOperatorDeclarationSyntax co => (SyntaxNode?)co.Body ?? co.ExpressionBody,
            AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody,
            LocalFunctionStatementSyntax lf => (SyntaxNode?)lf.Body ?? lf.ExpressionBody,
            PropertyDeclarationSyntax p => p.ExpressionBody,
            IndexerDeclarationSyntax i => i.ExpressionBody,
            _ => null
        };

        private static string GetTypeIdentifier(TypeDeclarationSyntax node)
        {
            var name = node.Identifier.Text;
            if (node.TypeParameterList is { Parameters.Count: > 0 } tpl)
                name += "<" + string.Join(", ", tpl.Parameters.Select(p => p.Identifier.Text)) + ">";
            return name;
        }

        private static string BuildSignature(ParameterListSyntax? parameterList)
        {
            if (parameterList is null || parameterList.Parameters.Count == 0)
                return "()";

            var types = parameterList.Parameters.Select(p =>
            {
                var prefix = string.Join(" ", p.Modifiers.Select(m => m.Text));
                var typeName = p.Type?.ToString() ?? "?";
                return string.IsNullOrEmpty(prefix) ? typeName : prefix + " " + typeName;
            });

            return "(" + string.Join(", ", types) + ")";
        }

        private static string BuildBracketSignature(BracketedParameterListSyntax? parameterList)
        {
            if (parameterList is null || parameterList.Parameters.Count == 0)
                return "[]";
            var types = parameterList.Parameters.Select(p => p.Type?.ToString() ?? "?");
            return "[" + string.Join(", ", types) + "]";
        }

        private static string BuildFullName(string ns, string type, string methodName, string signature)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(ns)) parts.Add(ns);
            if (!string.IsNullOrEmpty(type)) parts.Add(type);
            parts.Add(methodName);
            return string.Join(".", parts) + signature;
        }

        private static string GetAccessorMemberName(AccessorDeclarationSyntax node)
        {
            return node.Parent?.Parent switch
            {
                PropertyDeclarationSyntax prop => prop.Identifier.Text,
                IndexerDeclarationSyntax indexer => "this" + BuildBracketSignature(indexer.ParameterList),
                EventDeclarationSyntax evt => evt.Identifier.Text,
                _ => "unknown"
            };
        }
    }

    /// <summary>
    /// Counts decision points within a method body. Stops at nested method boundaries
    /// (local functions) since those are analyzed as separate methods.
    /// Lambdas contribute to the enclosing method's complexity.
    /// </summary>
    private sealed class DecisionPointCounter : CSharpSyntaxWalker
    {
        public int Count { get; private set; } = 1; // base complexity

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Count++;
            base.VisitIfStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            Count++;
            base.VisitForStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Count++;
            base.VisitForEachStatement(node);
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            Count++;
            base.VisitForEachVariableStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Count++;
            base.VisitWhileStatement(node);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            Count++;
            base.VisitDoStatement(node);
        }

        public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
        {
            Count++;
            base.VisitCaseSwitchLabel(node);
        }

        public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
        {
            Count++;
            base.VisitCasePatternSwitchLabel(node);
        }

        public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
        {
            // Exclude discard pattern (_) — it's the default arm
            if (node.Pattern is not DiscardPatternSyntax)
                Count++;
            base.VisitSwitchExpressionArm(node);
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            Count++;
            base.VisitCatchClause(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Count++;
            base.VisitConditionalExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
                node.IsKind(SyntaxKind.LogicalOrExpression))
            {
                Count++;
            }

            // Note: CoalesceExpression (??) is configurable, default OFF per spec 6.1
            base.VisitBinaryExpression(node);
        }

        // Note: ConditionalAccessExpression (?.) is configurable, default OFF per spec 6.1

        // Stop at local function boundaries — they are analyzed as separate methods
        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) { }
    }
}
