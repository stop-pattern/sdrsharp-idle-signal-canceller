using IdleSignal.Core;
using SDRSharp.Radio;

namespace SDRSharp.IdleSignalCanceller;

public sealed class IdleAudioProcessor : IRealProcessor
{
    private readonly object _configurationLock = new();
    private DetectorSettings _settings = new();
    private AudioGate? _gate;
    private double _sampleRate;
    private int _userEnabled, _receiving, _nfm = 1, _hookEnabled = 1, _reset, _processedReset;
    private string? _error;

    public bool Enabled { get => Volatile.Read(ref _hookEnabled) != 0; set { Volatile.Write(ref _hookEnabled,value?1:0);Reset(); } }
    public double SampleRate { set { lock(_configurationLock) { _sampleRate=value;PrepareGate(); } } }
    public bool UserEnabled => Volatile.Read(ref _userEnabled)!=0;
    public bool Receiving => Volatile.Read(ref _receiving)!=0;
    public bool SupportedMode => Volatile.Read(ref _nfm)!=0;
    public string? Error => Volatile.Read(ref _error);
    public GateSnapshot Snapshot => Volatile.Read(ref _gate)?.Snapshot ?? default;

    public void SetEnabled(bool enabled)
    {
        Volatile.Write(ref _userEnabled,enabled?1:0);
        Volatile.Read(ref _gate)?.SetEnabled(enabled && Receiving && SupportedMode);
        Reset();
    }

    public void SetStreamState(bool receiving, DetectorType mode)
    {
        Volatile.Write(ref _receiving,receiving?1:0);
        Volatile.Write(ref _nfm,mode==DetectorType.NFM?1:0);
        Volatile.Read(ref _gate)?.SetEnabled(UserEnabled && receiving && mode==DetectorType.NFM);
        Reset();
    }

    public void Configure(DetectorSettings settings)
    {
        settings.Validate();
        lock(_configurationLock) { _settings=settings;PrepareGate(); }
    }

    private void PrepareGate()
    {
        AudioGate? next=null;string? error=null;
        try { next=new AudioGate(_sampleRate,_settings); next.SetEnabled(UserEnabled && Receiving && SupportedMode); }
        catch(ArgumentOutOfRangeException) { error="音声サンプルレートが未設定または対応範囲外です"; }
        Volatile.Write(ref _gate,next);Volatile.Write(ref _error,error);Reset();
    }

    public void Reset() => Interlocked.Increment(ref _reset);

    public unsafe void Process(float* buffer,int length)
    {
        if(buffer==null || length<=0 || (length&1)!=0)return;
        var gate=Volatile.Read(ref _gate);if(gate is null)return;
        var generation=Volatile.Read(ref _reset);
        if(_processedReset!=generation){gate.Reset();_processedReset=generation;}
        gate.SetEnabled(UserEnabled && Receiving && SupportedMode && Enabled);
        // SDR# 1921 FilteredAudioOutput: interleaved L,R, SampleRate per channel.
        gate.Process(new Span<float>(buffer,length),2);
    }
}
