# English/Japanese live lip-sync integration readiness

The initial sections record the read-only audit at commit **`acea62e125aa2200648a489900de750c3e3587fa`**. Later addenda record the artist API and installed English core adapter; they supersede the corresponding initial readiness gaps. No audible or rendered acceptance is claimed. The artist still owns visual acceptance of Ren and the shared facial contract.

## Dependency and installation boundary

The source is [unity-realtime-lipsync at the audited revision](https://github.com/splatterfacegames/unity-realtime-lipsync/tree/acea62e125aa2200648a489900de750c3e3587fa). Its `unity-package/package.json` declares `com.splatterfacegames.lipsync` version `0.1.0`, Unity `6000.3.24f1`, and no package dependencies. The eventual pinned UPM URL is:

```text
https://github.com/splatterfacegames/unity-realtime-lipsync.git?path=/unity-package#acea62e125aa2200648a489900de750c3e3587fa
```

That URL alone is not a verified acoustic installation. The analyzer core lives outside the UPM directory in `src/SplatterfaceGames.LipSync.Core`, targeting `netstandard2.1`. Upstream's testbed includes `Assets/Plugins/SplatterfaceGames.LipSync.Core.dll`; the core is not supplied by a dependency entry. The runtime asmdef explicitly references `SplatterfaceGames.LipSync.Core`, while `AnalyzerVisemeSource` is guarded by `SPLATTERFACEGAMES_LIPSYNC_CORE`. Its version define names the core assembly; do not assume that resolves automatically for a copied DLL. Inspect and validate assembly/define wiring in an isolated installation before touching the shared project. Prefer a reproducible source-built DLL or a properly packaged source assembly, pinned to the same revision. Preserve notices and record hashes of the actual binary/model shipped. [Manifest](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/unity-package/package.json), [runtime asmdef](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/unity-package/Runtime/SplatterfaceGames.LipSync.asmdef).

The English Gaussian model must also be deployed to a location readable by `GaussianModel.Load(Stream)` or `Load(path)`. The upstream English model SHA-256 is `0358f68989b5861f9b7d18871b010fa6cbf88a53bda4954a954d8c548bbcf251`. StreamingAssets/mobile loading must be implemented deliberately; an editor filesystem path is not a device integration. The Japanese alignment scripts and Japanese model are outside the UPM package too. uLipSync is an upstream comparison baseline, not a requirement for this game's adapter. Upstream lists MIT code/model components and separate CC BY-SA Japanese corpus/model material; copy the relevant notices for what is actually imported. [Provenance and notices](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/THIRD_PARTY_NOTICES.md).

## Current game seam

`LiveGym.Handle` receives base64 `session.output_audio.delta` and writes decoded PCM into a three-second `AudioRingBuffer`. The relay documents mono signed PCM16 little-endian, 24 kHz. The ring currently exposes queue count, not a monotonic consumed-sample clock. Transcript deltas become truncated display strings. Keep the microphone capture path separate from the output speech analysis path. [LiveGym](../Unity/Assets/Gyms/Runtime/Live/LiveGym.cs), [ring buffer](../Unity/Assets/Gyms/Runtime/AudioRingBuffer.cs), [relay contract](../server/README.md).

The integration needs one owner of output playback and its clock. Recommended first pilot: replace output-ring consumption with the package timeline/player in an isolated Ren conversation scene, while leaving microphone input and the relay protocol intact. Analyze exactly the decoded PCM accepted for playback, preserving sample rate, channel count, sequence, and generation. Do not play a second copy of the audio merely to drive the face. Do not use amplitude, network receipt time, or transcript arrival time as the articulation clock.

### Actual package APIs

| API | Use and responsibility |
| --- | --- |
| `LipSyncSpeaker.BeginUtterance()` | Creates `SpeechStream` with speaker/generation; invalidates existing animator targets. Does not reset playback counters or clear old PCM. |
| `PushPcm16(bytes, rate, channels)` / `PushFloats(samples, rate, channels)` | Enqueues audio. Has no incoming generation parameter: host must reject stale packets before calling. |
| `AnalyzerVisemeSource(model, inputRate, inputChannels)` | English acoustic producer; `Begin(stream)` resets analyzer-local time; `PushAudio(floats)` analyzes; `Pump(queue, cursorMs)` enqueues produced frames. The supplied cursor is ignored. |
| `VisemeTargetQueue.Enqueue(...)` | Accepts timestamped generation-tagged targets; canonical 19-label space extends the 15-label acoustic core. |
| `ScheduledVoicePlayer.PlaybackSamplePos` / `AudibleMs` | Consumed real samples, converted to milliseconds minus an estimated device latency. Defaults: 48 kHz timeline, four-second capacity, 0.5-second streaming clip, 40 ms latency estimate. |
| `queue.SampleAt(audibleMs, scratch19)` | Interpolated target weights. Reuse a scratch array to avoid per-frame allocation. |
| `EndUtterance()` | Marks end of PCM; transport must establish a real end boundary. |
| `Interrupt()` / `Disconnect()` | Invalidate/drop current state, but host still rejects late packets and explicitly clears character speech. |

These facts come from implementation, not just `CONTRACTS.md`: [speaker](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/unity-package/Runtime/Unity/LipSyncSpeaker.cs), [analyzer adapter](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/unity-package/Runtime/Integration/AnalyzerVisemeSource.cs), [player](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/unity-package/Runtime/Unity/ScheduledVoicePlayer.cs).

### Integration gaps to resolve before a live pilot

1. **Clock origin across utterances.** Analyzer timestamps restart at zero on `Begin`, while playback counters survive `BeginUtterance` and `Interrupt`. Offset utterance-local targets by the corresponding absolute audio sample origin, or deliberately reset the complete stopped pipeline at a safe boundary. Calling the facade methods alone does not solve this. Exercise at least two consecutive utterances and barge-in.
2. **Late packet identity.** Carry local session/speaker/generation identity through decode, analysis, and queued delivery. `Invalidate` removes targets already present; the queue and speaker do not automatically reject every future stale enqueue. Reconnection, speaker switching, loop reset, and interruption must close the previous producer as well.
3. **Starvation and overflow.** The consumed clock pauses on underrun. A target at that paused timestamp can remain held; the queue's timestamp-based tail limit does not itself measure elapsed starvation time. Explicitly fade speech to rest after confirmed starvation. Queue overflow drops PCM, so continuing analyzer timestamps over all received audio would desynchronize subsequent articulation. Define a reset/drop policy and log the event.
4. **Playback latency and drain.** Consumed samples are handed to Unity audio buffering, not measured at the listener's ear. Calibrate the estimate on target hardware, avoid subtracting latency twice through both player and queue, and test that end-of-stream does not cut audio still buffered by the device. Neither physical latency nor audible completion is proved by the pure managed tests.
5. **Bounded work and lifetime.** The PCM ring is bounded, but `VisemeTargetQueue` uses a growing list and analyzer pending frames allocate. Bound/prune long sessions and give each analyzer an explicit disposal owner. The provided `AnalyzerVisemeSource` does not expose `IDisposable` even though its core analyzer does. Core `Flush()` returns no extra frames; handle final neutral pose explicitly.
6. **Explicit references.** Bind the selected speaker/player/face directly. The facade has scene-wide `FindFirstObjectByType` fallbacks that are unsuitable for four separate NPC identities.

## Character adapter contract for artist agreement

Keep `CharacterFaceDriver` as the sole writer of the authored face, expression, blink, and post-animation head/eye layers. The package's `RigAdapter` and `LipSyncFaceAnimator` otherwise introduce another facial compositor and optional blink/gaze/head animation. Its `ISemanticFace.Apply(in SemanticControls)` interface is not implemented by the current character driver, and semantic controls cannot simply be cast back into authored visemes. [CharacterFaceDriver](../Unity/Assets/CharacterArt/Runtime/CharacterFaceDriver.cs), [face composition](../Unity/Assets/CharacterArt/Runtime/FacePoseComposer.cs).

The smallest compatible engineering seam is a game adapter that samples the package queue using audible time, applies contact-aware transitions, then submits a complete speech-only frame to the artist-owned driver. The original proposed seam was:

```text
ApplySpeechFrame(language, [(authoredPoseId, normalizedWeight), ...])
ResetSpeech()
diagnostics: unknown language/pose, missing target, rejected stale generation
```

The driver currently offers `SetVisemes(IReadOnlyList<VisemeWeight>)` for the original 15 labels and `SetSpeechPose(SpeechLanguage, poseId, weight)` for one bilingual profile pose. Each call replaces the speech snapshot; calling the latter repeatedly will not build a blended frame. Use the implemented `ApplySpeechFrame(IReadOnlyList<SpeechPoseWeight>)` described below for a validated batched snapshot; the legacy methods retain their prior behavior. Do not silently feed `ja_U`, `ja_FU`, `ja_R`, or `en_L` to the legacy method: it filters unsupported labels. Existing `StartSpeechReviewSequence` is a timed review fixture, not an audio-clock streaming adapter. [Bilingual profiles](../Unity/Assets/CharacterArt/Runtime/BilingualSpeechProfiles.cs).

| Producer label | Character profile pose ID | Essential distinction |
| --- | --- | --- |
| `PP` | `pbm` (both languages) | Full seal under all expressions. |
| `FF` | English `fv` | Lower lip to upper teeth. |
| `RR` | English `r` | Held rhotic. |
| `U` | English `u` | Rounded/protruded vowel. |
| `ja_U` | Japanese `u` | Compressed Japanese vowel. |
| `ja_FU` | Japanese `fu` | Bilabial frication, distinct from English `fv`. |
| `ja_R` | Japanese `r_tap` | Brief tap, distinct from English `r`. |
| `en_L` | English `l` | Distinct lateral contact. |

Complete the remaining label mapping explicitly against the selected language profile. Some rich authored poses cannot be distinguished by the 19-label producer vocabulary alone; do not claim automatic SH/TSU/contextual-nasal selection merely because profile entries exist. Model-specific Japanese acoustic slot remapping must occur before canonical queue insertion: the stock analyzer adapter preserves English label names. In the core's canonical output order, remap named `U`, `FF`, and `RR` weights into `ja_U`, `ja_FU`, and `ja_R`; internal model slot numbers documented upstream use a different order.

Artist readiness should include the real mesh/prefab/profile identity, all required target bindings, contact tests under strong emotions, silence/rest behavior, and a statement that the face is ready for audible testing. This preserves the two shared skeletons and does not require adding a jaw bone driver. Morph/contact ownership stays with CharacterArt.

## Japanese: usable assets versus live implementation

Upstream recommends its **alignment route when matching text is known**. It is a Node fixture tool (`ja/run-alignment.mjs`) consuming complete WAV audio plus kana text, running energy/DP mora alignment, and emitting timeline JSON. It is not a bundled Unity streaming aligner, generic kanji-to-reading service, or Live transcript adapter. The acoustic Japanese model is available, but upstream reports weak brief-contact recognition and explicitly leaves live transport and hardware acceptance open. Its reference parity measures port agreement, not whether a human sees correct Japanese articulation. [Japanese route and limitations](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/docs/ja-analyzer-route.md), [acceptance report](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/docs/acceptance-report.md).

The game's Live contract has timestamped transcript fragments, not phoneme/mora alignments or an authoritative transcript-turn-completed event. Fragment bounds alone do not give internal contact timing. They also do not guarantee kana readings, stable phrase boundaries, or that the corresponding audio has not already played. Preserve text and timestamps and resolve the session/audio clock relationship. Any transcript-assisted path needs a bounded audio/text buffer, reading conversion, alignment, correction policy, and measured added latency. Do not retrospectively animate a fragment after its audio was heard. See [Live gameplay protocol](LIVE_GAMEPLAY_PROTOCOL.md).

Use prealigned English/Japanese fixtures to prove the rig and playback adapter first. Separately evaluate live English acoustic output. For live Japanese, select and measure a transcript-assisted streaming path or improve the Japanese acoustic producer; the existing weak acoustic route may be used as an explicitly labeled development fallback, not evidence that the bilingual goal is complete. Mixed-language speech needs explicit segment routing; changing a pose-set dropdown cannot infer language or produce the missing `en_L` signal.

## Verification plan and acceptance evidence

| Stage | Evidence required |
| --- | --- |
| Isolated dependency install | Core build, explicit assembly/define resolution, model loading, correct Unity version, no unresolved package references. |
| Pure managed pipeline | Irregular PCM chunking, resampling, two utterance origins, stale delivery after reset, bounded overflow/underrun handling, explicit neutral at end. Upstream core tests can run with `dotnet test src/SplatterfaceGames.LipSync.sln` in the isolated clone; package EditMode test sources cover queue and compositor behavior. |
| Authored face with recorded audio | Approved Ren prefab; English seal/FV/L/R and Japanese U/FU/tap contrasts, geminates, long vowels, devoicing, contextual nasals, and a code-switch example. Capture audible playback and visible contact with measured offsets. |
| Live session | Both languages, multiple utterances, silence, barge-in, disconnect/reconnect, character switch, and loop reset; demonstrate no duplicate audio or stale face motion. Keep transcript inference separate from confirmed world facts. |
| Target device | iPhone playback alignment, 30 fps scene load, allocations/queue growth, thermal behavior, interruption and background/resume audio behavior. |

The upstream README reports 19 core tests and 54 Unity EditMode tests, but those are upstream results, not tests rerun here or proof of this game's integration. This audit only inspected source and dependency files. No live API call, package installation, shared Unity run, or end-to-end face acceptance occurred.


## Implemented game-side weighted snapshot API

`CharacterFaceDriver.ApplySpeechFrame(IReadOnlyList<SpeechPoseWeight>)` accepts a complete snapshot with a language **per entry**. Each entry contains `Language`, authored `PoseId`, and a finite weight in `[0,1]`. For example:

```csharp
// Reuse this array; update weights at the audible playback cursor on the main thread.
var frame = new[] {
    new SpeechPoseWeight(SpeechLanguage.English, "l", .35f),
    new SpeechPoseWeight(SpeechLanguage.Japanese, "u", .65f)
};
bool accepted = face.ApplySpeechFrame(frame);
// End, interrupt, disconnect, or confirmed starvation:
face.ResetSpeech();
```

Every call replaces speech and stops the timed review fixture. Omitted targets clear immediately. An empty frame releases speech to the existing expression layer; it does not force a neutral expression. Null frames, unknown languages/poses, duplicate language/pose pairs, ambiguous profiles, invalid authored targets, nonfinite/out-of-range weights, or missing active mesh bindings reject the **whole frame**, clear preceding speech, and set `LastSpeechDiagnostic`. No partial application or English substitution occurs. Zero-weight entries still validate their pose and profile; only positive resolved targets require live mesh bindings. Silence contributes no target: a silence-only frame releases speech, while silence mixed with other entries does not erase their contribution.

`SpeechFrameResolver.TryResolve` is a pure managed helper used by the driver. Authored contributions are accumulated and saturated independently to 100 per morph; weights are not globally renormalized. Only the existing speech morph allowlist is accepted. Expressions, blink timing, gaze, body playback, and rig selection retain their existing owners. The driver remains the renderer writer. This is **API support**, not a contact-aware transition solver: weighted sums do not prove lip seals, FV/L/tap contacts, nonlinear H mouth/blink corrections, or actual H target coverage. The final H morph compositor/profile and audible bilingual acceptance remain incomplete.

### Exact canonical 19-slot mapping

`CanonicalSpeechPoseMap.TryMap(index, segmentLanguage, weight, out pose)` maps one canonical queue slot. It does not read PCM, infer language, normalize weights, or remap acoustic model slots. Reuse a 19-entry array, map each slot, then submit once. Reject/reset if any mapping fails. This table was checked against local read-only `B:\unity-realtime-lipsync/unity-package/Runtime/PoseLabel.cs` at `acea62e125aa2200648a489900de750c3e3587fa`.

| Slot | Canonical label | English segment pose | Japanese segment pose |
| --- | --- | --- | --- |
| 0 | sil | English/sil | Japanese/sil |
| 1 | PP | English/pbm | Japanese/pbm |
| 2 | FF | English/fv | English/fv |
| 3 | TH | English/th | English/th |
| 4 | DD | English/tdn | Japanese/tdn |
| 5 | kk | English/kgng | Japanese/kg |
| 6 | CH | English/chj | Japanese/ch |
| 7 | SS | English/sz | Japanese/sz |
| 8 | nn | English/n | Japanese/n_alveolar |
| 9 | RR | English/r | English/r |
| 10 | aa | English/a | Japanese/a |
| 11 | E | English/e | Japanese/e |
| 12 | I | English/i | Japanese/i |
| 13 | O | English/o | Japanese/o |
| 14 | U | English/u | English/u |
| 15 | ja_U | Japanese/u | Japanese/u |
| 16 | ja_FU | Japanese/fu | Japanese/fu |
| 17 | ja_R | Japanese/r_tap | Japanese/r_tap |
| 18 | en_L | English/l | English/l |

FF/TH/RR/U retain their canonical English articulation in a Japanese segment. A Japanese acoustic producer must already have moved its U/FF/RR values into ja_U/ja_FU/ja_R before canonical queue insertion. The mapping intentionally makes no SH/TSU/contextual-nasal inference. Generation rejection, playback clock ownership, starvation policy, and contact-aware timing remain adapter responsibilities.

Pure tests in `SpeechFrameResolverTests.cs` cover mixed snapshots, omission/empty clearing, whole-frame rejection, duplicates/null input, invalid authored channels, accumulation, every canonical slot in both segment languages, and expression/blink/gaze composition ownership. They do not establish H mesh readiness, audio synchronization, Unity rendering, or phone performance.

## Engineering implementation addendum: native English producer

The real pinned analyzer is now installed as a narrow source dependency in [LiveSpeech](../Unity/Assets/LiveSpeech/README.md): nine unmodified MIT core C# files, the English model imported as a `TextAsset`, and two license/notice files. There is no DLL resolution or optional UPM assembly dependency in this installation. The project's `SplatterfaceGames.LipSync.Core.asmdef` compiles these sources directly; `LucidLoop.LiveSpeech` references it and `LucidLoop.CharacterArt`. [Provenance](../Unity/Assets/LiveSpeech/ThirdParty/provenance.json) records the commit, original source paths, destination paths and SHA-256 values. [Acquisition/verification script](../tools/live_speech/vendor_core.py) reproduces committed bytes from a pinned checkout. No Japanese corpus, model, baseline library or fixture collection is shipped by this addition.

[EnglishSpeechStream](../Unity/Assets/LiveSpeech/Runtime/EnglishSpeechStream.cs) consumes exactly the accepted native 24 kHz mono PCM16 stream. It rejects incomplete samples and stale generations, keeps at most 512 analyzer targets, prunes consumed history, owns analyzer disposal, and fails closed on target overflow or inconsistent playback clocks. A new stream restarts both input time and target time at zero; the host must reset its output counter at the same boundary. Playback sampling uses real consumed samples minus an explicitly uncalibrated latency estimate. Starvation and end-of-stream clear the speech snapshot. Winning PP contacts receive full seal weight; this does not establish every contact's visual correctness.

[LiveSpeechFaceAdapter](../Unity/Assets/LiveSpeech/Runtime/LiveSpeechFaceAdapter.cs) maps the actual 15-slot English analyzer output through `CanonicalSpeechPoseMap` and calls the artist's `ApplySpeechFrame` once per sampled frame. It never writes renderer morphs, blink/gaze, body animation or expression directly. Its API matches the encounter voice controller event seam: begin, accepted PCM, consumed playback progress, and stop/reset. The host must bind the selected character driver explicitly and retain one audio playback owner. `Diagnostic` reports model, clock, mapping and active mesh-target failures.

Live Japanese remains explicitly unsupported by this producer: selecting Japanese yields `LANGUAGE_UNSUPPORTED` with no English approximation. Extended weighted pose support in the artist driver does not create a Japanese acoustic/alignment producer. English L remains a producer limitation too. Adding live Japanese still requires the separately evaluated route described above.

Validation completed for this implementation: **6/6 standalone tests passed**, covering irregular PCM chunk invariance, consecutive stream reset and stale packets, starvation/end clearing, bounded overflow, impossible clocks, and incomplete PCM. All 12 vendored files passed SHA-256 verification. The complete adapter and current CharacterArt source compiled against Unity `6000.3.24f1` managed references with zero errors and three nullable-context warnings in unchanged upstream source. This was a compiler check, not Unity import, IL2CPP/device build, scene playthrough, paid Live session, or audible/visual acceptance. Reproduction commands are in the [runtime README](../Unity/Assets/LiveSpeech/README.md).
