using Crap4DotNet.Core.Coverage;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Normalizes Cobertura CLR method names into canonical matching keys.
/// Converts CLR type names to C# keywords, backtick generics to angle brackets,
/// nested type separators, and accessor/operator naming conventions.
/// </summary>
public static class CoberturaMethodParser
{
    private static readonly Dictionary<string, string> ClrToCSharpTypes = new(StringComparer.Ordinal)
    {
        ["System.String"] = "string",
        ["System.Int32"] = "int",
        ["System.Int64"] = "long",
        ["System.Int16"] = "short",
        ["System.Boolean"] = "bool",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.Decimal"] = "decimal",
        ["System.Char"] = "char",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.UInt16"] = "ushort",
        ["System.UInt32"] = "uint",
        ["System.UInt64"] = "ulong",
        ["System.Object"] = "object",
        ["System.Void"] = "void",
        ["System.IntPtr"] = "nint",
        ["System.UIntPtr"] = "nuint",
    };

    private static readonly Dictionary<string, string> ClrOperatorNames = new(StringComparer.Ordinal)
    {
        ["op_Addition"] = "operator +",
        ["op_Subtraction"] = "operator -",
        ["op_Multiply"] = "operator *",
        ["op_Division"] = "operator /",
        ["op_Modulus"] = "operator %",
        ["op_Equality"] = "operator ==",
        ["op_Inequality"] = "operator !=",
        ["op_LessThan"] = "operator <",
        ["op_GreaterThan"] = "operator >",
        ["op_LessThanOrEqual"] = "operator <=",
        ["op_GreaterThanOrEqual"] = "operator >=",
        ["op_BitwiseAnd"] = "operator &",
        ["op_BitwiseOr"] = "operator |",
        ["op_ExclusiveOr"] = "operator ^",
        ["op_LeftShift"] = "operator <<",
        ["op_RightShift"] = "operator >>",
        ["op_UnaryNegation"] = "operator -",
        ["op_UnaryPlus"] = "operator +",
        ["op_LogicalNot"] = "operator !",
        ["op_OnesComplement"] = "operator ~",
        ["op_Increment"] = "operator ++",
        ["op_Decrement"] = "operator --",
        ["op_True"] = "operator true",
        ["op_False"] = "operator false",
    };

    /// <summary>
    /// Normalize a Cobertura coverage entry into a canonical matching key.
    /// </summary>
    public static string ToCanonicalKey(CoberturaMethodCoverage coverage)
    {
        var className = NormalizeClassName(coverage.ClassName);
        var methodName = NormalizeMethodName(coverage.MethodName, className);
        var signature = NormalizeSignature(coverage.Signature);
        return $"{className}.{methodName}"
               + MethodKeyHelper.NormalizeSignatureForMatching(signature);
    }

    private static string NormalizeClassName(string className)
    {
        // Nested type separator: / → .
        var result = className.Replace('/', '.');
        // Generic arity: `1 → <>, `2 → <,>
        return MethodKeyHelper.NormalizeBacktickGenerics(result);
    }

    private static string NormalizeMethodName(string methodName, string normalizedClassName)
    {
        // Constructor: .ctor / .cctor → simple class name
        if (methodName is ".ctor" or ".cctor")
        {
            var lastDot = normalizedClassName.LastIndexOf('.');
            var simpleName = lastDot >= 0 ? normalizedClassName[(lastDot + 1)..] : normalizedClassName;
            // Strip generic notation for constructor name
            var genericIdx = simpleName.IndexOf('<');
            return genericIdx >= 0 ? simpleName[..genericIdx] : simpleName;
        }

        // Property accessors: get_X → X.get, set_X → X.set
        if (methodName.StartsWith("get_", StringComparison.Ordinal))
            return methodName[4..] + ".get";
        if (methodName.StartsWith("set_", StringComparison.Ordinal))
            return methodName[4..] + ".set";

        // Event accessors: add_X → X.add, remove_X → X.remove
        if (methodName.StartsWith("add_", StringComparison.Ordinal))
            return methodName[4..] + ".add";
        if (methodName.StartsWith("remove_", StringComparison.Ordinal))
            return methodName[7..] + ".remove";

        // Operators: op_Addition → operator +
        if (ClrOperatorNames.TryGetValue(methodName, out var opName))
            return opName;

        // Conversion operators: op_Implicit / op_Explicit
        if (methodName is "op_Implicit")
            return "implicit operator";
        if (methodName is "op_Explicit")
            return "explicit operator";

        // Generic methods: Find`1 → Find<>
        return MethodKeyHelper.NormalizeBacktickGenerics(methodName);
    }

    private static string NormalizeSignature(string signature)
    {
        if (string.IsNullOrEmpty(signature) || signature == "()")
            return "()";

        if (!signature.StartsWith('(') || !signature.EndsWith(')'))
            return signature;

        var inner = signature[1..^1];
        if (string.IsNullOrWhiteSpace(inner))
            return "()";

        var types = MethodKeyHelper.SplitTypeList(inner);
        var normalized = types.Select(NormalizeSingleType);
        return "(" + string.Join(", ", normalized) + ")";
    }

    internal static string NormalizeSingleType(string clrType)
    {
        var trimmed = clrType.Trim();

        // By-reference: System.Int32& → ref int
        if (trimmed.EndsWith('&'))
        {
            var inner = NormalizeSingleType(trimmed[..^1]);
            return "ref " + inner;
        }

        // Array: System.String[] → string[]
        if (trimmed.EndsWith("[]", StringComparison.Ordinal))
        {
            var inner = NormalizeSingleType(trimmed[..^2]);
            return inner + "[]";
        }

        // Pointer: System.Int32* → int*
        if (trimmed.EndsWith('*'))
        {
            var inner = NormalizeSingleType(trimmed[..^1]);
            return inner + "*";
        }

        // CLR primitive types (exact match)
        if (ClrToCSharpTypes.TryGetValue(trimmed, out var csharpType))
            return csharpType;

        // Generic types: System.Collections.Generic.List`1<System.String> → List<string>
        var backtickIdx = trimmed.IndexOf('`');
        if (backtickIdx >= 0)
        {
            var angleIdx = trimmed.IndexOf('<', backtickIdx);
            if (angleIdx >= 0)
            {
                var baseName = trimmed[..backtickIdx];
                var genericArgs = trimmed[(angleIdx + 1)..^1];
                var normalizedBase = StripNamespace(baseName);
                var normalizedArgs = MethodKeyHelper.SplitTypeList(genericArgs).Select(NormalizeSingleType);

                // Nullable<T> → T?
                if (baseName is "System.Nullable" or "Nullable")
                {
                    var innerType = string.Join(", ", normalizedArgs);
                    return innerType + "?";
                }

                return normalizedBase + "<" + string.Join(", ", normalizedArgs) + ">";
            }

            // Backtick without angle brackets (open generic in signature — rare)
            var baseWithoutArity = trimmed[..backtickIdx];
            return StripNamespace(baseWithoutArity) + MethodKeyHelper.NormalizeBacktickGenerics(trimmed[backtickIdx..]);
        }

        // Non-generic, non-primitive: strip namespace
        return StripNamespace(trimmed);
    }

    private static string StripNamespace(string typeName)
    {
        // Only strip up to the last dot that isn't inside a nested type
        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
    }
}
