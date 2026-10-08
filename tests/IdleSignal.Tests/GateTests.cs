using IdleSignal.Core;

static class GateTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("stereo 1500 Hz mutes both channels at per-channel rate", () =>
        {
            var mono=Tone(1500,16000);var stereo=mono.SelectMany(v=>new[]{v,v}).ToArray();
            var gate=NewGate();gate.Process(stereo,2);
            Check(stereo.AsSpan(16000).ToArray().All(v=>v==0),"stereo tone leaks");Check(gate.Snapshot.IsIdle,"stereo frequency interpreted incorrectly");
        });
        yield return ("stereo release preserves both channels", () =>
        {
            var gate=NewGate();gate.Process(Tone(1500,16000).SelectMany(v=>new[]{v,v}).ToArray(),2);
            var x=Tone(700,3200).SelectMany(v=>new[]{v,v*.5f}).ToArray();var original=x.ToArray();gate.Process(x,2);
            Check(x.AsSpan(4000).SequenceEqual(original.AsSpan(4000)),"stereo release alters audio");
        });
        yield return ("gate mutes stable tone to exact zero", () =>
        {
            var gate = NewGate(); var x = Tone(1500, 16000); gate.Process(x);
            Check(x.AsSpan(8000).ToArray().All(v => v == 0), "tone leaks after detection");
            Check(gate.Snapshot.IsIdle, "idle display missing");
        });
        yield return ("disable passes next buffer unchanged", () =>
        {
            var gate = NewGate(); gate.Process(Tone(1500, 16000)); gate.SetEnabled(false);
            var x = Tone(1500, 800); var original = x.ToArray(); gate.Process(x);
            Check(x.SequenceEqual(original), "disabled changes samples"); Check(!gate.Snapshot.IsIdle, "disabled display stale");
        });
        yield return ("startup disabled preserves audio", () =>
        {
            var gate = new AudioGate(16000, new DetectorSettings());
            var x = Tone(1500, 16000); var original = x.ToArray(); gate.Process(x);
            Check(x.SequenceEqual(original), "startup mutes audio");
        });
        yield return ("tone to speech releases gate", () =>
        {
            var gate = NewGate(); gate.Process(Tone(1500, 16000));
            var x = Tone(700, 3200); var original = x.ToArray(); gate.Process(x);
            Check(x.AsSpan(2000).SequenceEqual(original.AsSpan(2000)), "speech not restored"); Check(!gate.Snapshot.IsIdle, "stale idle");
        });
        yield return ("disable re-enable clears acquisition history", () =>
        {
            var gate=NewGate();gate.Process(Tone(1500,16000));gate.SetEnabled(false);gate.SetEnabled(true);
            var x=Tone(1500,640);var original=x.ToArray();gate.Process(x);
            Check(x.SequenceEqual(original), "stale mute after re-enable");
        });
        yield return ("reset clears muted state", () =>
        {
            var gate=NewGate();gate.Process(Tone(1500,16000));gate.Reset();
            var x=Tone(1500,640);var original=x.ToArray();gate.Process(x);Check(x.SequenceEqual(original), "reset does not open gate");
        });
        yield return ("chunk lengths do not change audio", () =>
        {
            var x=Tone(1500,16000).Concat(Tone(700,8000)).Concat(Tone(1500,8000)).ToArray();var y=x.ToArray();
            NewGate().Process(x);var gate=NewGate();int at=0;
            foreach(var length in new[]{1,17,639,5000,3,411,8000,10000,7929}) { if(at>=y.Length) break; var n=Math.Min(length,y.Length-at);gate.Process(y.AsSpan(at,n));at+=n; }
            if(at<y.Length)gate.Process(y.AsSpan(at));Check(x.SequenceEqual(y), "chunk-dependent gate");
        });
        yield return ("nonfinite input cannot latch mute", () =>
        {
            var gate=NewGate();gate.Process(Tone(1500,16000));var x=Tone(700,1600);x[0]=float.NaN;gate.Process(x);
            Check(x.All(float.IsFinite), "nonfinite output");Check(!gate.Snapshot.IsIdle,"nonfinite latches mute");
        });
        yield return ("steady audio processing does not allocate", () =>
        {
            var gate=NewGate();var x=Tone(700,1600);for(int i=0;i<10;i++)gate.Process(x);
            var before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)gate.Process(x);
            Check(GC.GetAllocatedBytesForCurrentThread()==before,"audio loop allocates");
        });
    }
    private static AudioGate NewGate() { var g=new AudioGate(16000,new DetectorSettings());g.SetEnabled(true);return g; }
    internal static float[] Tone(double f,int n,int fs=16000) => Enumerable.Range(0,n).Select(i=>(float)(.2*Math.Sin(2*Math.PI*f*i/fs))).ToArray();
    private static void Check(bool ok,string reason) {if(!ok)throw new Exception(reason);}
}
