namespace Crap4DotNet.Core.Coverage;

/// <summary>
/// Coverage data for a single method as read from Cobertura XML.
/// Identity fields are in raw Cobertura format (CLR type names, etc.).
/// The normalizer converts these to canonical form for matching against Roslyn results.
/// </summary>
public sealed record CoberturaMethodCoverage
{
    public required string ClassName { get; init; }
    public required string MethodName { get; init; }
    public required string Signature { get; init; }
    public string? FileName { get; init; }

    /// <summary>
    /// First source line this entry describes, where the report provides one.
    /// Compiler-generated types keep the positions of the method they were rewritten
    /// from, which is what makes overloads separable once the signature is gone.
    /// </summary>
    public int? StartLine { get; init; }

    public required double Coverage { get; init; }
}
