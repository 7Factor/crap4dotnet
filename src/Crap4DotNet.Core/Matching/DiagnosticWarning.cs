namespace Crap4DotNet.Core.Matching;

/// <summary>
/// A diagnostic warning emitted during the coverage-complexity join.
/// </summary>
public sealed record DiagnosticWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}
