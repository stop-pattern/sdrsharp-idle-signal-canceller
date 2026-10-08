using IdleSignal.Core;

var tests = new List<(string Name, Action Run)>
{
    ("persistent 1500 Hz is idle", () => Check(Detect(1500).IsIdle, "tone not detected")),
    ("lower tolerance endpoint", () => Check(Detect(1450).IsIdle, "1450 not detected")),
    ("upper tolerance endpoint", () => Check(Detect(1550).IsIdle, "1550 not detected")),
    ("1000 Hz is not idle", () => Check(!Detect(1000).IsIdle, "outside tone muted")),
    ("2000 Hz is not idle", () => Check(!Detect(2000).IsIdle, "outside tone muted")),
    ("silence is not idle", () => Check(!Detect(0, amplitude: 0).IsIdle, "silence muted")),
    ("white noise is not idle", () => Check(!Detect(0, amplitude: 0, noise: .2).IsIdle, "noise muted")),
    ("noisy 1500 Hz is idle", () => Check(Detect(1500, amplitude: .03, noise: .2).IsIdle, "weak tone missed")),
    ("short tone is not idle", () => Check(!Detect(1500, seconds: .08).IsIdle, "short event muted")),
    ("speech-like mixture is not idle", () =>
    {
        var d = new ToneDetector(16000, new DetectorSettings());
        for (var i = 0; i < 16000; i++) d.Push((float)(.08*Math.Sin(2*Math.PI*250*i/16000) + .06*Math.Sin(2*Math.PI*700*i/16000) + .04*Math.Sin(2*Math.PI*2300*i/16000)));
        Check(!d.IsIdle, "speech-like mixture muted");
    }),
    ("tone end releases", () => { var d = Detect(1500); for (var i=0; i<1600; i++) d.Push(0); Check(!d.IsIdle, "stale mute"); }),
    ("reset clears idle", () => { var d=Detect(1500); d.Reset(); Check(!d.IsIdle, "reset retains mute"); }),
};
tests.AddRange(GateTests.All());
var failures = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Count-failures}/{tests.Count} passed");
return failures == 0 ? 0 : 1;

static ToneDetector Detect(double frequency, double amplitude = .2, double noise = 0, double seconds = 1)
{
    var d = new ToneDetector(16000, new DetectorSettings());
    var random = new Random(17);
    for (var i=0; i<(int)(16000*seconds); i++) d.Push((float)(amplitude*Math.Sin(2*Math.PI*frequency*i/16000) + noise*(random.NextDouble()*2-1)));
    return d;
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
