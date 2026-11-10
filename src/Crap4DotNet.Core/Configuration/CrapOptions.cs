namespace Crap4DotNet.Core.Configuration;

/// <summary>
/// Configuration options for CRAP analysis. Threshold must be greater than 0.
/// </summary>
public sealed record CrapOptions
{
    /// <summary>
    /// Used when neither <c>--threshold</c> nor <c>crap4dotnet-config.json</c> sets a threshold.
    /// </summary>
    public const int DefaultThreshold = 30;

    private readonly int _threshold;

    public required int Threshold
    {
        get => _threshold;
        init
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(Threshold), value, "Threshold must be greater than 0.");
            _threshold = value;
        }
    }
}
