# Japanese NPC alignment baseline

**Deferred as of 2026-09-14:** the user paused Japanese voice work and excluded it from shipping scope. Findings and proposed next steps below are archived, not active release requirements.

The pinned full-clip Japanese aligner was run against all four [captured NPC voices](JAPANESE_NPC_VOICE_EVIDENCE.md), using the [checked pronunciation reading](JAPANESE_READING_FRONTEND.md). This is an offline baseline with complete audio and text available. It does not establish causal production, contact accuracy or believable lip-sync.

The [summary](validation/japanese-npc-alignment/summary.json) records the upstream commit, recording hashes, checked-reading hash, measured desktop computation and per-voice failures. The adjacent alignment files preserve the actual mora and phone spans. No evaluation audio was used for training. The probe verifies the capture files and unchanged pinned upstream sources before importing them.

All voices produced the expected 60 morae with finite, positive, ordered spans. However, energy segmentation found only one or two regions, versus six punctuation-delimited reading phrases. The upstream fallback therefore aligned all morae together across the union of detected regions. Its DP searches the full earlier-frame range for each boundary, making these short passages take seconds of desktop computation. Feature extraction is measured separately; neither measurement includes text conversion, delivery or iOS execution.

The fallback also exposes a pause-handling defect: consecutive mora boundaries can sit on opposite sides of a removed silence gap. Theo's result includes an 840 ms mora over a 670 ms detected gap. `labelAt` checks the mora span without independently checking speech regions, so it can keep a non-silence mouth target throughout the gap. The summary's `gapSpans` reports this independently of `structurallyValid`; correct ordering is insufficient for usable speech animation. Small edge discrepancies no larger than one 2 ms feature hop are excluded from this gap report.

The follow-up below implements feasible duration windows and silence masking. These changes do not make the algorithm causal: global energy thresholds, boundary normalization and whole-reading allocation still depend on future input. A bounded live producer still needs phrase completion rules, immutable emitted targets, measured arrival/playback deadlines, and independently reviewed contact labels.

Reproduce after downloading/extracting the original capture artifact and checking out upstream commit `acea62e125aa2200648a489900de750c3e3587fa`:

```powershell
node tools/live_speech/probe_npc_alignment.mjs .local/lipsync-audit .local/japanese-npc-voices-34788497022 docs/validation/japanese-reading/checked-reading.json .local/japanese-npc-alignment
```

The command writes only its output directory. It makes no provider calls and changes no Unity or vendor files. Desktop timings vary with concurrent workloads; use the recorded single-run values as evidence of the current cost, not as a device benchmark.

## Duration-window optimization and silence masking

The [experimental fork](../tools/live_speech/vendor/japanese_alignment/README.md) preserves the original costs and tie order but searches only predecessor boundaries within the existing 30–340 ms duration constraint. Monotonic frame times allow these candidate ranges to be computed once. This removes the full-prefix scan for each mora/boundary pair. The original MIT license and original source hashes are retained.

The [comparison report](validation/japanese-npc-alignment/optimized-summary.json) records exact equality of the entire alignment result for every NPC, including all 60 morae, phone spans, flags, segments and parsed phrases. Forty-eight additional deterministic dense/gapped/infeasible domain cases matched the pinned DP. Silence-mask tests passed half-open interval boundaries and nonfinite timestamps; the actual 16 ms target-grid comparison verifies unchanged speech-region labels and silence everywhere outside detected regions.

| Voice | Original DP ms | Optimized DP ms | Original non-silence grid frames masked |
| --- | ---: | ---: | ---: |
| Maya | 5,622 | 450 | 5 |
| Ren | 18,172 | 1,221 | 0 |
| Luca | 12,562 | 826 | 0 |
| Theo | 10,846 | 1,015 | 42 |

These are separate single desktop runs under concurrent workloads, not controlled device benchmarks. Feature extraction remains additional cost. The optimized output still contains the original boundary spans; consumers must use `labelAtMasked`, rather than the retained reference `labelAt`, to apply silence masking. Masking trusts the existing energy detector and does not prove it correctly identifies every unvoiced speech sound.

```powershell
$env:LUCID_LIPSYNC_SOURCE='B:/lucid-loop/.local/lipsync-audit'
node --test tools/live_speech/japanese_alignment.test.mjs
node tools/live_speech/probe_npc_alignment.mjs .local/lipsync-audit .local/japanese-npc-voices-34788497022 docs/validation/japanese-reading/checked-reading.json .local/japanese-npc-alignment-fast docs/validation/japanese-npc-alignment
```

The optional fifth argument selects the optimized fork and requires exact equality against the supplied original results. The tests skip upstream DP comparison if `LUCID_LIPSYNC_SOURCE` is absent; both tests ran without skips for this evidence. Memory still grows with mora count × feature-domain size. No Unity runtime path is enabled by this checkpoint.
