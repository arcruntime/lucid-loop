# Ren hair detail recovery — one bounded comparison

This comparison restores the existing Tripo hair's irregular overlapping layers,
curved locks, narrow tips and gaps. It is **9,004 rendered triangles**. It does
not rebuild or continue the rejected procedural ribbon hair, and it is not final
Ren likeness, UV work, or a finished head/cap fit.

The P2 face in these images is temporary context. The user has selected a
different Studio H face as the next baseline; fitting against this P2 head is
therefore deliberately limited to one provisional cap state.

## Inspect the actual 3D captures

| State | Front | Quarter | Profile |
|---|---|---|---|
| Recovered detail, cap off | [Front](candidate-cap-off-front.png) | [Quarter](candidate-cap-off-quarter.png) | [Profile](candidate-cap-off-profile.png) |
| One temporary cap fit | [Front](candidate-cap-on-front.png) | [Quarter](candidate-cap-on-quarter.png) | [Profile](candidate-cap-on-profile.png) |
| Unchanged 9,026-triangle source, same context | [Front](source-cap-off-front.png) | [Quarter](source-cap-off-quarter.png) | — |

All are real Blender 5.1.1 renders at 960×960, with identical cameras, neutral
lighting and uniform clay hair. The existing painted head and black cap provide
context; no new strand textures, normal maps or paint hide the hair geometry.
The head is in its closed-mouth neutral state. [captures.json](captures.json)
records image hashes and camera settings.

The earlier [native 51,054-triangle versus returned 9,026-triangle comparison](../tripo-hair-lowpoly-v1/README.md)
provides matched hair-only views of both provider sources. The return preserves
the overall layered design but already has blunted crown roots and more angular
thin tips than the native source.

## What changed

- Input: the immutable [returned Tripo FBX](../tripo-hair-lowpoly-v1/originals/model.fbx),
  SHA256 `236db190bd05e48750fb68d23f381538a4917fa8c1ac6255dee533a4c0bdf036`.
- Removed 22 suspect internal triangles from its 9,026. Only small-component,
  sliver or branched-edge faces were eligible; all three vertices, three edge
  midpoints and the centroid had to lie at least 0.004 native units inside the
  temporary head surface. This is a conservative sampled, context-dependent
  check, not a proof of invisibility on a different face.
- Every retained Basis vertex coordinate and triangle winding is unchanged.
  There was no decimation, smoothing of positions, replacement lock construction,
  or nonrigid cap-off fit. Source vertex IDs are retained, including unused ones.
- Review placement is translation **(-0.09, 0, 0.22)**, scale **1**. Native axis
  orientation is +X front, +Z up. Export object transforms preserve this explicit
  placement; the original provider files remain in their original frame.
- The separate `capOn_TEMPORARY_P2_FIT` morph moves 3,273 upper vertices, maximum
  displacement 0.208153 native units, into a sampled head/cap corridor. Lower
  vertices below local cap-band height minus 0.045 are untouched. A virtual
  fitting skirt constrains roots but adds no output geometry. Three rays missed;
  zero sampled insufficient corridors does **not** certify whole-triangle fit.

[geometry-report.json](geometry-report.json) contains the exact removed source
face/vertex IDs, kept face IDs, per-vertex cap movement, transforms, source and
output hashes, and input-preservation checks. The native 51,054-triangle FBX and
all previously frozen source assemblies are unchanged.

## Geometry artifacts

- [Review Blender scene](Ren_TripoHair_Detail_Review.blend): current head/cap
  context plus separate hidden unchanged-source hair and the visible candidate.
  Default is cap off. The source object is `Ren_Hair_Tripo9026_Unchanged`; the
  candidate is `Ren_Hair_TripoDetail_Comparison`.
- [Hair-only FBX](Ren_TripoHair_Detail_Comparison.fbx).
- [Hair-only GLB](Ren_TripoHair_Detail_Comparison.glb).

Hair exports default to Basis/cap off, with the temporary cap morph retained.
Show the existing cap and set that morph to 1 only for the provisional P2 fit.
Both exports are local derivatives, not provider originals or installed viewer
assets. There are **no UV layers** and only a uniform neutral material.

The frozen Blender SHA256 is
`1dd08be9807a19d128e97137c28126ba1700e00f223634fb14e93eeb0944e260`.
The cap context uses the final band-clearance accessory source SHA256
`bff2a53430d58535e6f2aef8d271e8a8774a6c8015185a4cd5f2b74168ab805d`.

[verification.json](verification.json) confirms FBX and GLB each reimport with
9,004 triangles and both position states. Maximum state-vertex error is below
0.000000086 native units. Export vertex packing differs (FBX 5,189; GLB 5,426),
while rendered triangle counts agree. The head and cap vertex coordinates,
face shapes and matrices, every source file and all frozen exports were checked
unchanged. Verification never resaves the review scene.

## Visual review and recommendation

The cap-off front and quarter restore the layered, asymmetric outline and slim
curved ends lost in the ribbon version. The profile confirms the layered back
mass, but also exposes retained crossbars behind the ear, a thin horizontal
strand across the temple, ragged inner sheets along the cheek, and broken-looking
crown junctions. Those are real source defects, not texture problems. They were
kept when deleting them could alter the visible lock or silhouette.

The cap-on view retains the fine lower locks, but the one radial fit crowds and
flattens some side roots against the band. That fit is **not accepted**. It should
be redone only after the selected Studio H head is available; the unchanged
cap-off geometry remains the useful basis.

Use this as the detail-preserving comparison and retain the native 51k source
as the shape guide for targeted manual repair. Do not force it below 8k by
replacing the layers. The next repair should resolve specific visible crossbars,
inner cheek sheets and crown joins while tracing the existing locks, then fit
the selected face and create paintable UVs. This pass makes no final likeness
claim and performs no texture, body, rig, Unity or viewer integration.

## Reproduce

The bounded script is
[`tools/character_art/recover_ren_hair_detail.py`](../../../../../../tools/character_art/recover_ren_hair_detail.py).
It starts an isolated background Blender process and writes only this directory.
Preserve this frozen comparison; change the script's output directory for a new
iteration. `python tools/character_art/recover_ren_hair_detail.py --verify` reruns
read-only export checks without regenerating or resaving the artifacts.
