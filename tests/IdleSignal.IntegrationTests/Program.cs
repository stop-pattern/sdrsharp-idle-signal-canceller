using System.ComponentModel;
using System.Reflection;
using SDRSharp.Common;
using SDRSharp.Radio;
using SDRSharp.IdleSignalCanceller;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var tests = new (string, Action)[]
        {
            ("host hook startup bypass", () => WithPlugin((p,h,check) => { var x=Stereo(1500);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"startup changed audio");Assert(!check.Checked,"startup checkbox enabled"); })),
            ("host hook mutes both channels", () => WithPlugin((p,h,check) => {check.Checked=true;var x=Stereo(1500);h.Process(x);Assert(x.AsSpan(16000).ToArray().All(v=>v==0),"host tone leaks");})),
            ("GUI disable restores exact samples", () => WithPlugin((p,h,check) => {check.Checked=true;h.Process(Stereo(1500));check.Checked=false;var x=Stereo(1500);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"UI bypass failed");})),
            ("frequency change clears stale mute", () => WithPlugin((p,h,check) => {check.Checked=true;h.Process(Stereo(1500));h.Raise("Frequency");var x=Stereo(1500,640);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"stale frequency detection");})),
            ("receive restart clears stale mute", () => WithPlugin((p,h,check) => {check.Checked=true;h.Process(Stereo(1500));h.Playing=false;h.Raise("IsPlaying");h.Playing=true;h.Raise("IsPlaying");var x=Stereo(1500,640);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"stale stream detection");})),
            ("sample rate change handles stereo frequency", () => WithPlugin((p,h,check) => {check.Checked=true;h.Hooks.SetProcessorSampleRate(ProcessorType.FilteredAudioOutput,48000);var x=Stereo(1500,48000,48000);h.Process(x);Assert(x.AsSpan(48000).ToArray().All(v=>v==0),"wrong sample rate");})),
            ("unsupported rate bypasses", () => WithPlugin((p,h,check) => {check.Checked=true;h.Hooks.SetProcessorSampleRate(ProcessorType.FilteredAudioOutput,0);var x=Stereo(1500);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"invalid rate altered audio");})),
            ("WFM passes through", () => WithPlugin((p,h,check) => {check.Checked=true;h.Mode=DetectorType.WFM;h.Raise("DetectorType");var x=Stereo(1500);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"WFM was muted");})),
            ("close unregisters and is repeatable", () => {var (p,h)=Create();p.Close();p.Close();var x=Stereo(1500);var original=x.ToArray();h.Process(x);Assert(x.SequenceEqual(original),"closed hook changed audio");}),
            ("panel displays idle state", () => WithPlugin((p,h,check) => {check.Checked=true;h.Process(Stereo(1500));((IdleSignalPanel)p.Gui).RefreshStatus();Assert(Find<Label>(p.Gui,"statusLabel").Text.Contains("ミュート中"),"idle label missing");if(args.Length>0){p.Gui.Size=new System.Drawing.Size(320,440);using var form=new Form {Opacity=0,ShowInTaskbar=false,ClientSize=p.Gui.Size};form.Controls.Add(p.Gui);form.Show();Application.DoEvents();using var bitmap=new System.Drawing.Bitmap(320,440);p.Gui.DrawToBitmap(bitmap,p.Gui.ClientRectangle);bitmap.Save(args[0]);form.Close();}})),
        };
        int failed=0;foreach(var (name,run) in tests){try {run();Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
        Console.WriteLine($"{tests.Length-failed}/{tests.Length} integration tests passed");return failed==0?0:1;
    }
    static (IdleSignalPlugin, HostProxy) Create()
    {
        var control=DispatchProxy.Create<ISharpControl,HostProxy>();var host=(HostProxy)(object)control;var p=new IdleSignalPlugin();p.Initialize(control);host.Hooks.SetProcessorSampleRate(ProcessorType.FilteredAudioOutput,16000);return(p,host);
    }
    static void WithPlugin(Action<IdleSignalPlugin,HostProxy,CheckBox> action){var(p,h)=Create();try{action(p,h,Find<CheckBox>(p.Gui,"enableCheckBox"));}finally{p.Close();}}
    static T Find<T>(Control c,string name) where T:Control => c.Controls.Find(name,true).OfType<T>().Single();
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static float[] Stereo(double f,int n=16000,int fs=16000) => Enumerable.Range(0,n).SelectMany(i=>{var v=(float)(.2*Math.Sin(2*Math.PI*f*i/fs));return new[]{v,v};}).ToArray();
}

public class HostProxy : DispatchProxy
{
    public HookManager Hooks {get;} = new();
    public bool Playing=true;
    public DetectorType Mode=DetectorType.NFM;
    private PropertyChangedEventHandler? _changed;
    public void Raise(string name)=>_changed?.Invoke(this,new PropertyChangedEventArgs(name));
    public unsafe void Process(float[] x){fixed(float* p=x)Hooks.ProcessFilteredAudioOutput(p,x.Length);}
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        switch(method!.Name)
        {
            case "get_IsPlaying": return Playing;
            case "get_DetectorType": return Mode;
            case "get_ThemeIsDark": return false;
            case "get_ThemeForeColor": return System.Drawing.Color.Black;
            case "get_ThemeBackColor": case "get_ThemePanelColor": return System.Drawing.Color.White;
            case "RegisterStreamHook": Hooks.RegisterStreamHook(args![0],(ProcessorType)args[1]!);return null;
            case "UnregisterStreamHook": Hooks.UnregisterStreamHook(args![0]);return null;
            case "add_PropertyChanged": _changed+=(PropertyChangedEventHandler)args![0]!;return null;
            case "remove_PropertyChanged": _changed-=(PropertyChangedEventHandler)args![0]!;return null;
            default: throw new NotSupportedException(method.Name);
        }
    }
}
