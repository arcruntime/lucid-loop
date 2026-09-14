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
| Unity import | 42,170 rendered triangles, 67 bones, 316 blendshape bindings across renderers, 12 materials |
| Approved body | Derived from the approved Meshy body through female-v3 and the headphone-removal repair; original source remains immutable |
| Body reduction | Headless body and neck/chest repair total 11,998 Blender triangles; source normals and baked colors restored after constrained reduction |
| Facial controls | Expressive head with English/Japanese speech shapes, expression controls, gaze, and idle blink; 316 bindings are renderer bindings, not 316 unique expressions |
| Hair | 12 animated hair bones plus a fixed anchor; runtime secondary movement |
| Accessories | Separate toggleable baseball cap and standalone replacement headphones included in the assembly |
| Body idle | Runtime playback of the female-v3 Walt observer idle through `RenLOD0Controller`; the complete scene is playing |
| NPR shading | `RenLOD0Toon.shader`, white key light and cyan/magenta club-light controls in the complete-character scene |
| Export | `art/generated/characters/ren/lod0-final-v1/Ren_LOD0.fbx`, portable Blender assembly, textures, and material mapping |

Unity import evidence is
`Unity/Assets/CharacterArt/Generated/RenLOD0/Evidence/import.json`.
The same Evidence directory contains the complete-character capture and current
face/control captures. Body reduction, neck-ring metadata, and FBX checks are in
`art/generated/characters/ren/lod0-final-v1/body/`.

## Remaining acceptance

- Designer approval of Ren's facial silhouette, eyes, expressions, and complete
  character likeness. Assembly/export success is not likeness approval.
- Final moving-character inspection of neck seams, clothing, hands, cap/hair
  contact, and headphone clearance across controls and idle motion.
- Sustained 30 fps on the minimum-spec iPhone 15 Plus in Unity 6.3 LTS,
  landscape 16:9, including the nightclub's dynamic lighting and intended crowd.
  Phone profiling and thermal testing have not been completed.
- LOD1 and transition review remain separate from this LOD0 delivery.
- A spoken live-conversation acceptance test remains outstanding. Existing
  relay-session and analyzer checks do not establish a microphone-to-character
  conversation test.

The preserved-body comparison remains
`Unity/Assets/CharacterArt/Generated/RenBodyV3/Scenes/RenBodyV3Review.unity`.
The separate conversation face scene remains
`Unity/Assets/Gyms/Scenes/LiveGym.unity`. Earlier body/assembly studies are retained
as development history; use the RenLOD0 scene above for the complete character.
