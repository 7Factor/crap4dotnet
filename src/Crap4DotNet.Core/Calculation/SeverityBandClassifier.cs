using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Calculation;

/// <summary>
/// Classifies a CRAP score into a severity band per spec 3.3.
/// Bands: [1,5] Low, (5,15] Moderate, (15,30] Elevated, (30,60] High, (60,+inf) Critical.
/// </summary>
public static class SeverityBandClassifier
{
    public static SeverityBand Classify(double crapScore) => crapScore switch
    {
        <= 5.0 => SeverityBand.Low,
        <= 15.0 => SeverityBand.Moderate,
        <= 30.0 => SeverityBand.Elevated,
        <= 60.0 => SeverityBand.High,
        _ => SeverityBand.Critical
    };
}
