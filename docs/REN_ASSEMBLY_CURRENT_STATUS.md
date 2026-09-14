# Ren assembly status — 2026-09-14

The complete Ren LOD0 is assembled, imported, and playing in Unity. Open
`Assets/CharacterArt/Generated/RenLOD0/Scenes/RenLOD0.unity` inside the `Unity`
project and enter Play mode. Repository path:
`Unity/Assets/CharacterArt/Generated/RenLOD0/Scenes/RenLOD0.unity`.

This is the current complete-character delivery. Designer likeness approval and
sustained 30 fps on iPhone 15 Plus remain pending; a working Editor scene does
not establish either acceptance criterion.

## Current assembly

| Item | Current evidence |
|---|---|
| Unity import | 42,170 rendered triangles, 67 bones, 324 blendshape bindings across renderers after gaze installation, 12 materials |
| Approved body | Derived from the approved Meshy body through female-v3 and the headphone-removal repair; original source remains immutable |
| Body reduction | Headless body and neck/chest repair total 11,998 Blender triangles; source normals and baked colors restored after constrained reduction |
| Facial controls | Retained speech shapes, expressions, idle blink and eight independent per-eye gaze controls; bindings count renderer targets, not unique expressions. Gaze uses the same final writer and fades quadratically with each blink |
| Hair | 12 animated hair bones plus a fixed anchor; runtime secondary movement |
| Accessories | Separate toggleable baseball cap and standalone replacement headphones included in the assembly |
| Body idle | Runtime playback of the female-v3 Walt observer idle through `RenLOD0Controller`; the complete scene is playing |
| NPR shading | `RenLOD0Toon.shader`, white key light and cyan/magenta club-light controls in the complete-character scene |
| Export | `art/generated/characters/ren/lod0-final-v1/Ren_LOD0.fbx`, portable Blender assembly, textures, and material mapping |
| Neck motion | Final Blender assembly: 293 paired endpoints stayed coincident in 56 head/expression combinations; head vertices moved up to 0.1215 m, confirming actual deformation |
| Main encounter | Complete prefab installed in `Assets/Gyms/Scenes/BeforeTheDrop.unity`; consumed-PCM speech binding integrated by lead in `cf06605`, with 91 passing encounter EditMode tests including five Ren binding tests |

Unity import evidence is
`Unity/Assets/CharacterArt/Generated/RenLOD0/Evidence/import.json`.
The same Evidence directory contains the complete-character capture and current
face/control captures. Body reduction, neck-ring metadata, and FBX checks are in
`art/generated/characters/ren/lod0-final-v1/body/`.
`art/generated/characters/ren/lod0-final-v1/neck-motion-validation.json` records
the motion cases and source hash. Reproduce with Blender background execution of
`tools/character_art/verify_ren_lod0_neck_motion.py`. This checks geometric endpoint
continuity, not shading, accessory contact or artistic acceptance.

## Remaining acceptance

- Designer approval of Ren's facial silhouette, eyes, expressions, and complete
  character likeness. Assembly/export success is not likeness approval.
- The designer's GUARDED hand-to-mouth acting is being finished. The delivered
  GUARDED facial shape is not yet the complete pose.
- Final moving-character inspection of neck seams, clothing, hands, cap/hair
  contact, and headphone clearance across controls and idle motion.
- Sustained 30 fps on the minimum-spec iPhone 15 Plus in Unity 6.3 LTS,
  landscape 16:9, including the nightclub's dynamic lighting and intended crowd.
  Phone profiling and thermal testing have not been completed.
- Corrected LOD1 is saved under `art/generated/characters/ren/lod1-final-v1`:
  13,418 Blender triangles / 13,404 after FBX reimport. Source hair/neck boundary
  positions, facial shape names and rig hierarchy are retained. It remains over
  the 10–12k budget; Unity import, deformation and transition review are pending.
- A spoken live-conversation acceptance test remains outstanding. Existing
  relay-session and analyzer checks do not establish a microphone-to-character
  conversation test.

The preserved-body comparison remains
`Unity/Assets/CharacterArt/Generated/RenBodyV3/Scenes/RenBodyV3Review.unity`.
The conversation scene `Unity/Assets/Gyms/Scenes/LiveGym.unity` now uses the complete
Ren prefab. Its speech adapter forwards mouth weights through the same controller
that owns expressions and blinking. Actual Play-frame checks passed for A,
bilabial closure, blink preservation and speech reset; a real spoken conversation
is still pending. Earlier body/assembly studies are retained
as development history; use the RenLOD0 scene above for the complete character.

## Gaze integration

The prefab, viewer, LiveGym and main encounter use `RenEyesGaze.asset`, cloned
from the original Unity eye mesh. Its 1,124 ordered vertices, triangles and UVs
match the donor exactly; existing skin bindings and 35 old eye targets are
retained. Only the eight per-eye targets are added. The construction source is
`art/generated/characters/ren/gaze-final-v1/`; rebuild the mesh override with
**Lucid Loop → Ren LOD0 → Install gaze shapes** outside Play mode.

`RenLOD0Controller.SetGaze(left, right)` accepts each eye's normalized horizontal
and vertical input; the mixer cancels opposing directions, limits diagonal
travel, and suppresses gaze during closure. This is subtle surface displacement
of the existing painted eyes, with 2.5 mm horizontal / 1.8 mm vertical limits.
It is not unrestricted eyeball rotation. The face sliders expose each target.

Seven gaze/speech tests passed. Actual Play frames verified left/right travel
and full-blink suppression while an A mouth remained active. Evidence is in
`Generated/RenLOD0/Evidence/gaze-*`. The Unity captures show closed lids without
exposed irises; they do not establish designer approval or complete articulation
quality. LOD1 has not yet received this eye override.
