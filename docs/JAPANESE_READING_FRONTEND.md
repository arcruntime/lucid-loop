# Checked Japanese reading prototype

The experimental [Python frontend](../tools/live_speech/japanese_reading.py) converts bounded Japanese text into provisional pronunciation records. The [Node handoff](../tools/live_speech/checked_kana.mjs) validates those readings before invoking the pinned upstream kana parser. These tools do not generate speech, train a model, supply audio timing, or enable Japanese in Unity.

## Actual findings

Official [pyopenjtalk](https://github.com/r9y9/pyopenjtalk) 0.4.1 built successfully in an isolated Windows Python 3.11.9 environment. Its OpenJTalk frontend exposes token surface, reading, pronunciation and mora count. It has a separate speech-synthesis backend; this project used only text processing. The wrapper uses a directly constructed frontend bound to a verified dictionary, so another caller's cached global frontend cannot silently select a different dictionary.

The [13-case observations](validation/japanese-reading/frontend-observations.json) exposed behavior that a simple kana-output check would miss:

- Latin cast names become spelled-out letters. The prototype explicitly authors `Ren → レン`, `Maya → マヤ`, `Luca → ルカ`, and `Theo → テオ`; other Latin words are rejected. These aliases are intended Japanese readings, not proof of a particular recording's pronunciation or general code-switch support.
- Unknown 髙橋 and 﨑田 become zero-mora punctuation pronunciations; 𠮷田 can lose its first character and keep only タ. The wrapper rejects unknown spoken tokens instead of accepting commas as a successful reading.
- Emoji and unsupported scripts are rejected before native processing. Real punctuation is handled through an explicit surface allowlist.
- Pronunciation differs from spelling: particle は becomes ワ. OpenJTalk devoicing markers are preserved in raw token metadata and counted; they are explicitly removed only from the kana handoff string.

The captured NPC passage produced [checked token readings](validation/japanese-reading/checked-reading.json) and [60 mora records in six phrases](validation/japanese-reading/kana-handoff.json). The mora handoff preserves Japanese U/FU, geminates and contextual nasals, rejects skipped input and invalid leading/cross-phrase long-vowel marks, and records that timing is unestablished. This is not a native-speaker pronunciation review, phoneme ground truth or a streaming-latency result.

## Reproduce

Install [pinned requirements](../tools/live_speech/requirements-japanese-reading.txt) in a separate virtual environment. The observed build required native C/C++ compilation. Download the official OpenJTalk dictionary archive from the URL in [the lock file](../tools/live_speech/japanese_reading_lock.json), verify archive SHA-256 `fe6ba0e43542cef98339abdffd903e062008ea170b04e7e2a35da805902f382a`, and extract it to a dedicated directory. The wrapper verifies all nine extracted dictionary files before loading them. It does not download missing resources.

Provide UTF-8 JSON containing a `text` field, then run:

```text
python tools/live_speech/japanese_reading.py --dictionary <dictionary-directory> --input <request.json> --output <reading.json>
```

Input is limited to 2,048 UTF-8 bytes both before and after normalization, below the upstream native frontend's fixed buffer. The remaining pipeline must separately bound queued phrases, audio and computation. Tokens expose no source offsets; normalization and numeric processing may change surfaces. Do not invent precise transcript-character spans from token order.

For checks, set `OPEN_JTALK_DICT_DIR` to the verified dictionary and run `python -m unittest discover -s tools/live_speech -p test_japanese_reading.py -v`. Set `LUCID_LIPSYNC_SOURCE` to upstream checkout `acea62e125aa2200648a489900de750c3e3587fa` and run `node --test tools/live_speech/checked_kana.test.mjs`. The Node loader verifies normalized source hashes for `ja/kana.mjs` and `ja/kana-map.json` before loading them; it does not install or modify upstream files.

## Distribution and next work

The wrapper is MIT licensed; OpenJTalk and its dictionary have their own BSD-style notices. Preserve the complete dictionary `COPYING` if distributing it. Source/version/file hashes are recorded in the observation and lock files. No dictionary, package binary, HTS voice or synthesis resource is bundled into Unity or the relay by this change.

The next implementation still needs bounded audio/text windows, provisional-reading revisions, verified timing and playback deadlines, plus independent audio/face evaluation. A valid kana reading can still differ from what the NPC actually pronounced. Keep the [NPC corpus](JAPANESE_NPC_VOICE_EVIDENCE.md) reserved for evaluation and do not use this provisional frontend output as unquestioned training labels.
