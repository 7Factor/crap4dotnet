using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Tests;

internal static class TestHelpers
{
    internal static MethodCrapData MakeMethod(
        double crapScore,
        int complexity = 1,
        double coverage = 1.0,
        double crapLoad = 0,
        bool isCrappy = false,
        SeverityBand severity = SeverityBand.Low,
        string ns = "MyApp",
        string className = "Service",
        string methodName = "DoWork",
        string? fullName = null) => new()
    {
        Identity = new MethodIdentity
        {
            Namespace = ns,
            ClassName = className,
            MethodName = methodName,
            Signature = "()",
            FullName = fullName ?? $"{ns}.{className}.{methodName}()"
        },
        CrapScore = crapScore,
        Complexity = complexity,
        Coverage = coverage,
        CrapLoad = crapLoad,
        IsCrappy = isCrappy,
        Severity = severity
    };
}
