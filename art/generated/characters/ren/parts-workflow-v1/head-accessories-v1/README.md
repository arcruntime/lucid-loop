# Ren cap and ear jewelry

Separate Blender-built accessories fitted to the corrected EyeUV head, using
the original [Ren character sheet](../../../../../characters/ren-model-sheet.png).
The head's geometry, UVs, shape coordinates and object matrices remain unchanged.
These are accessory candidates for the main look, not final character approval.

| Mesh | Rendered triangles | Toggle |
| --- | ---: | --- |
| `Ren_Cap` | 962 | Whole cap, including crown, bill, inner band, button and rear strap |
| `Ren_RightEar_HelixCuffs` | 200 | Paired silver helix cuffs |
| `Ren_RightEar_DropPiercing` | 156 | Lobe loop/link and narrow open metal drop |
| **Total** | **1,318** | Head, hair, headphones and necklace excluded |

The visible portrait ear is implemented on the character's anatomical right,
negative Y in the native head frame. The cap uses six cloth panels, a curved
bill, a small irregular embroidered mark, an offset crown peak and a shallow
rear opening/strap. Seams, stitching and fine cloth variation use a 1024² color
texture. They do not add tiny geometry. The silver pieces follow the portrait's
small cuffs and long narrow drop rather than a generic large hoop set.

## Files

- [Ren_Head_Accessories.blend](Ren_Head_Accessories.blend): the three accessory
  objects only, with packed texture and editable geometry.
- [Ren_Head_Accessories.fbx](Ren_Head_Accessories.fbx) and
  [Ren_Head_Accessories.glb](Ren_Head_Accessories.glb): accessories only; no head,
  hair, body, animation or additional skeleton.
- [Ren_Head_Accessories_Study.blend](Ren_Head_Accessories_Study.blend): current
  corrected head and hair included for fit review. This is not the import file
  for the whole character.
- [Ren_Cap_Cloth_BaseColor.png](Ren_Cap_Cloth_BaseColor.png): standard sRGB color
  texture, also embedded in GLB/FBX and packed in Blender.
- [fitting-accessories.json](fitting-accessories.json): object selections and
  `Head` attachment intent for later common character alignment/binding.

All positions remain in the source head's **native units, +X front and +Z up**.
Do not normalize the accessories independently of the character. FBX and GLB
use their standard format axis conversion; Blender reimport restores the same
source placement. The current files contain no skin weights or armature.

## Material handoff

These use Principled materials with ordinary color/metallic/roughness inputs.
No Blender procedural shader, alpha hair card, geometry node or custom render
effect is required for their appearance. The sole texture is the cap color map.

| Material | Metallic | Roughness | Equivalent Unity smoothness |
| --- | ---: | ---: | ---: |
| `Ren_Cap_BlackCloth` | 0 | 0.91 | 0.09 |
| `Ren_Cap_Underside` | 0 | 0.94 | 0.06 |
| `Ren_Piercing_BrushedSilver` | 0.86 | 0.29 | 0.71 |

The crown material is two-sided because its small rear opening can expose the
inner cloth face. The underside and metal use back-face culling. The cap's two
material slots and two jewelry meshes produce four material primitives across
three renderers. GLB records the cloth's reduced specular strength with
`KHR_materials_specular`; an eventual URP material needs its own cloth specular
setting. FBX import alone does not establish final URP material equivalence.

## Verification and review

[construction.json](construction.json) records the source/reference hashes,
head preservation checks, exact part bounds/counts and contextual hair version.
[export-verification.json](export-verification.json) records actual exported
hashes and FBX/GLB reimport results. Reimports preserve all three mesh names,
triangle counts, material slots, UV presence and native vertex placement. There
are no degenerate accessory triangles. These checks do not certify Unity
shader output, head skinning or device performance.

[visual-review.json](visual-review.json) records the exact inspected image and
asset hashes, final source hash rechecks, observations and remaining limitations.

The [quarter cap fit](cap-fit-quarter.png) and [rear cap fit](cap-fit-rear.png)
show the cap against the current head with hair temporarily hidden. The broad
draft rose lip material is replaced by the existing skin material **for context
captures only**; the original source material slots are restored in the saved
study. The accessory-only files contain no facial material.

The [front](combined-front.png), [quarter](combined-quarter.png) and
[profile](combined-profile.png) captures now use the hair worker's
`ren-shag-v1/cap-on-v1/Ren_Shag_CapState.blend`, with `capOn=1`. The broad
cap-off root intersections are gone. The [context jewelry detail](jewelry-detail.png)
and [hair-hidden jewelry fit](jewelry-fit-detail.png) make the current coverage
and attachment visible. The thick side lock still obscures much of the upper
cuffs, and a fuller swept fringe is being revised by the hair worker.

These images are a **coordinated cap fit proof**. They do not approve the
current sparse fringe or final character likeness. The cap remains fixed for
that next hair revision. Artist likeness, final tuft/fringe clearance, close-up
cuff attachment and runtime material tuning remain subject to combined review.

## Reproduction

Run the dedicated helper in background Blender with at most two threads:

```text
blender --background --threads 2 --factory-startup --offline-mode
  --python-exit-code 1 --python tools/character_art/build_ren_head_accessories.py
  -- --build --export --hair <selected Ren_Shag.blend>
```

`--inspect` inventories the immutable head, `--skip-renders` skips captures,
`--export` exports/checks the saved study, and `--preview-cap` renders the
quarter/rear fit with hair hidden. All outputs stay in this directory. The
helper never edits source head/hair files, the shared skeleton, or Unity.

No paid generation call, staging, commit or push was performed for this task.
