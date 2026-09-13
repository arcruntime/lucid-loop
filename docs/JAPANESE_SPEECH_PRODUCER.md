# Japanese live speech producer: evidence and implementation path

The requested repository already contains a **real Japanese acoustic model**. It is not the English model with renamed outputs. A scratch C# experiment confirms that the existing core can produce Japanese-specific targets quickly on this Windows workstation. However, its label quality remains weak even after reducing vote smoothing. Installing this model alone would not satisfy believable Japanese speech. This investigation changes no Unity or CharacterArt files and does not enable Japanese in the shipping adapter.

## What is available now

Audited revision: `splatterfacegames/unity-realtime-lipsync@acea62e125aa2200648a489900de750c3e3587fa`.

| Item | Evidence and implication |
| --- | --- |
| Japanese model | `ja/model-ja-mixed.bin`, 12,512 bytes; SHA-256 `e085a7c11f43c88dee7b139704e63369631ba0f61193b5fb640128bc231634ca`. Loads through the existing pure C# `GaussianModel`/`VisemeAnalyzer`. |
| Model license | Upstream explicitly distributes this derived model/report under **CC BY-SA 4.0**, with attribution to JSUT/Saruwatari Lab and jsut-label/sarulab-speech. It is not part of the MIT-only English dependency currently installed. Preserve its declared license and provenance if distributing it. |
| Data | Existing Japanese fixtures are native Japanese female read speech from JSUT; one separate fixture splices English. The source manifest and fixture notice identify actual recorded speech, despite an upstream analysis document's inconsistent synthetic-speech wording. |
| Training | Gaussian prototypes fitted to this fixture collection using machine-derived kana/energy alignment. The same collection is used in its evaluation, so neither the original numbers nor this probe represent held-out generalization. |
| Canonical semantics | After the core's internal-to-canonical conversion, remap canonical `U → ja_U`, `FF → ja_FU`, `RR → ja_R`, including every weight, not just the winning label. Do not confuse these canonical names with raw model slot indices. |
| Unity artist seam | `ApplySpeechFrame` and `CanonicalSpeechPoseMap` already support the extended poses. Producer correctness, segment language and actual mesh contacts remain separate gates. |

Primary source: [Japanese model license](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/ja/MODEL_LICENSE.md), [fixture provenance](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/fixtures/ja/NOTICE.md), [training implementation](https://github.com/splatterfacegames/unity-realtime-lipsync/blob/acea62e125aa2200648a489900de750c3e3587fa/ja/train-model.mjs), [artist contract](BILINGUAL_SPEECH_RIG.md).

## Bounded experiment completed

[Probe script](../tools/live_speech/probe_japanese.py) copies the pinned C# core into `.local/japanese-producer-probe`, changes only the classifier's vote-window capacity and reset bound, then feeds existing PCM WAVs in 960-float chunks. It remaps the three Japanese slots after core output. It leaves the installed English source untouched.

The probe used **39 Japanese clips, 170.94 seconds, 10,628 analysis frames per variant**. It excluded the English/Japanese splice because running a Japanese-only model over its English half would conflate language routing with Japanese quality. Reference targets are the upstream machine alignment, sampled at or immediately before each analyzer frame timestamp. [Full measured results](data/japanese-producer-probe.json).

| Vote frames | All-frame agreement | Nonsilence agreement | ja_U recall | ja_FU recall | ja_R recall | PP recall | Label changes/second |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 6 (upstream) | 29.3% | 17.7% | 11.8% | 7.1% | 14.0% | 23.5% | 14.8 |
| 3 | 30.4% | 19.1% | 10.7% | 14.3% | 35.7% | 28.1% | 20.4 |
| 2 | 27.8% | 16.0% | 13.2% | 42.9% | 38.8% | 24.9% | 24.1 |
| 1 (no temporal vote) | 32.6% | 21.8% | 16.2% | 42.9% | 28.7% | 31.3% | 33.6 |

These are **viseme-label agreement and recall against an estimated reference**, not phoneme accuracy or measured lip contact. The FU denominator is only **14 reference frames**: the apparent improvement is one matched frame versus six. Other reference denominators: ja_U 1,148; ja_R 129; PP 217. Thin contacts, same-corpus training and machine timing make a confident accuracy claim inappropriate. More frequent label changes indicate a stability tradeoff, not a direct count of visibly wrong movements.

Measured desktop processing time was 4.51–5.57 seconds per 170.94 seconds of audio (RTF 0.026–0.033). This measures analysis only in one process under varying workstation load; the first variant can include JIT overhead. It excludes Unity, rendering, audio buffering and phone thermals. It shows a bounded C# producer is computationally plausible, not that iOS performance has passed. A 32 ms analysis window and 16 ms hop still impose analysis timing; a six-frame vote can defer stable contact by several hops. Removing the vote reduces that history but does not remove feature-window or output-device latency.

One implementation detail matters for the existing adapter: core `VisemeFrame.Label` uses the temporal vote, while `Weights` are derived from the current prototype distances. They are not the same signal. A consumer blending `Weights` does not automatically inherit the voted label's stability. Changing vote length mainly affects the chosen label and any label-based contact rule. Evaluate the **rendered consumer path**, not just winner histograms, before selecting a production smoothing policy.

Reproduce from repository root:

```powershell
python tools/live_speech/probe_japanese.py --source .local/lipsync-audit
```

## Concrete next implementation

Keep the current English route unchanged. Introduce a producer registry whose entry binds `(language, model hash, label-map version, status)`, with Japanese initially marked **experimental**, rather than weakening the English-only guard globally. The scratch prototype shows that this entry can use the current C# analyzer and tiny Japanese model; no ONNX/CoreML/native plugin is required for that path. An experimental scene can preserve the bounded target queue and consumed-sample clock already implemented.

Before enabling it as the normal Japanese experience, complete these gates:

1. **Independent target-voice recordings and contact labels.** Use Japanese samples from the actual four NPC voices and retain an untouched test split. Human-review the small but essential set: U/FU/tap, P/B/M, contextual ん, geminates, long vowels, devoiced vowels and code-switch boundaries. Measure contacts and boundary offsets against this reference, not labels produced by the same aligner that trained the model. Do not train on the evaluation split.
2. **Choose acoustic or transcript-assisted production using measured Live timing.** Record output PCM sample spans, received transcript `start_ms/end_ms`, receipt time, and audible cursor. Measure how much corresponding text arrives before or after playback. This determines whether text can help before the audio is heard. The current events do not provide mora/phone boundaries or reliable per-segment language.
3. **For sufficiently early text, implement constrained Japanese alignment.** Convert text to a validated reading, apply the existing kana/context rules, and align those expected contacts against buffered audio. Adapt the upstream full-clip DP to bounded phrases/windows; emit immutable timestamped targets before their playback deadline. Keep a small revisable lookahead region and reject late corrections to already-heard audio. Store explicit source timing and confidence. The necessary buffer duration is a measurement result, not an invented guarantee of low latency.
4. **If text is too late, improve the acoustic route on independent data.** Expose the raw contact signal separately from vowel smoothing, retrain with balanced contact examples across voices, and evaluate both contact recall and false contacts. The vote-window result is insufficient to select a production setting. A larger causal phonetic model is a later option if the small Gaussian features remain unable to separate classes; it is a new model/export/device-validation project.
5. **Run audio-plus-face and phone acceptance.** Feed actual producer targets through the artist's weighted snapshot, retain its contact ownership, and record audible output with the rendered face. Test interruptions, resumed speech, long sessions, target queue limits, and false language selection. Only these tests can establish that speech is believable at the actual camera distance.

This is a feasible implementation sequence with a real existing prototype and explicit rejection criteria. It is not evidence that switching the current runtime to Japanese today would meet the user's goal.

## Focused external options checked

| Primary source | What it actually supplies | Fit for this project |
| --- | --- | --- |
| [Allosaurus](https://github.com/xinjli/allosaurus) and its [ICASSP paper](https://arxiv.org/abs/2002.11800) | Multilingual phone recognizer, language inventory selection and approximate CTC phone timestamps. The repository is Python-based and GPL-3.0; its documented interface consumes audio files. | Useful offline comparison/label-assistance candidate. No supplied Unity/iOS streaming implementation was established in this audit, and approximate CTC timestamps are not contact ground truth. Do not assume its model merely drops into the current C# Gaussian loader. |
| [sherpa-onnx iOS](https://k2-fsa.github.io/sherpa/onnx/ios/index.html) and [Japanese Zipformer model documentation](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/offline-transducer/zipformer-transducer-models.html) | Native iOS inference support and Japanese speech-to-text models. The inspected ReazonSpeech option is documented among offline transducers; token timestamps are lexical output. | Deployment tooling is real, but it does not supply this game's Japanese phoneme/contact producer. Adding ASR duplicates text already available from Live without solving internal phone timing. |
| [OpenJTalk Python wrapper](https://github.com/r9y9/pyopenjtalk) | Japanese text-to-reading/phoneme conversion and user dictionaries; wrapper MIT, OpenJTalk modified BSD. | Concrete input stage for transcript-assisted alignment. It does not infer when the original audio pronounced those phones. Use the frontend only; its TTS voice is unrelated to native NPC audio. Server-side use avoids requiring Python on iOS; native on-device frontend packaging/dictionaries would be additional work. |

No external model, runtime, or paid service was installed or called for this investigation.

## Language routing and Japanese text are additional dependencies

`EncounterSpeechBinding.Language` currently defaults to English and is read when the stream begins. It is configuration, not detection of the language actually spoken. A Japanese setting currently reaches `LANGUAGE_UNSUPPORTED`, as intended. Changing only the field mid-stream does not change the active analyzer. [Current binding](../Unity/Assets/Gyms/Runtime/Encounter/EncounterSpeechBinding.cs).

For an initial single-language session, carry an explicit requested conversation language from settings into both voice instructions and the producer registry. Treat that as an intended-language policy, not proof about each emitted sound. Restart analysis/playback generation together when changing language at a confirmed safe boundary. Without reliable language timestamps, avoid declaring arbitrary code switching supported. Kana-versus-Latin script heuristics are insufficient: Japanese names, loanwords and English abbreviations can be spoken differently from their spelling. A transcript-assisted route must preserve per-segment readings and sample-aligned language; a future bilingual acoustic model needs its own demonstrated cross-language mapping.

The current UI uses `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`; no `.ttf` or `.otf` font is bundled under `Unity/Assets` in this audit. Thus Japanese glyph coverage and fallback have not been established. This does not prevent PCM analysis, but it can make transcripts, language selection and Japanese acceptance unreadable. Bundle and assign a tested Japanese-capable font, retain its license, and test kana, common/uncommon kanji, punctuation, Latin names, wrapping and clipping on iPhone. [Noto CJK](https://github.com/notofonts/noto-cjk) supplies Japanese font distributions; its [SIL OFL license](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE) permits bundling under its stated terms. Arbitrary generated transcripts need broader coverage than a tiny static UI glyph subset. No font asset was imported while phone validation was active.
