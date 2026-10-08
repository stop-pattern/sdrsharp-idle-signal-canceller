namespace IdleSignal.Core;

public sealed class ToneDetector
{
    public const int FrameMs = 40;
    private readonly DetectorSettings _settings;
    private readonly int _frameLength;
    private readonly int _toneBins;
    private readonly double[] _window, _coefficients, _previous, _previous2;
    private int _position, _acquiredMs, _absentMs;
    private double _energy, _sum;

    public ToneDetector(double sampleRate, DetectorSettings settings)
    {
        settings.Validate();
        if (!double.IsFinite(sampleRate) || sampleRate < 8000 || sampleRate > 192000 ||
            settings.CenterHz + settings.ToleranceHz + 350 >= sampleRate / 2)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _settings = settings;
        _frameLength = (int)Math.Round(sampleRate * FrameMs / 1000);
        _toneBins = (int)Math.Ceiling(2 * settings.ToleranceHz / 25) + 1;
        _window = new double[_frameLength];
        _coefficients = new double[_toneBins + 8];
        _previous = new double[_coefficients.Length];
        _previous2 = new double[_coefficients.Length];
        for (var i = 0; i < _window.Length; i++) _window[i] = .5 - .5 * Math.Cos(2 * Math.PI * i / (_frameLength - 1));
        for (var i = 0; i < _toneBins; i++)
            _coefficients[i] = 2 * Math.Cos(2 * Math.PI * (settings.CenterHz - settings.ToleranceHz + i * 2 * settings.ToleranceHz / (_toneBins - 1)) / sampleRate);
        for (var i = 0; i < 8; i++)
        {
            var offset = settings.ToleranceHz + 150 + (i % 4) * 50;
            _coefficients[_toneBins + i] = 2 * Math.Cos(2 * Math.PI * (settings.CenterHz + (i < 4 ? -offset : offset)) / sampleRate);
        }
    }

    public bool IsIdle { get; private set; }
    public double ProminenceDb { get; private set; }
    public double LevelDb { get; private set; } = -100;

    public bool Push(float sample)
    {
        if (!float.IsFinite(sample)) { Reset(); return false; }
        _energy += (double)sample * sample;
        _sum += sample;
        var weighted = sample * _window[_position++];
        for (var i = 0; i < _coefficients.Length; i++)
        {
            var current = weighted + _coefficients[i] * _previous[i] - _previous2[i];
            _previous2[i] = _previous[i];
            _previous[i] = current;
        }
        if (_position < _frameLength) return false;
        CompleteFrame();
        return true;
    }

    private void CompleteFrame()
    {
        double peak = 0, noise = 0;
        for (var i = 0; i < _coefficients.Length; i++)
        {
            var power = Math.Max(0, _previous[i] * _previous[i] + _previous2[i] * _previous2[i] - _coefficients[i] * _previous[i] * _previous2[i]);
            if (i < _toneBins) peak = Math.Max(peak, power); else noise += power;
        }
        LevelDb = 10 * Math.Log10(Math.Max(1e-10, _energy / _frameLength - Math.Pow(_sum / _frameLength, 2)));
        ProminenceDb = Math.Clamp(10 * Math.Log10(Math.Max(1e-30, peak) / Math.Max(1e-30, noise / 8)), -100, 100);
        var present = LevelDb >= _settings.MinimumLevelDb && ProminenceDb >= _settings.ThresholdDb - (IsIdle ? 3 : 0);
        if (present)
        {
            _absentMs = 0;
            _acquiredMs = Math.Min(_acquiredMs + FrameMs, _settings.AcquireMs);
            if (_acquiredMs >= _settings.AcquireMs) IsIdle = true;
        }
        else
        {
            _acquiredMs = 0;
            _absentMs = Math.Min(_absentMs + FrameMs, _settings.ReleaseMs);
            if (_absentMs >= _settings.ReleaseMs) IsIdle = false;
        }
        ClearFrame();
    }

    private void ClearFrame()
    {
        Array.Clear(_previous); Array.Clear(_previous2);
        _position = 0; _energy = 0; _sum = 0;
    }

    public void Reset()
    {
        ClearFrame(); _acquiredMs = 0; _absentMs = 0;
        IsIdle = false; ProminenceDb = 0; LevelDb = -100;
    }
}
