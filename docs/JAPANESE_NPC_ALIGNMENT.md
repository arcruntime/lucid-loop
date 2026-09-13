# Japanese NPC alignment baseline

The pinned full-clip Japanese aligner was run against all four [captured NPC voices](JAPANESE_NPC_VOICE_EVIDENCE.md), using the [checked pronunciation reading](JAPANESE_READING_FRONTEND.md). This is an offline baseline with complete audio and text available. It does not establish causal production, contact accuracy or believable lip-sync.

The [summary](validation/japanese-npc-alignment/summary.json) records the upstream commit, recording hashes, checked-reading hash, measured desktop computation and per-voice failures. The adjacent alignment files preserve the actual mora and phone spans. No evaluation audio was used for training. The probe verifies the capture files and unchanged pinned upstream sources before importing them.

All voices produced the expected 60 morae with finite, positive, ordered spans. However, energy segmentation found only one or two regions, versus six punctuation-delimited reading phrases. The upstream fallback therefore aligned all morae together across the union of detected regions. Its DP searches the full earlier-frame range for each boundary, making these short passages take seconds of desktop computation. Feature extraction is measured separately; neither measurement includes text conversion, delivery or iOS execution.

The fallback also exposes a pause-handling defect: consecutive mora boundaries can sit on opposite sides of a removed silence gap. Theo's result includes an 840 ms mora over a 670 ms detected gap. `labelAt` checks the mora span without independently checking speech regions, so it can keep a non-silence mouth target throughout the gap. The summary's `gapSpans` reports this independently of `structurallyValid`; correct ordering is insufficient for usable speech animation. Small edge discrepancies no larger than one 2 ms feature hop are excluded from this gap report.

Next implementation work must restrict the DP search to feasible duration windows, measure equivalence before changing alignment costs, and explicitly mask detected silence in target emission. These changes alone will not make the algorithm causal: global energy thresholds, boundary normalization and whole-reading allocation still depend on future input. A bounded live producer also needs phrase completion rules, immutable emitted targets, measured arrival/playback deadlines, and independently reviewed contact labels.

Reproduce after downloading/extracting the original capture artifact and checking out upstream commit `acea62e125aa2200648a489900de750c3e3587fa`:

```powershell
node tools/live_speech/probe_npc_alignment.mjs .local/lipsync-audit .local/japanese-npc-voices-34788497022 docs/validation/japanese-reading/checked-reading.json .local/japanese-npc-alignment
```

The command writes only its output directory. It makes no provider calls and changes no Unity or vendor files. Desktop timings vary with concurrent workloads; use the recorded single-run values as evidence of the current cost, not as a device benchmark.
