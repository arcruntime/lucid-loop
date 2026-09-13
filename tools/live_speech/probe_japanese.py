"""Scratch-only Japanese vote-window experiment; never changes Unity or its pinned source.

python tools/live_speech/probe_japanese.py --source .local/lipsync-audit
Requires the audited upstream checkout (including its attributed Japanese fixtures/model).
Scores against upstream machine alignment, NOT human contact ground truth or held-out data.
"""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
PIN = "acea62e125aa2200648a489900de750c3e3587fa"
parser = argparse.ArgumentParser()
parser.add_argument("--source", required=True, type=Path)
args = parser.parse_args()
source = args.source.resolve()
if subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip() != PIN:
    raise SystemExit("Wrong upstream revision")
scratch = ROOT / ".local/japanese-producer-probe"
scratch.mkdir(parents=True, exist_ok=True)
core = source / "src/SplatterfaceGames.LipSync.Core"
for path in core.glob("*.cs"):
    text = path.read_text(encoding="utf-8-sig")
    if path.name == "GaussianClassifier.cs":
        text = text.replace("new RingBuffer<int>(6,", "new RingBuffer<int>(int.Parse(Environment.GetEnvironmentVariable(\"JA_VOTE\") ?? \"6\"),")
        text = text.replace("i < 6;", "i < _ringPredictions.Capacity;")
    (scratch / path.name).write_text(text, encoding="utf-8")
(scratch / "WavFile.cs").write_bytes((source / "src/SplatterfaceGames.LipSync.Cli/WavFile.cs").read_bytes())
(scratch / "Probe.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable>
</PropertyGroup></Project>''', encoding="utf-8")
(scratch / "Program.cs").write_text(r'''
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
var votes = new[] {6, 3, 2, 1};
var results = new List<object>();
foreach (int vote in votes)
{
    Environment.SetEnvironmentVariable("JA_VOTE", vote.ToString());
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
            expected[target] = expected.GetValueOrDefault(target) + 1;
            if (label == target) { matched[target] = matched.GetValueOrDefault(target) + 1; matches++; }
            framesN++;
            if (target != "sil") { nonSilN++; if (target == label) nonSilMatches++; }
            if (label != previous) changes++;
            previous = label;
        }
    }
    results.Add(new { vote, clips, audioSeconds = seconds, analysisSeconds = processing,
        desktopRtf = processing / seconds, frames = framesN, agreement = (double)matches / framesN,
        nonSilAgreement = (double)nonSilMatches / nonSilN, transitionsPerSecond = changes / seconds,
        recall = expected.ToDictionary(e => e.Key, e => new { expectedFrames = e.Value,
            matchedFrames = matched.GetValueOrDefault(e.Key), value = (double)matched.GetValueOrDefault(e.Key) / e.Value }) });
}
Console.WriteLine(JsonSerializer.Serialize(new { pin = "acea62e125aa2200648a489900de750c3e3587fa",
    warning = "Exploratory same-corpus machine-alignment comparison; not independent accuracy or iOS timing.", results },
    new JsonSerializerOptions { WriteIndented = true }));
''', encoding="utf-8")
subprocess.run(["dotnet", "build", str(scratch / "Probe.csproj"), "--nologo", "-v", "quiet"], check=True)
raw = subprocess.check_output(["dotnet", str(scratch / "bin/Debug/net8.0/Probe.dll"), str(source)])
result = json.loads(raw)
(scratch / "results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
for item in result["results"]:
    print(json.dumps({k: item[k] for k in ("vote", "clips", "agreement", "nonSilAgreement", "transitionsPerSecond", "desktopRtf")}))
    print(json.dumps({k: item["recall"][k]["value"] for k in ("ja_U", "ja_FU", "ja_R", "PP")}))
print("Full result:", scratch / "results.json")
