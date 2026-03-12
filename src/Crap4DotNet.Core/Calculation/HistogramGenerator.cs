using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Calculation;

/// <summary>
/// Generates CRAP score histogram with half-open interval bins per spec 3.4.
/// A score of exactly 5.0 falls in the "5-10" bin (half-open: [lower, upper)).
/// </summary>
public static class HistogramGenerator
{
    private static readonly (string Range, double Lower, double Upper)[] BinDefinitions =
    [
        ("0-5", 0, 5),
        ("5-10", 5, 10),
        ("10-15", 10, 15),
        ("15-20", 15, 20),
        ("20-25", 20, 25),
        ("25-30", 25, 30),
        ("30-40", 30, 40),
        ("40-50", 40, 50),
        ("50-75", 50, 75),
        ("75-100", 75, 100),
        ("100+", 100, double.PositiveInfinity)
    ];

    public static IReadOnlyList<HistogramBin> Generate(IReadOnlyList<MethodCrapData> methods)
    {
        var totalCount = methods.Count;
        var bins = new HistogramBin[BinDefinitions.Length];

        for (var i = 0; i < BinDefinitions.Length; i++)
        {
            var (range, lower, upper) = BinDefinitions[i];
            var count = methods.Count(m => m.CrapScore >= lower && m.CrapScore < upper);
            var percent = totalCount > 0 ? (double)count / totalCount * 100.0 : 0.0;
            bins[i] = new HistogramBin { Range = range, Count = count, Percent = percent };
        }

        return bins;
    }
}
