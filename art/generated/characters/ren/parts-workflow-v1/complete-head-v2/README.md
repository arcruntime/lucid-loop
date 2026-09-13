# Ren complete-head V2 review

This is the current assembled head for visual review: painted skin, independent
eyes and oral geometry, pale-blond hair, the lower black cap and right-ear jewelry.
It has working gaze, independent/idle blink, mouth seal/open A and cap fitting.
Hair likeness and closed-eyelid painting remain unfinished; this is not final
artist acceptance, full speech coverage or a complete character.

## Open and inspect

Open `Unity/` in Unity 6000.3.24f1, then
`Assets/CharacterArt/Generated/Preview/Scenes/RenCompleteHead.unity` and press Play
with a 16:9 Game view. Run `git lfs pull` first on a new checkout. The scene shows
the original artist reference beside painted/clay candidates.

The local Windows player is `.local/ren-complete-head-v2/RenCompleteHead.exe`.
It is a build output, not checked in. See the
[Unity build and capture instructions](../../../../../../Unity/Assets/CharacterArt/Editor/RenCompleteHead.md).

For the browser, run these commands from the repository root:

```powershell
npm --prefix tools/character_art/viewers ci --no-audit --no-fund
python tools/character_art/serve_ren_texture_viewer.py --port 8768 --head
```

Open [the local head viewer](http://127.0.0.1:8768/). It loads the actual GLB and
drives its morphs and gaze hierarchy. Keep the server running.

Actual Unity evidence:

- [Front](unity-review/live/RenCompleteHead-live--rest--front.png),
  [quarter](unity-review/live/RenCompleteHead-live--rest--quarter.png),
  [profile](unity-review/live/RenCompleteHead-live--rest--profile.png).
- [Cap off](unity-review/live/RenCompleteHead-live--cap-off--front.png),
  [combined gaze and half blink](unity-review/live/RenCompleteHead-live--partial-diagonal-blink--quarter.png).
- [Five-second motion clip](unity-review/motion/RenCompleteHead-v2-motion.mp4):
  150 actual Unity GPU-rendered frames, 1280x720, 30 output fps, no audio. It shows
  gaze, blink and one mouth gesture. It is not a phone-performance measurement
  or a demonstration of speech lip-sync.
- [Focused review](unity-review/RenCompleteHeadV2Review.json),
  [live control evidence](unity-review/live/RenCompleteHeadLiveEvidence.json),
  [motion provenance](unity-review/motion/RenCompleteHeadMotionEvidence.json).

V1 browser checks exercised the same facial controls. Final V2 browser automation
timed out while attaching to the existing tab; the V2 source is served correctly,
but fresh V2 browser screenshots are not claimed. V2 visual validation above is
from the running Unity player.

## Geometry budget and source preservation

The assembly contains **26,426 source triangles**: 13,790 head surface, 1,702 eyes,
792 teeth, 776 tongue, 7,900 hair and 1,466 cap/jewelry. Unity imports **26,422**;
its back-hair object has four fewer triangles, with the cause unresolved. All
other per-object counts match. Against the complete 40k LOD0 target, the source
assembly leaves 13,574 triangles for the remaining character and reserve. See
the [complete-character budget](../../../../../../docs/REN_POLYGON_BUDGET.md).

[assembly.json](assembly.json) records exact source and export hashes, all mesh
counts, shape names, source transforms, material/texture metadata and the gaze
contract. Inputs are frozen:

- [Low-sclera gaze repair](../gaze-repair-v1/README.md).
- [Painted face](../anime-paint-v2/README.md).
- [Selected V2 hair](../ren-shag-v1/selected-v2/README.md).
- [V2 cap with band clearance](../head-accessories-v2/band-clearance-v1/README.md).

The Blender file preserves native mesh geometry, UVs, shape coordinates and
authoring materials. Export copies put `FaceUV_v1` in UV0, remove unused vertex
color attributes and use a portable skin material carrying the baked atlas.
Original packed texture bytes are copied without recompression: 2K face, 4K hair
and 1K cap. GLB defaults to closed mouth, open eyes and `capOn=1`. FBX defaults
to zero shape weights; the Unity controller applies mouth seal and all six hair
fitting shapes at runtime.

The browser uses a 65% base-color / 35% diffuse mix for its lit review. Unity uses
the existing diffuse review shader. Neither preview implements the final
nightclub material. Metallic and roughness metadata are retained for later
material integration, but the current diffuse viewers do not reproduce metal PBR.

## Rebuild a derivative

Run `tools/character_art/assemble_ren_complete_head.py` through Blender with
`--hair` set to the selected V2 blend, `--accessories` set to the band-clearance
V2 blend, and `--output` set to a **new** directory. The default painted face is
the input above. Add `--render` for new Blender review images. Preserve these
frozen files and their evidence when creating a later revision.

## Remaining art work

Hair still has broad ribbon-like locks, abrupt root transitions and unfinished
strand painting. Tiny pale specks remain on the ear-side cap band. Full blink
stretches the eyelid paint. The face's final likeness and shading require review
against the original designer sheet. The
[Tripo hair reduction](../tripo-hair-lowpoly-v1/README.md) preserves the layered
shag better and is a stronger possible repair basis, but is not installed here.
It needs crown/internal-geometry cleanup, fitting, reduction and UVs before paint.

The six expressions, English/Japanese speech set, shared-rig body fitting, idle
animation and sustained iPhone 15 Plus performance remain separate unfinished
parts of the active Ren goal.
