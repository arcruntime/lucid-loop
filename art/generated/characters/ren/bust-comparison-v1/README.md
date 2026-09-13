# Ren bust comparison

This comparison uses the new cap-off head-and-neck references from
[head-reference-sheets-v1](../head-reference-sheets-v1/). The artist's original
Ren sheet remains the likeness authority. See the
[task brief](../../../../../docs/REN_HEAD_SHEET_BRIEF.md) for scope and review.

## Results available for review

Updated 2026-09-13. Three downloaded sources are imported into Unity and have
matched captures. Closed rest and open A were generated separately.

| Imported candidate | Recorded generation route | Actual triangles | Actual base-color image |
| --- | --- | --- | --- |
| Tripo H closed rest | Studio v3.1 Best Quality, Ultra, front + right profile | 1,859,218 | 8192 × 8192 JPEG |
| Meshy closed rest | Meshy 7 Ultra front-only geometry; separate front + profile 8K texturing | 3,028,819 | 8192 × 8192 PNG |
| Meshy open A | Same Meshy route with open-A references | 2,439,591 | 8192 × 8192 PNG |

The Studio label does not independently identify the dated Tripo API backend.
Meshy's Ultra multi-image/combined requests failed; the successful route uses
single-image geometry and separate multi-view texturing. Both generation inputs
and this difference are preserved in provenance. The separate texturing pass
also changes Meshy's connectivity/counts; original untextured geometry is retained.

Tripo H open A and P2 closed/A jobs have completed remotely but **await export**.
P2's generation form did not expose an 8K texture setting, so no P2 8K result is
claimed. These three pending assets are not included in the viewer or recommendation.
The installed official Tripo Codex plugin uses the API account; its read-only
check could not retrieve the Studio job.
[Export handoff](tripo/STUDIO-EXPORT-HANDOFF.md),
[Plugin setup](tripo/PLUGIN-SETUP.md).

Provider originals remain unchanged. The FBX and Blender files are derived viewing
copies; none of these candidates has been retopologized, rigged, or optimized by
this comparison. Exact settings, source hashes, failed requests, and measured
results are recorded in the [Tripo notes](tripo/API-NOTES.md) and
[Meshy results](meshy/README.md).

## Viewer and evidence

Open this existing scene in Unity and enter Play mode:
`Assets/CharacterArt/Generated/Preview/Scenes/RenBustComparison.unity`.

The Windows viewer is also built and visually verified:
[launch viewer](../../../../../.local/ren-bust-viewer/RenBustComparison.exe),
[actual viewer screenshot](unity-review/viewer-front-basecolor.png).
The executable is a local build output; the screenshot is retained as review evidence.

The scene uses actual downloaded assets, with synchronized orbit/zoom,
matched view presets, source references, and three material/lighting modes.
Base color shows the returned texture without added illumination. Neutral and
nightclub modes apply the same diagnostic material response to both candidates.
They are not a finished game shader.

[Viewer setup and manifest schema](../../../../../Unity/Assets/CharacterArt/Editor/REN_BUST_COMPARISON.md)
documents the builder and standalone Windows viewer commands.
[Unity import evidence](unity-review/unity-import-evidence.json) records actual
texture dimensions and rendered triangle counts. The reference selector includes
the original artist sheet, exact generation inputs, and thirteen expression sheets.

## Interim recommendation

**Tripo H closed rest is the preferred construction base among the three inspected
sources.** Its hair masses, lash edges, and lip seam are cleaner. It needs stronger
painted contrast; Meshy retains more of the rose lip color. Both need deliberate
eye and mouth construction and further likeness review. See the
[visual review and captures](REVIEW.md) for evidence and limitations.

The [pipeline research](../../../../../research/japanese-anime-character-workflows-2026-09.md)
now proposes swappable eye assemblies, independent gaze, and continuous mouth
blendshapes fitted from approved expression references. Those controls are the
next construction stage. The current viewer compares static source meshes.
