# Ren head reference sheets v1

Generated on 2026-09-13 with the built-in image generation tool. The selected set contains **13 three-view sheets** and **four separate single-view provider inputs**. Each selected image has been visually reviewed for identity, framing and expression readability. The original artist has not reviewed these generated extrapolations.

The [original Ren artist sheet](../../../../characters/ren-model-sheet.png) remains the identity authority. Its labeled cap-off alternate establishes the exposed short pale-blond hair. The character keeps the artist's long grey-blue almond eyes, heavy tapered lashes, compact nose, tapered chin, graphic rose lips and silver ear hardware. These are head-and-neck references: cap, shoulders, hands, headphones and necklace are omitted.

## Selected sheets

Every sheet shows front, approximately 45-degree three-quarter, and strict right-facing profile at a consistent scale and neutral lighting. Expressions are carried through the face instead of tilting the head. Actual output is **1672 × 941 px**, except AMUSED at **1672 × 940 px**.

| State | Selected image | Modeling cue |
| --- | --- | --- |
| Closed rest | [00-closed-rest-v2.png](00-closed-rest-v2.png) | Closed lip seam; relaxed eyes and brows |
| Artist NEUTRAL | [01-neutral.png](01-neutral.png) | Slightly parted lips; relaxed half lids |
| AMUSED | [02-amused-v2.png](02-amused-v2.png) | Asymmetric narrowed lid and restrained raised mouth corner |
| SKEPTICAL | [03-skeptical.png](03-skeptical.png) | Uneven brows and parted lip line |
| FOCUSED | [04-focused.png](04-focused.png) | Inward brow tension; firm closed mouth |
| ALERT | [05-alert.png](05-alert.png) | More open attentive eyes; modest brow lift |
| GUARDED | [06-guarded.png](06-guarded.png) | Restrained narrowed eyes and closed mouth; uncovered mouth is inferred |
| Full blink | [07-blink.png](07-blink.png) | Both lids fully closed; resting brows and mouth |
| A / あ | [08-vowel-a.png](08-vowel-a.png) | Natural open jaw, visible mouth cavity and low tongue |
| I / い | [09-vowel-i.png](09-vowel-i.png) | Wide shallow opening |
| U / う | [10-vowel-u-v2.png](10-vowel-u-v2.png) | Small compressed opening; minimal lip projection |
| E / え | [11-vowel-e.png](11-vowel-e.png) | Wide opening with more vertical separation than I |
| O / お | [12-vowel-o.png](12-vowel-o.png) | Rounded opening, larger and taller than U |

The user listed A/I/U/O/U; the repeated U is interpreted as the standard A/I/U/E/O set. These reference poses are not a complete English/Japanese speech blendshape inventory.

## Shared inputs for Tripo and Meshy

These are separately generated, unlabeled single-head portraits, each **1122 × 1402 px**. They give the face a larger share of the image than a crop of the three-view sheets. Use matching expressions together; do not combine a closed front with an open profile.

| Candidate | Front input | Right-facing profile input |
| --- | --- | --- |
| Closed rest | [closed-rest-front-v2.png](generation-inputs/closed-rest-front-v2.png) | [closed-rest-profile.png](generation-inputs/closed-rest-profile.png) |
| Open mouth A | [vowel-a-front.png](generation-inputs/vowel-a-front.png) | [vowel-a-profile.png](generation-inputs/vowel-a-profile.png) |

## Review and provenance

The parent agent reviewed the first closed-rest sheet and accepted its likeness, style and framing as the basis for this experiment. Four targeted corrections are preserved alongside their first drafts:

- Closed rest v2 removes a tiny tooth-like gap in the three-quarter mouth.
- AMUSED v2 aligns the front raised mouth corner with the anatomical side shown in the other views.
- U v2 makes its small compressed aperture visibly open.
- Closed-front input v2 adds enough background clearance to keep the full hair and flyaways inside the canvas.

Use the selected filenames above. The unversioned files replaced by v2 remain as generation history.

[manifest.json](manifest.json) records all 21 generation outputs, the 17 selected deliverables, exact prompts, input image paths, original tool output paths, actual dimensions, SHA-256 hashes, and visual review notes. [prompts/](prompts/) contains the complete prompt set. All workspace PNGs were verified byte-identical to their tool outputs. No programmatic image cropping, compositing, retouching or upscaling was used. Requested resolutions in the prompts are not delivered-resolution claims.

Unseen angles, speech poses and blink are extrapolations. GUARDED's source mouth is partly hidden by a hand; the unobstructed closed mouth here is a conservative interpretation. Fine hair strands and small ear hardware vary slightly between independently generated portraits. The images guide construction but are not mathematically calibrated projections of one 3D head.

No meshes, blendshapes, rigs or Unity assets are included in this image set. The provider comparison and Unity review are tracked separately in [the task brief](../../../../../docs/REN_HEAD_SHEET_BRIEF.md).
