namespace IdleSignal.Core;

public sealed record DetectorSettings
{
    public double CenterHz { get; init; } = 1500;
    public double ToleranceHz { get; init; } = 50;
    public double ThresholdDb { get; init; } = 7;
    public int AcquireMs { get; init; } = 160;
    public int ReleaseMs { get; init; } = 80;
    public double MinimumLevelDb { get; init; } = -65;

    public void Validate()
    {
        if (!double.IsFinite(CenterHz) || CenterHz < 500 || CenterHz > 3000 ||
            !double.IsFinite(ToleranceHz) || ToleranceHz < 25 || ToleranceHz > 150 ||
            !double.IsFinite(ThresholdDb) || ThresholdDb < 3 || ThresholdDb > 30 ||
            AcquireMs < 80 || AcquireMs > 1000 || ReleaseMs < 40 || ReleaseMs > 400 ||
            !double.IsFinite(MinimumLevelDb) || MinimumLevelDb < -100 || MinimumLevelDb > -10)
            throw new ArgumentOutOfRangeException(nameof(DetectorSettings), "Invalid tone detector settings.");
    }
}
