using System.Text.RegularExpressions;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Shared utilities for canonical method key operations:
/// name-only key extraction, signature parsing, generic normalization.
/// </summary>
public static partial class MethodKeyHelper
{
    /// <summary>
    /// Extract the name-only key (without signature) from a canonical key.
    /// Used for fallback matching when exact keys don't match.
    /// </summary>
    public static string GetNameOnlyKey(string canonicalKey)
    {
        var sigStart = FindSignatureStart(canonicalKey);
        return sigStart >= 0 ? canonicalKey[..sigStart] : canonicalKey;
    }

    /// <summary>
    /// Find the start index of the method signature (the last balanced paren group).
    /// </summary>
    public static int FindSignatureStart(string fullName)
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
    /// Split a comma-separated type list, respecting nested angle brackets.
    /// </summary>
    public static List<string> SplitTypeList(string typeList)
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

    /// <summary>
    /// Strip the generic arity marker from the method-name segment only.
    /// </summary>
    /// <remarks>
    /// Roslyn knows a method is generic and writes <c>Foo&lt;&gt;</c>; a Cobertura
    /// <c>&lt;method name&gt;</c> carries no method-level arity at all, so the two keys can
    /// never be equal while the marker survives. Class-level arity is left alone: Cobertura
    /// does encode that, as a backtick (<c>Cache`1</c>), and dropping it would make
    /// <c>Cache&lt;T&gt;.Get</c> and a non-generic <c>Cache.Get</c> collide.
    /// Only the final segment is touched, and only when its angle brackets are balanced.
    /// </remarks>
    public static string StripMethodGenericArity(string namePart)
    {
        var depth = 0;
        var lastDot = -1;
        for (var i = 0; i < namePart.Length; i++)
        {
            switch (namePart[i])
            {
                case '<': depth++; break;
                case '>': depth--; break;
                case '.' when depth == 0: lastDot = i; break;
            }
        }

        var segStart = lastDot + 1;
        var segment = namePart[segStart..];
        var open = segment.IndexOf('<', StringComparison.Ordinal);
        if (open < 0 || !segment.EndsWith('>'))
            return namePart;

        return namePart[..segStart] + segment[..open];
    }

    /// <summary>
    /// Reduce a normalized signature to the information both sides can actually carry.
    /// </summary>
    /// <remarks>
    /// Two kinds of detail exist on the Roslyn side and nowhere in a CLR signature, and each
    /// silently blocks the exact-signature pass — which is the only pass that can resolve an
    /// overload set, because the name-only fallback deliberately refuses an ambiguous one:
    /// <list type="bullet">
    /// <item>parameter modifiers: C# distinguishes <c>out</c>/<c>in</c>/<c>ref</c>, the CLR
    /// records one by-ref marker, so all three fold to <c>ref</c>;</item>
    /// <item>nullable-reference annotations: <c>string?</c> and <c>string</c> are the same CLR
    /// type. <c>?</c> is dropped on both sides rather than one, so <c>int?</c> (Roslyn) and
    /// <c>Nullable&lt;int&gt;</c> (Cobertura, which normalizes to <c>int?</c>) still agree.</item>
    /// </list>
    /// The cost is that an overload set differing <em>only</em> by nullability or by
    /// <c>out</c> vs <c>ref</c> becomes ambiguous; such a set cannot be declared in C# anyway
    /// for the modifier case, and the name-only pass still refuses rather than guessing.
    /// </remarks>
    public static string NormalizeSignatureForMatching(string signature)
    {
        if (string.IsNullOrEmpty(signature) || signature == "()")
            return "()";
        if (!signature.StartsWith('(') || !signature.EndsWith(')'))
            return signature;

        var inner = signature[1..^1];
        if (string.IsNullOrWhiteSpace(inner))
            return "()";

        var reduced = SplitTypeList(inner).Select(t =>
        {
            var x = t.Trim();
            if (x.StartsWith("out ", StringComparison.Ordinal))
                x = "ref " + x[4..];
            else if (x.StartsWith("in ", StringComparison.Ordinal))
                x = "ref " + x[3..];
            return x.Replace("?", "", StringComparison.Ordinal);
        });

        return "(" + string.Join(", ", reduced) + ")";
    }

    /// <summary>
    /// Convert CLR backtick generic arity notation to angle bracket notation.
    /// Cache`1 → Cache&lt;&gt;, Dictionary`2 → Dictionary&lt;,&gt;
    /// </summary>
    public static string NormalizeBacktickGenerics(string name) =>
        BacktickGenericRegex().Replace(name, match =>
        {
            var arity = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return "<" + new string(',', arity - 1) + ">";
        });

    [GeneratedRegex(@"`(\d+)")]
    internal static partial Regex BacktickGenericRegex();

    [GeneratedRegex(@"<([^>]+)>")]
    internal static partial Regex GenericTypeParamRegex();
}
