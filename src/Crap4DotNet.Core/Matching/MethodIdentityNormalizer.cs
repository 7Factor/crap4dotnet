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
