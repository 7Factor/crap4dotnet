using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Calculation;

/// <summary>
/// Computes aggregated CRAP statistics from a collection of method results.
/// Returns null for averages/median/stddev when the collection is empty (spec 2.4.5).
/// </summary>
public static class CrapStatisticsCalculator
{
    public static CrapStatistics Calculate(IReadOnlyList<MethodCrapData> methods)
    {
        if (methods.Count == 0)
        {
            return new CrapStatistics
            {
                MethodCount = 0,
                TotalCrap = 0,
                AverageCrap = null,
                MedianCrap = null,
                StandardDeviation = null,
                CrappyMethodCount = 0,
                CrappyMethodPercent = 0.0,
                TotalCrapLoad = 0
            };
        }

        var scores = methods.Select(m => m.CrapScore).ToList();
        var totalCrap = scores.Sum();
        var average = totalCrap / scores.Count;
        var median = CalculateMedian(scores);
        var stddev = CalculatePopulationStdDev(scores, average);
        var crappyCount = methods.Count(m => m.IsCrappy);
        var crappyPercent = (double)crappyCount / methods.Count * 100.0;
        var totalLoad = methods.Sum(m => m.CrapLoad);

        return new CrapStatistics
        {
            MethodCount = methods.Count,
            TotalCrap = totalCrap,
            AverageCrap = average,
            MedianCrap = median,
            StandardDeviation = stddev,
            CrappyMethodCount = crappyCount,
            CrappyMethodPercent = crappyPercent,
            TotalCrapLoad = totalLoad
        };
    }

    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private static double CalculatePopulationStdDev(List<double> values, double mean)
    {
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / values.Count);
    }
}
