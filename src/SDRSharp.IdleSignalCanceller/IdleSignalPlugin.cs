using SDRSharp.Common;
using SDRSharp.Radio;
using System.ComponentModel;
namespace SDRSharp.IdleSignalCanceller;
public sealed class IdleSignalPlugin : ISharpPlugin
{
    private ISharpControl? _control;
    private IdleSignalPanel? _panel;
    private IdleAudioProcessor? _processor;
    public string DisplayName => "空線信号キャンセラー";
    public UserControl Gui => _panel ?? throw new InvalidOperationException("Plugin not initialized.");
    public void Initialize(ISharpControl control)
    {
        if(_control is not null)throw new InvalidOperationException("Plugin already initialized.");
        var processor=new IdleAudioProcessor();processor.SetStreamState(control.IsPlaying,control.DetectorType);
        var panel=new IdleSignalPanel(processor,control.ThemeForeColor,control.ThemePanelColor);
        _control=control;_processor=processor;_panel=panel;
        try {control.RegisterStreamHook(processor,ProcessorType.FilteredAudioOutput);control.PropertyChanged+=HostChanged;}
        catch {Close();throw;}
    }

    private void HostChanged(object? sender,PropertyChangedEventArgs e)
    {
        var control=_control;var processor=_processor;if(control is null || processor is null)return;
        if(e.PropertyName is "IsPlaying" or "DetectorType" or null or "") processor.SetStreamState(control.IsPlaying,control.DetectorType);
        if(e.PropertyName is "Frequency" or "CenterFrequency" or "Source" or "FilterBandwidth" or "AudioSampleRate" or "InputSampleRate") processor.Reset();
    }

    public void Close()
    {
        var control=_control;var processor=_processor;_control=null;_processor=null;
        if(control is not null)
        {
            control.PropertyChanged-=HostChanged;
            if(processor is not null){processor.SetEnabled(false);processor.Enabled=false;control.UnregisterStreamHook(processor);}
        }
        _panel?.Dispose();_panel=null;
    }
}
