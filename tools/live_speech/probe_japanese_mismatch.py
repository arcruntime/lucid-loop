"""Two scratch-only Japanese model corrections and exact C#/Node parity verification.

python tools/live_speech/probe_japanese_mismatch.py --source .local/lipsync-audit
Optional --output writes the compact measured result; otherwise output stays in .local.
Requires dotnet 8 and the pinned upstream checkout with its existing fixtures/model.
No retraining, downloads, Unity edits, or changes to source-checkout files.
"""
from pathlib import Path
import argparse, hashlib, json, subprocess

PIN="acea62e125aa2200648a489900de750c3e3587fa"
MODEL_SHA="e085a7c11f43c88dee7b139704e63369631ba0f61193b5fb640128bc231634ca"
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument("--source",required=True,type=Path)
parser.add_argument("--output",type=Path)
args=parser.parse_args()
source=args.source.resolve()
actual_pin=subprocess.check_output(["git","-C",str(source),"rev-parse","HEAD"],text=True).strip()
if actual_pin!=PIN: raise SystemExit("Unexpected upstream revision: "+actual_pin)
inputs=["src/SplatterfaceGames.LipSync.Core","src/SplatterfaceGames.LipSync.Cli/WavFile.cs",
        "ja/model-ja-mixed.bin","fixtures/manifest-ja.json","fixtures/ja","eval-output/ja"]
if subprocess.run(["git","-C",str(source),"diff","--quiet",PIN,"--",*inputs]).returncode:
    raise SystemExit("Upstream inputs differ from pinned committed content")
manifest=json.loads((source/"fixtures/manifest-ja.json").read_text(encoding="utf-8-sig"))
for clip in manifest:
    if hashlib.sha256((source/"fixtures"/clip["file"]).read_bytes()).hexdigest()!=clip["sha256"]:
        raise SystemExit("Fixture hash mismatch: "+clip["id"])
if hashlib.sha256((source/"ja/model-ja-mixed.bin").read_bytes()).hexdigest()!=MODEL_SHA:
    raise SystemExit("Japanese model hash mismatch")
dest=root/".local/japanese-producer-followup"
dest.mkdir(parents=True,exist_ok=True)
for p in (source/'src/SplatterfaceGames.LipSync.Core').glob('*.cs'):
    text=p.read_text(encoding='utf-8-sig')
    if p.name=='GaussianClassifier.cs':
        text=text.replace('new RingBuffer<int>(6,','new RingBuffer<int>(int.Parse(Environment.GetEnvironmentVariable("JA_VOTE") ?? "6"),')
        text=text.replace('i < 6;', 'i < _ringPredictions.Capacity;')
        text=text.replace('private double[] _distances', 'private double[] _volumePenalty = Array.Empty<double>();\n        private double[] _distances')
        text=text.replace('_distances = new double[_prototypesN];', '''_distances = new double[_prototypesN];
            _volumePenalty = new double[_prototypesN];
            if(Environment.GetEnvironmentVariable("JA_CORRECTION") == "gaussian")
                for(int k=0;k<_prototypesN;k++) _volumePenalty[k]=LogCovarianceDeterminant(_prototypes[k].SigmaInvLower);''')
        text=text.replace('distances[i] = d;', 'd += _volumePenalty[i];\n                distances[i] = d;')
        anchor='        /// <summary>\n        /// Predict the viseme'
        method='''        // Model-only covariance-volume correction; no evaluation labels used.
        static double LogCovarianceDeterminant(float[] precision) {
            int n=Parameters.MfccCoeffNWithDeltas;
            var lower=new double[n,n]; double logDetPrecision=0;
            for(int i=0;i<n;i++) for(int j=0;j<=i;j++) {
                double sum=precision[SigmaIndex(i,j)];
                for(int k=0;k<j;k++) sum-=lower[i,k]*lower[j,k];
                if(i==j) {
                    if(sum<=0 || double.IsNaN(sum)) throw new InvalidOperationException("Non-positive precision matrix");
                    lower[i,j]=Math.Sqrt(sum); logDetPrecision+=Math.Log(sum);
                } else lower[i,j]=sum/lower[j,j];
            }
            return -logDetPrecision;
        }

'''
        assert anchor in text
        text=text.replace(anchor,method+anchor)
    if p.name=='VisemeAnalyzer.cs':
        text=text.replace('if (inactive != 0)', 'if (inactive != 0 && Environment.GetEnvironmentVariable("JA_CORRECTION") != "no-vad")')
    (dest/p.name).write_text(text,encoding='utf-8')
program=r'''
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using SplatterfaceGames.LipSync;
using SplatterfaceGames.LipSync.Cli;

string root = args[0];
var model = GaussianModel.Load(Path.Combine(root, "ja/model-ja-mixed.bin"));
var variants = new[] {"baseline6", "baseline1", "no-vad", "gaussian"};
var results = new List<object>();
foreach (string variant in variants)
{
    int vote=variant=="baseline6"?6:1;
    Environment.SetEnvironmentVariable("JA_VOTE", vote.ToString());
    Environment.SetEnvironmentVariable("JA_CORRECTION",variant);
    int parityN=0, parityMatches=0;
    var expected = new Dictionary<string, int>();
    var matched = new Dictionary<string, int>();
    int framesN = 0, matches = 0, nonSilN = 0, nonSilMatches = 0, clips = 0, changes = 0;
    double seconds = 0, processing = 0;
    foreach (string wav in Directory.GetFiles(Path.Combine(root, "fixtures/ja"), "*.wav").OrderBy(p => p))
    {
        string id = Path.GetFileNameWithoutExtension(wav);
        // Spliced code-switch needs language-segment routing, not a Japanese model over its English half.
        if (id == "ja-en-codeswitch") continue;
        using var alignment = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "eval-output/ja", id + ".alignment.json")));
        var aligned = alignment.RootElement.GetProperty("frames").EnumerateArray().ToArray();
        using var reference=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"eval-output/ja",id+".acoustic.json")));
        var referenceFrames=reference.RootElement.GetProperty("frames").EnumerateArray().ToDictionary(f=>f.GetProperty("tMs").GetDouble(),f=>f.GetProperty("label").GetString());
        var (rate, channels, pcm) = WavFile.Read(wav);
        seconds += (double)pcm.Length / rate / channels; clips++;
        using var analyzer = new VisemeAnalyzer(model, rate, channels);
        var watch = Stopwatch.StartNew();
        var frames = new List<VisemeFrame>();
        for (int offset = 0; offset < pcm.Length; offset += 960)
            frames.AddRange(analyzer.ProcessChunk(new ReadOnlySpan<float>(pcm, offset, Math.Min(960, pcm.Length - offset))));
        processing += watch.Elapsed.TotalSeconds;
        int index = 0; string previous = "sil";
        foreach (var frame in frames)
        {
            while (index + 1 < aligned.Length && aligned[index + 1].GetProperty("tMs").GetDouble() <= frame.TimeMs) index++;
            if (aligned[index].GetProperty("tMs").GetDouble() > frame.TimeMs) continue;
            string target = aligned[index].GetProperty("label").GetString()!;
            string label = frame.Label switch { "U" => "ja_U", "FF" => "ja_FU", "RR" => "ja_R", _ => frame.Label };
            if(vote==6 && referenceFrames.TryGetValue(frame.TimeMs,out var referenceLabel)) { parityN++; if(label==referenceLabel) parityMatches++; }
            expected[target] = expected.GetValueOrDefault(target) + 1;
            if (label == target) { matched[target] = matched.GetValueOrDefault(target) + 1; matches++; }
            framesN++;
            if (target != "sil") { nonSilN++; if (target == label) nonSilMatches++; }
            if (label != previous) changes++;
            previous = label;
        }
    }
    results.Add(new { variant, vote, clips, parityN, parityMatches, audioSeconds = seconds, analysisSeconds = processing,
        desktopRtf = processing / seconds, frames = framesN, agreement = (double)matches / framesN,
        nonSilAgreement = (double)nonSilMatches / nonSilN, transitionsPerSecond = changes / seconds,
        recall = expected.ToDictionary(e => e.Key, e => new { expectedFrames = e.Value,
            matchedFrames = matched.GetValueOrDefault(e.Key), value = (double)matched.GetValueOrDefault(e.Key) / e.Value }) });
}
Console.WriteLine(JsonSerializer.Serialize(new { pin = "acea62e125aa2200648a489900de750c3e3587fa",
    warning = "Exploratory same-corpus machine-alignment comparison; not independent accuracy or iOS timing.", results },
    new JsonSerializerOptions { WriteIndented = true }));
'''

(dest/'Program.cs').write_text(program,encoding='utf-8')
(dest/'WavFile.cs').write_bytes((source/'src/SplatterfaceGames.LipSync.Cli/WavFile.cs').read_bytes())
(dest/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
    '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable>'
    '</PropertyGroup></Project>',encoding='utf-8')
subprocess.run(['dotnet','build',str(dest/'Probe.csproj'),'--nologo','-v','quiet'],check=True)
raw=subprocess.check_output(['dotnet',str(dest/'bin/Debug/net8.0/Probe.dll'),str(source)])
result=json.loads(raw)
result['provenance']={
    'upstreamRepository':'https://github.com/splatterfacegames/unity-realtime-lipsync',
    'commit':PIN,'modelSHA256':MODEL_SHA,
    'fixtureManifest':'https://github.com/splatterfacegames/unity-realtime-lipsync/blob/'+PIN+'/fixtures/manifest-ja.json',
    'fixtureHashVerification':{'checked':len(manifest),'mismatches':[]},
    'evaluatedClips':39,'excludedClip':'ja-en-codeswitch',
    'reference':'upstream eval-output/ja/<id>.alignment.json; machine-derived timing, same collection as model training',
    'parityReference':'upstream eval-output/ja/<id>.acoustic.json at identical timestamps',
    'reproduction':'python tools/live_speech/probe_japanese_mismatch.py --source <pinned-checkout>',
    'runtimeChanges':False,'retraining':False}
assert result['results'][0]['parityMatches']==result['results'][0]['parityN']==10628
output=args.output or dest/'results.json'
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for r in result['results']:
    print(json.dumps({k:r[k] for k in ['variant','agreement','nonSilAgreement','parityN','parityMatches']}))
    print('silenceRecall:',r['recall']['sil']['value'])
print('Result:',output)
