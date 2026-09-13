# Ren head references and provider comparison

Requested 2026-09-13. This is the current reference-generation and bust-comparison
task; it precedes further facial rig work in the broader Ren implementation goal.

## Artist authority and framing

Use [the original artist sheet](../art/characters/ren-model-sheet.png) as the
identity and illustration reference. Its explicit cap-off alternate establishes
the exposed hair. The old generated full-body reference, rejected fitted head,
and additional `Ren_model.glb` are not likeness authorities for these sheets.

Show the full hair/head and a short neck with no shoulders, torso, hands,
headphones, or necklace. Keep visible silver ear hardware. Removing the cap
exposes the brows, eyelids, ears, and hair for modeling. Use a plain pale
background and consistent scale and lighting across front, three-quarter, and
true-profile views. Turn in the same direction on every sheet.

Preserve the long half-lidded grey-blue eyes, tapered heavy upper lashes,
compact nose, tapered chin, short tousled pale-blond bob, and graphic rose lips.
Ren's lips have visible volume and a localized painted highlight. Match the
artist's drawing rather than introducing a realistic face or round doll eyes.

## Sheet coverage

One large three-view sheet per state; do not compress the full set into a small
atlas. Exact artist labels and observations come from the
[expression catalog](../art/characters/expression-catalog.json).

| State | Facial direction |
| --- | --- |
| CLOSED REST | Additional construction basis: relaxed eyes and brows, lips meeting at a closed seam |
| NEUTRAL | Artist expression: relaxed half lids, subtly parted lips |
| AMUSED | Asymmetric narrowed lids and a restrained lifted mouth corner |
| SKEPTICAL | Side gaze, subtly asymmetric brows, uneven parted lips |
| FOCUSED | Inward brow tension, intent upward gaze, firm closed mouth |
| ALERT | Modestly widened attentive eyes and slightly raised brows |
| GUARDED | Narrowed restrained eyes; expose the mouth for modeling rather than reproducing the covering hand |
| A | Open speaking mouth with coherent interior |
| I | Wide shallow speaking opening |
| U | Small compressed opening, avoiding exaggerated lip projection |
| E | Wider opening, more vertical separation than I |
| O | Rounded opening, visibly distinct from U |
| BLINK | Both eyelids fully closed, coordinated lash line; resting brows and mouth |

The user listed A/I/U/O/U; the repeated U is interpreted as the standard
A/I/U/E/O set. These are visual reference poses, not a claim of complete English
and Japanese speech-control coverage. Keep vowel poses emotionally neutral.
Unseen views, speech poses, and GUARDED's uncovered mouth are extrapolations
from the artist's design, not new drawings by the original artist.

## Outputs and review

The image sub-agent owns
`art/generated/characters/ren/head-reference-sheets-v1/`, including images,
prompts, provenance, coverage, and review notes. Use built-in imagegen and save
its original outputs into the repository workspace. Record actual pixel sizes;
a requested 4K prompt does not establish a 4K output.

Parent reviews the first closed-rest three-view sheet for likeness before the
remaining expressions are generated. Subsequent sheets retain the original
artist reference and that construction basis. Inspect each output for identity,
view consistency, expression visibility, shoulder exclusion, and mouth/eye
artifacts. Preserve earlier versions when correcting a generated image.

After the sheets, provide separate unlabeled high-detail FRONT and PROFILE
images for CLOSED REST and A, so geometry generation can consume one large
head per image. Use the corresponding sheet and original artist source to
retain identity and pose; do not feed a labeled three-head collage into a
single-view geometry endpoint.

## Tripo and Meshy comparison

The user explicitly authorizes paid generation with both services and prioritizes
their newest highest-quality models over cost. Verify each current official API
before selecting models and settings. Resolve credentials through local BWS
without exposing their values in commands or artifacts.

Prepare generation while sheets finish; begin paid model jobs only after the
reference set is complete and the shared inputs have been reviewed. Use matched
FRONT and PROFILE inputs for the primary cross-provider comparison. Generate
closed-rest and open-mouth A candidates separately; combining expressions in one
reconstruction would make the intended surface ambiguous.

Preserve each provider's highest-detail original and texture outputs. Record
input hashes, exact requested and returned model identity where available,
settings, task identifiers, texture dimensions, and file hashes. Keep maximum
fidelity generation separate from later mobile optimization. Newest topology
models and highest-detail models can be different product lines.

Create an actual Unity comparison scene with synchronized orbit/zoom, matched
front/three-quarter/profile framing, source references, and material/lighting
controls. Inspect likeness, eye construction, lip contour and mouth interior,
hair silhouette, texture detail and UV defects, and suitability for deformation.
Recommend a candidate from observed results. Desktop viewing does not establish
the project's sustained 30 fps iPhone 15 Plus target.

Provider source assets and review evidence belong under
`art/generated/characters/ren/bust-comparison-v1/`. Keep the old Ren assets intact
as historical comparisons until a new candidate has been reviewed.
