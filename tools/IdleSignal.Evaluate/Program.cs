using System.Text;
using System.Text.Json;
using IdleSignal.Core;

if (args.Length < 1) { Console.Error.WriteLine("Usage: evaluate INPUT.wav [REPORT.json] [thresholdDb] [acquireMs] [releaseMs]"); return 2; }
using var file = File.OpenRead(args[0]); using var reader = new BinaryReader(file);
if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("Not a RIFF WAV");
reader.ReadUInt32(); if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("Not a WAVE file");
int format=0, channels=0, fs=0, bits=0; byte[]? data=null;
while (file.Position + 8 <= file.Length)
{
    var id=Encoding.ASCII.GetString(reader.ReadBytes(4));var size=reader.ReadUInt32();var next=file.Position+size+(size%2);
    if (next>file.Length) throw new InvalidDataException("Truncated WAV chunk");
    if (id=="fmt ") { format=reader.ReadUInt16();channels=reader.ReadUInt16();fs=reader.ReadInt32();reader.ReadUInt32();reader.ReadUInt16();bits=reader.ReadUInt16(); if(format==65534 && size>=40) {reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt32();format=reader.ReadUInt16();} }
    if (id=="data") data=reader.ReadBytes(checked((int)size));
    file.Position=next;
}
if (data is null || channels is not (1 or 2) || !((format==1 && bits==16) || (format==3 && bits==32))) throw new InvalidDataException("Expected mono/stereo PCM16 or float32 WAV");
var samples=new float[data.Length/(bits/8)];
for(int i=0;i<samples.Length;i++) samples[i]=format==1 ? BitConverter.ToInt16(data,i*2)/32768f : BitConverter.ToSingle(data,i*4);
var settings=new DetectorSettings { ThresholdDb=args.Length>2 ? double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture) : 7, AcquireMs=args.Length>3 ? int.Parse(args[3]) : 160, ReleaseMs=args.Length>4 ? int.Parse(args[4]) : 80 };
var gate=new AudioGate(fs,settings);gate.SetEnabled(true);
int block=(int)Math.Round(fs*.04)*channels, idle=0, zero=0, transitions=0, frames=0, idleRun=0, stableMutedFrames=0;bool previous=false;
double stableMutedPeak=0;
var strengths=new List<double>();var intervals=new List<double[]>(); double start=0;
for(int at=0;at<samples.Length;at+=block)
{
    var span=samples.AsSpan(at,Math.Min(block,samples.Length-at));gate.Process(span,channels);var state=gate.Snapshot;
    frames++;strengths.Add(state.ProminenceDb);if(state.IsIdle)idle++;
    if (span.IndexOfAnyExcept(0f)<0)zero++;
    idleRun=state.IsIdle?idleRun+1:0;
    if(idleRun>=3){stableMutedFrames++;foreach(var value in span)stableMutedPeak=Math.Max(stableMutedPeak,Math.Abs(value));}
    if(state.IsIdle!=previous){transitions++; if(state.IsIdle)start=(at+span.Length)/(double)(fs*channels);else intervals.Add([start,(at+span.Length)/(double)(fs*channels)]);previous=state.IsIdle;}
}
if(previous)intervals.Add([start,samples.Length/(double)(fs*channels)]);strengths.Sort();
var report=new { sampleRate=fs, channels, durationSeconds=samples.Length/(double)(fs*channels), settings, frames, idleFrames=idle, exactlyZeroFrames=zero, idleFraction=idle/(double)frames, transitions, prominenceMedianDb=strengths[strengths.Count/2], stableMutedFrames, stableMutedPeak, idleIntervalsSeconds=intervals };
var json=JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true});Console.WriteLine(json);
if(args.Length>1)File.WriteAllText(args[1],json+Environment.NewLine);
return stableMutedPeak==0 ? 0 : 1;
