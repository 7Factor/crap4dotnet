namespace Crap4DotNet.Core.Calculation;

/// <summary>
/// Computes the CRAP Load for a method — a heuristic estimate of effort to bring
/// a CRAPpy method back under threshold. Only non-zero when CRAP > threshold (strict).
/// Formula: comp * (1 - cov) + comp / threshold
/// </summary>
public static class CrapLoadCalculator
{
    public static double Calculate(int complexity, double coverage, int threshold)
    {
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold must be greater than 0.");

        var crapScore = CrapCalculator.Calculate(complexity, coverage);
        if (crapScore <= threshold)
            return 0.0;

        var cov = Math.Clamp(coverage, 0.0, 1.0);
        var comp = (double)complexity;
        return comp * (1.0 - cov) + comp / threshold;
    }
}
