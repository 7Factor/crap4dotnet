using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Matching;

/// <summary>
/// Normalizes Roslyn-sourced MethodIdentity into a canonical matching key.
/// Strips generic type parameter names (Cache&lt;T&gt; → Cache&lt;&gt;)
/// and ensures trailing () for empty signatures.
/// </summary>
public static class RoslynMethodParser
{
    /// <summary>
    /// Normalize a Roslyn MethodIdentity into a canonical matching key.
    /// </summary>
    public static string ToCanonicalKey(MethodIdentity identity)
    {
        var fullName = identity.FullName;

        // Property accessors have empty signature → add () for consistent matching
        if (!fullName.EndsWith(')')
            && !fullName.EndsWith('>')) // don't add () to generic names without sig
            fullName += "()";

        return NormalizeGenericTypeParams(fullName);
    }

    /// <summary>
    /// Strip generic type parameter names from the name portion of a FullName,
    /// preserving parameter types in the signature.
    /// Cache&lt;T&gt;.Get(string) → Cache&lt;&gt;.Get(string)
    /// </summary>
    private static string NormalizeGenericTypeParams(string fullName)
    {
        var sigStart = MethodKeyHelper.FindSignatureStart(fullName);
        if (sigStart < 0)
        {
            // No signature parens — normalize the whole string
            return MethodKeyHelper.GenericTypeParamRegex().Replace(fullName, match =>
            {
                var paramCount = MethodKeyHelper.SplitTypeList(match.Groups[1].Value).Count;
                return "<" + new string(',', paramCount - 1) + ">";
            });
        }

        var namePart = fullName[..sigStart];
        var sigPart = fullName[sigStart..];

        var normalized = MethodKeyHelper.GenericTypeParamRegex().Replace(namePart, match =>
        {
            var paramCount = MethodKeyHelper.SplitTypeList(match.Groups[1].Value).Count;
            return "<" + new string(',', paramCount - 1) + ">";
        });

        return normalized + sigPart;
    }
}
