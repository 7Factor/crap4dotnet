using System.Text.RegularExpressions;
using Crap4DotNet.Core.Coverage;
using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Normalizes method identities from Cobertura (CLR names) and Roslyn (C# names)
/// into a canonical form for matching. Both sides produce the same key format:
/// <c>Namespace.ClassName.MethodName(param1, param2)</c>
/// with generics normalized to arity notation (<c>Cache&lt;&gt;</c>, <c>Dict&lt;,&gt;</c>).
/// </summary>
public static partial class MethodIdentityNormalizer
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
    /// Normalize a Roslyn MethodIdentity into a canonical matching key.
    /// Strips generic type parameter names (Cache&lt;T&gt; → Cache&lt;&gt;)
    /// and ensures a trailing () for empty signatures (property accessors).
    /// </summary>
    public static string NormalizeFromRoslyn(MethodIdentity identity)
    {
        var fullName = identity.FullName;

        // Property accessors have empty signature → add () for consistent matching
        if (!fullName.EndsWith(')')
            && !fullName.EndsWith('>')) // don't add () to generic names without sig
            fullName += "()";

        return NormalizeGenericTypeParams(fullName);
    }

    /// <summary>
    /// Normalize a Cobertura coverage entry into a canonical matching key.
    /// Converts CLR type names to C# keywords, backtick generics to angle brackets,
    /// nested type separators, and accessor/operator naming conventions.
    /// </summary>
    public static string NormalizeFromCobertura(CoberturaMethodCoverage coverage)
    {
        var className = NormalizeCoberturaClassName(coverage.ClassName);
        var methodName = NormalizeCoberturaMethodName(coverage.MethodName, className);
        var signature = NormalizeCoberturaSignature(coverage.Signature);
        return $"{className}.{methodName}{signature}";
    }

    /// <summary>
    /// Extract the name-only key (without signature) from a canonical key.
    /// Used for fallback matching when exact keys don't match.
    /// </summary>
    public static string GetNameOnlyKey(string canonicalKey)
    {
        var sigStart = FindSignatureStart(canonicalKey);
        return sigStart >= 0 ? canonicalKey[..sigStart] : canonicalKey;
    }

    // --- Cobertura normalization ---

    private static string NormalizeCoberturaClassName(string className)
    {
        // Nested type separator: / → .
        var result = className.Replace('/', '.');
        // Generic arity: `1 → <>, `2 → <,>
        return NormalizeBacktickGenerics(result);
    }

    private static string NormalizeCoberturaMethodName(string methodName, string normalizedClassName)
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
        return NormalizeBacktickGenerics(methodName);
    }

    private static string NormalizeCoberturaSignature(string signature)
    {
        if (string.IsNullOrEmpty(signature) || signature == "()")
            return "()";

        if (!signature.StartsWith('(') || !signature.EndsWith(')'))
            return signature;

        var inner = signature[1..^1];
        if (string.IsNullOrWhiteSpace(inner))
            return "()";

        var types = SplitTypeList(inner);
        var normalized = types.Select(NormalizeSingleType);
        return "(" + string.Join(", ", normalized) + ")";
    }

    private static string NormalizeSingleType(string clrType)
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
                var normalizedArgs = SplitTypeList(genericArgs).Select(NormalizeSingleType);

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
            return StripNamespace(baseWithoutArity) + NormalizeBacktickGenerics(trimmed[backtickIdx..]);
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

    /// <summary>
    /// Split a comma-separated type list, respecting nested angle brackets.
    /// </summary>
    private static List<string> SplitTypeList(string typeList)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < typeList.Length; i++)
        {
            switch (typeList[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(typeList[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        if (start < typeList.Length)
            result.Add(typeList[start..].Trim());

        return result;
    }

    // --- Roslyn normalization ---

    /// <summary>
    /// Strip generic type parameter names from the name portion of a FullName,
    /// preserving parameter types in the signature.
    /// Cache&lt;T&gt;.Get(string) → Cache&lt;&gt;.Get(string)
    /// </summary>
    private static string NormalizeGenericTypeParams(string fullName)
    {
        var sigStart = FindSignatureStart(fullName);
        if (sigStart < 0)
        {
            // No signature parens — normalize the whole string
            return GenericTypeParamRegex().Replace(fullName, match =>
            {
                var paramCount = SplitTypeList(match.Groups[1].Value).Count;
                return "<" + new string(',', paramCount - 1) + ">";
            });
        }

        var namePart = fullName[..sigStart];
        var sigPart = fullName[sigStart..];

        var normalized = GenericTypeParamRegex().Replace(namePart, match =>
        {
            var paramCount = SplitTypeList(match.Groups[1].Value).Count;
            return "<" + new string(',', paramCount - 1) + ">";
        });

        return normalized + sigPart;
    }

    /// <summary>
    /// Find the start index of the method signature (the last balanced paren group).
    /// </summary>
    private static int FindSignatureStart(string fullName)
    {
        if (!fullName.EndsWith(')'))
            return -1;

        var depth = 0;
        for (var i = fullName.Length - 1; i >= 0; i--)
        {
            if (fullName[i] == ')')
                depth++;
            else if (fullName[i] == '(')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Convert CLR backtick generic arity notation to angle bracket notation.
    /// Cache`1 → Cache&lt;&gt;, Dictionary`2 → Dictionary&lt;,&gt;
    /// </summary>
    private static string NormalizeBacktickGenerics(string name) =>
        BacktickGenericRegex().Replace(name, match =>
        {
            var arity = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return "<" + new string(',', arity - 1) + ">";
        });

    [GeneratedRegex(@"`(\d+)")]
    private static partial Regex BacktickGenericRegex();

    [GeneratedRegex(@"<([^>]+)>")]
    private static partial Regex GenericTypeParamRegex();
}
