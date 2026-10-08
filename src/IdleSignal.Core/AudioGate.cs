namespace IdleSignal.Core;

public readonly record struct GateSnapshot(bool Enabled, bool IsIdle, double ProminenceDb, double LevelDb);

public sealed class AudioGate
{
    private readonly ToneDetector _detector;
    private readonly double _fadeStep;
    private int _enabled, _generation, _processedGeneration, _publishedGeneration, _idle;
    private double _gain = 1, _prominence, _level = -100;

    public AudioGate(double sampleRate, DetectorSettings settings)
    {
        _detector = new ToneDetector(sampleRate, settings);
        _fadeStep = 1 / Math.Max(1, Math.Round(sampleRate * .005));
    }

    public GateSnapshot Snapshot => new(Volatile.Read(ref _enabled) != 0,
        Volatile.Read(ref _enabled) != 0 && Volatile.Read(ref _generation) == Volatile.Read(ref _publishedGeneration) && Volatile.Read(ref _idle) != 0,
        Volatile.Read(ref _prominence), Volatile.Read(ref _level));

    public void SetEnabled(bool enabled)
    {
        if (Interlocked.Exchange(ref _enabled, enabled ? 1 : 0) != (enabled ? 1 : 0)) Reset();
    }

    public void Reset() => Interlocked.Increment(ref _generation);

    public void Process(Span<float> audio, int channels = 1)
    {
        if (channels is not (1 or 2) || audio.Length % channels != 0) throw new ArgumentException("Expected mono samples or complete stereo pairs.");
        var generation = Volatile.Read(ref _generation);
        if (_processedGeneration != generation)
        {
            _detector.Reset(); _gain = 1; _processedGeneration = generation;
        }
        if (Volatile.Read(ref _enabled) == 0) return;
        for (var i = 0; i < audio.Length; i += channels)
        {
            var sample = channels == 1 ? audio[i] : (float)(((double)audio[i] + audio[i + 1]) / 2);
            _detector.Push(sample);
            if (!float.IsFinite(sample)) { for (var c=0;c<channels;c++) audio[i+c] = 0; _gain = 1; continue; }
            _gain = _detector.IsIdle ? Math.Max(0, _gain - _fadeStep) : 1;
            for (var c=0;c<channels;c++) audio[i+c] = (float)(audio[i+c] * _gain);
        }
        Volatile.Write(ref _idle, _detector.IsIdle ? 1 : 0);
        Volatile.Write(ref _prominence, _detector.ProminenceDb);
        Volatile.Write(ref _level, _detector.LevelDb);
        Volatile.Write(ref _publishedGeneration, generation);
    }
}
