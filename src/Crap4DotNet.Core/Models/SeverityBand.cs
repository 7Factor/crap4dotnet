namespace Crap4DotNet.Core.Models;

/// <summary>
/// Risk classification for a method's CRAP score.
/// Bands: [1,5] Low, (5,15] Moderate, (15,30] Elevated, (30,60] High, (60,+inf) Critical.
/// </summary>
public enum SeverityBand
{
    Low,
    Moderate,
    Elevated,
    High,
    Critical
}
