# Japanese NPC voice evidence

The [manual capture workflow](../.github/workflows/japanese-voice-corpus.yml) collects independent Japanese speech from the game's configured Maya, Ren, Luca and Theo voices. This is an evaluation corpus, not training data or an accepted Japanese lip-sync producer. The upstream JSUT-derived classifier measurements do not establish performance on these voices.

The collector uses the same `buildSessionStart` voice/model choices as the game, with an isolated authored Japanese recording instruction and no game-state context. It sends silent input audio and one requested passage, then closes each session after a fixed capture window. The requested passage is retained separately from the provider's actual output transcript. Neither a request to repeat text nor a pause in events proves that the spoken passage is complete or matches the script.

Run **Capture Japanese NPC voice evidence** manually on the intended source revision. It uses the repository's existing `OPENAI_API_KEY` secret only during collection. It makes four sequential, bounded provider sessions and retains complete or partial evidence in the `japanese-npc-voices-<run>-<attempt>` artifact for 14 days. Ordinary push tests do not spend provider credit on this capture.

Each sample retains mono 24 kHz PCM audio in a WAV, raw output transcript deltas, event receipt times, cumulative PCM sample spans, available provider timing fields, final usage, and content hashes. The exact capture bounds and failure status are recorded by the collector. A failed or truncated recording must not be relabeled as a completed utterance.

## Timing interpretation

Keep three concepts separate:

- Receipt time is measured by the capture process's monotonic clock.
- PCM sample position is the concatenation order of received output audio. Concatenation can remove gaps between utterances.
- Provider transcript timestamps belong to the provider's timeline. Without audio timestamp anchors or another verified mapping, they cannot be converted to PCM indices simply by multiplying milliseconds by 24.

This headless run has no actual playback device or audible cursor. It cannot measure physical latency, underruns at an iPhone speaker, or how much text is available before a sound is heard. Preserve raw evidence for a subsequent instrumented playback experiment rather than inventing that relationship.

## First actual capture

[Run 34788497022](https://github.com/jethac/lucid-loop/actions/runs/34788497022) passed at `c341691`. All four sessions returned nonzero PCM, Japanese output transcript fragments and final usage. [Raw corpus ZIP](validation/japanese-npc-voices-34788497022.zip) preserves the original WAVs and JSON bytes beyond Actions retention; [inspection summary](validation/japanese-npc-voices-summary.json) records verified file hashes and sample spans.

| NPC / voice | Captured PCM duration, including silence | Transcript fragments |
| --- | --- | --- |
| Maya / gleam | 34.1 seconds | 41 |
| Ren / quartz | 34.1 seconds | 40 |
| Luca / meridian | 34.0 seconds | 42 |
| Theo / vesper | 34.1 seconds | 37 |

Each receipt-order transcript concatenation matches the requested passage. No PCM sample reached either full-scale endpoint. These are data checks, not listening-based pronunciation, distortion or clipping acceptance. One or two packets per voice arrived outside the fixed capture window; their metadata remains recorded, but their audio is intentionally excluded from the WAV. The files include silence, so these durations are not speech-duration measurements.

The observed audio packets contain **only `type` and `delta`**, with no provider timestamp or event ID. Output transcript fragments do contain `start_ms`, `end_ms` and an event ID. That asymmetry leaves the transcript-to-PCM mapping unverified. Do not treat these samples as aligned phoneme labels or use first-packet receipt as a validated audible-time anchor.

After extracting the ZIP into a fresh directory, reproduce inspection with `python tools/live_speech/inspect_voice_capture.py <directory> --output <report.json>`. The tool verifies WAV/event-file hashes, format, sample counts and contiguous packet sample spans before reporting observations.

## Evaluation and implementation boundary

Reserve these recordings for evaluation; do not train on them. First review what was actually spoken, pronunciation, transcript fidelity and clipping. Human contact labels and rendered-face comparison remain required for believable Japanese articulation. Retain negative examples and capture failures.

The pinned upstream alignment tool reads an entire WAV and validated kana before running global segmentation and dynamic programming. Its `lookaheadMs: 0` output does not establish causal streaming. Reusable kana rules, feature extraction and alignment code still need a validated reading frontend, explicit rejection of unknown characters, bounded phrase buffering and a policy for revised/late transcripts. Raw Japanese text contains kanji and Latin names; the upstream kana parser silently skips unknown characters and must not receive such text unchecked. See [Japanese producer investigation](JAPANESE_SPEECH_PRODUCER.md) for the model quality findings and required acceptance work.
