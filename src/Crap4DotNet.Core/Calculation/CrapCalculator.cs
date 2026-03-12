namespace Crap4DotNet.Core.Calculation;

/// <summary>
/// Computes the CRAP score for a method.
/// Formula: comp^2 * (1 - cov)^3 + comp
/// </summary>
public static class CrapCalculator
{
    public static double Calculate(int complexity, double coverage)
    {
        var cov = Math.Clamp(coverage, 0.0, 1.0);
        var comp = (double)complexity;
        return comp * comp * Math.Pow(1.0 - cov, 3) + comp;
    }
}
