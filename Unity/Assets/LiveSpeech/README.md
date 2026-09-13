# Native speech adapter

The runtime uses the actual `VisemeAnalyzer` and English Gaussian model from `splatterfacegames/unity-realtime-lipsync`, pinned at `acea62e125aa2200648a489900de750c3e3587fa`. The nine core source files are unchanged; the project supplies assembly definitions and a bounded playback-clock adapter. No optional Unity demo, Japanese corpus/model, baseline uLipSync, or DLL installation is needed for this English path. See `ThirdParty/provenance.json`, `LICENSE.txt`, and `THIRD-PARTY-NOTICES.md`.

`LiveSpeechFaceAdapter` is explicitly bound to the active artist-owned `CharacterFaceDriver`. Host wiring calls `BeginStream(generation, language)` when both PCM consumption and analyzer input start at zero; passes only accepted mono PCM16/24 kHz packets to `PushPcm16`; and calls `UpdatePlayback(consumedSamples, generation, starved, ended)` every main-thread frame. Consumed samples exclude synthesized underrun silence. Both playback and analysis must reset on overflow, character/session change, and loop reset. Never reset only one clock. The adapter does not create or play audio.

Every visual write goes through `ApplySpeechFrame`; no blendshape, body, blink or gaze write bypasses the artist driver. English canonical 15-slot output maps through `CanonicalSpeechPoseMap`. A winning PP contact is held at full weight instead of mixed through an open vowel. This is a narrow safeguard, not visual proof of consonant contacts. The current acoustic producer does not output a distinct English L. Strong emotion/contact tests and real mesh review remain required.

Japanese requests fail explicitly with `LANGUAGE_UNSUPPORTED`; the English model is never presented as Japanese coverage. Missing model, mapping, mesh target and timing errors remain visible through `Diagnostic`. `OutputLatencyMs` defaults to an uncalibrated 40 ms estimate; device validation must measure it. No rendered or audible acceptance is claimed.

Run from repository root:

```powershell
python tools/live_speech/vendor_core.py --verify
dotnet test tools/live_speech/Tests.csproj
```

To reproduce source acquisition, clone the upstream repository into an isolated location, check out the pinned commit, then run `python tools/live_speech/vendor_core.py --source <checkout>`. This copies committed bytes and records hashes, excluding fixtures and research models.
