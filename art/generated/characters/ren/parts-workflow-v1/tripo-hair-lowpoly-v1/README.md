# Native Ren hair: one Tripo 8k retopology trial

**The result preserves the source shag better than the local 7,900-triangle
candidate, but fails the 8,000-triangle target and needs geometry cleanup and UVs.**
It is a static comparison and possible repair source, not an approved replacement.

This experiment followed the local procedural hair's visual review: its broad
ribbons remained too schematic against the reviewed wig reference. Tripo task
`690bc771-33c8-4e75-ab76-4588adb67e38` used the existing native hair task
`1a8b7598-cddf-4b6c-a344-9e9ae16dcd5d`, with `model=v2.0`, `face_limit=8000`,
`quad=false`, `bake=false`. [Receipt](receipt.json): **30 credits**. Exactly one
non-retrying creation POST was issued after [durable intent](submission-intent.json).
There was no source upload, fallback upload, second task or paid conversion.

## Inspect the actual result

| View | Native source, 51,054 triangles | Tripo target 8k, actual 9,026 | Local selected, 7,900 |
| --- | --- | --- | --- |
| Front | [Native](review/native-front.png) | [Tripo](review/tripo8000-front.png) | [Local](review/local7900-front.png) |
| Three-quarter | [Native](review/native-quarter.png) | [Tripo](review/tripo8000-quarter.png) | [Local](review/local7900-quarter.png) |
| Profile | [Native](review/native-profile.png) | [Tripo](review/tripo8000-profile.png) | [Local](review/local7900-profile.png) |

All nine captures use the same 900-square camera framing, lighting and neutral clay
material. Native and returned hair receive the same historical rigid translation
`(-0.09,0,0.22)` into the head frame; the fitted local candidate receives identity.
No scale, axis correction or nonrigid fit was used to make the returned silhouette
appear closer. The different overall envelopes are therefore visible. These are
hair-only comparisons, not a demonstration of fit to the current head/cap.

The service returned **FBX despite `quad=false`**. `originals/model.fbx` contains
the exact downloaded bytes and all 9,026 faces are triangles. No provider GLB was
returned. `derived/Ren_NativeHair_8000_Local.glb` is a clearly labeled local
materials-free export for convenient viewing; reimport confirms 9,026 triangles
and no UV layer. It is not described as provider-original bytes.

The comparison Blender file is `review/Ren_Hair_Retopology_Comparison.blend`.
It contains three distinct candidates; only the returned candidate is enabled for
rendering by default. The saved camera is the profile comparison view.

## What survived, and what did not

The Tripo result retains the tall, asymmetric, layered shag envelope, the long
curved front locks, outward-pointing side tips and irregular layered back. These
are visibly closer to the [reviewed source plate](../../parts-reference-v1/hair/generation-inputs/hair-front-v2.png)
and native geometry than the lower, compact, uniform strip structure of the local
candidate. This is a comparison of shape, not final painted likeness approval.

However, reduction leaves the native internal crossbars and intersecting slivers,
and introduces angular flyaways, flattened/blunt crown roots, broken-looking tips
and stepped transitions between some locks. These defects are especially clear in
the quarter/profile captures. Painting alone will not fix those silhouettes.

| Metric | Native hair | Returned hair | Local candidate |
| --- | ---: | ---: | ---: |
| Triangles | 51,054 | **9,026** | 7,900 |
| Position-connected components | 79 | 193 | 54 |
| Boundary edges | 4,293 | 745 | 32 |
| Edges with more than two incident triangles | 2,027 | 82 | 0 |
| Zero-area geometric triangles | 4 | 0 | 0 |

The returned triangle count is **1,026 over target, or 12.825%**. Position
connectivity uses coordinates rounded to seven decimal places, independently of
UV vertex splits. Open hair sheets can legitimately have boundaries; these counts
alone are not an animation or visual pass. The returned 193 components and visible
crossbars warrant local selection/cleanup rather than treating the mesh as a
single finished wig.

The return has **no UV layers**. With `bake=false`, no texture preservation or
usable painting layout is claimed. The local candidate has longitudinal UVs with
intentionally collapsed hidden tip/root caps; its existence does not provide UVs
for this different returned topology. A fresh unwrap is required after cleanup.

[Geometry audit](review/geometry-audit.json) includes hashes, object transforms,
UV inventory and bidirectional source-vertex-to-surface samples. The native-to-
return p95 distance is 0.0361 native units, versus return-to-native p95 0.00967;
thin omitted strands contribute to that asymmetry. These proximity measurements
are not a substitute for inspecting the silhouettes. All source-file hashes were
unchanged after rendering.

**Recommendation:** consider this return as the stronger source for lock layout
and silhouette, then repair the crown/tips and remove confirmed hidden crossbars
locally before fitting the head/cap, reducing the remaining 1,026+ triangles and
unwrapping. Do not replace the integrated hair merely because the service task
succeeded. This experiment did not create cap morphs, skin weights, animation,
paint, Unity assets or a phone-performance result.

## Reproduce or resume

[Submission helper](../../../../../../tools/character_art/tripo_hair_lowpoly_jobs.py)
uses the existing BWS loader and non-retrying `TripoApi` transport. Credentials and
raw signed download URLs stay in ignored `.local/character-art/tripo-hair-lowpoly-v1/`.
Public records are sanitized. An existing submission intent prevents another
creation call; `resume` only watches/downloads the saved task.

[Audit helper](../../../../../../tools/character_art/audit_ren_tripo_hair_lowpoly.py)
reads the native FBX, returned FBX and selected local GLB in a background Blender
process, writes the local GLB derivative and these matched comparison files. It
does not save or modify any of its source assets. Re-running replaces only this
trial's derived review output. `tripo8000-*` filenames name the requested target;
the measured result is always 9,026 triangles.
