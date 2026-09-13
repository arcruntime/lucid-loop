# Ren separate-parts construction

Started 2026-09-13 after the reference/provider comparison. The reviewed method
and facial design are in [the pipeline research](../../../../../research/japanese-anime-character-workflows-2026-09.md)
and [Ren's implementation goal](../../../../../docs/REN_CHARACTER_GOAL.md).

## Current state

- Original artist design: [Ren sheet](../../../../characters/ren-model-sheet.png).
- Construction comparison: [three imported busts](../bust-comparison-v1/README.md).
  Tripo H closed rest is the provisional source reference, pending the new parts.
- The [cleaned hair-free rest sheet](../parts-reference-v1/head-closed-rest-three-view-v2.png)
  and [separate hair sheet](../parts-reference-v1/hair/hair-three-view-v2.png)
  have passed the initial construction-reference review. The original artist
  remains the likeness authority. The reviewed
  [standalone open-mouth inputs](../parts-reference-v1/HEAD_REFERENCES.md) were
  used for the head generation.
- The user funded the API. A read-only check reported 2,600 available credits,
  separate from the Studio balance. Both separate P2 tasks completed for
  **100 credits each, 200 total**, with native FBX files preserved. The
  [head receipt](../parts-generation-v1/tripo/head/receipt.json) and
  [hair receipt](../parts-generation-v1/tripo/hair/receipt.json) record actual
  charges; their neighboring provenance files bind the exact reviewed inputs.
- Both Tripo jobs requested explicit P2 native quads, geometry only:
  head target 10,000 faces and separate hair target 25,000 faces. Counts are requests;
  returned topology, UVs, and surface quality must be inspected. The API documents
  `export_uv` independently; its presence in a request does not establish usable UVs.

The rest-sheet cleanup removed faint hair-shaped marks across the brows and nose
bridge. The second hair sheet consolidates dense, fine strands into broader locks
while retaining Ren's asymmetric shag. A few outer wisps remain; the uncapped rear
is an extrapolation. These are reviewed construction inputs, not approved final
character models.

The [original Tripo H baseline audit](audits/tripo-h31-closed-baseline/VISUAL_REVIEW.md)
found one position-connected surface spanning the head, hair, eye regions, and
jewelry. Its 1,120 imported components match 1,120 UV islands, so they should not
be treated as ready-made detachable facial parts. Clay renders also show that iris
appearance comes from the texture, without separately addressable iris objects.

The new [P2 head audit](audits/p2-head-native/MOUTH_REVIEW.md) found 9,005 quads
and 1,142 triangles, including four closed 44-edge outer lip loops and a real
mouth opening. The interior remains unfinished. The raised brow masses and filled
eye surfaces also need correction before likeness acceptance. The
[P2 hair audit](audits/p2-hair-native/VISUAL_REVIEW.md) found 22,820 quads and
5,414 triangles, with visible crossbars, thin wisps and inward sheets requiring
cleanup. Native quads alone do not make either source ready to animate.

A [rigid placement study](assembly/README.md) positions the separate sources
without modifying their vertices or UVs. It is an untextured construction review,
not the neutral-character milestone. The
[Unity parts study](../../../../../Unity/Assets/CharacterArt/Generated/Preview/Scenes/RenPartsStudy.unity)
now contains those exact source files and the reviewed placement, with
[actual Unity captures](unity-review/README.md). Open the scene, press Play, and
select 16:9 in the Game view; the original artist reference is visible by default.

The [texture projection helper](texture-projection/README.md) passed isolated
Blender 5.1.1 and 5.2.1 fixtures for UV mapping, occlusion, and unchanged source
geometry. It has not painted Ren. Her facial UVs and geometry still need review
before the reference-image projection step.

## Local repair checkpoint

The [face study](../../../../../Unity/Assets/CharacterArt/Generated/Preview/Scenes/RenFaceStudy.unity)
is launchable in Unity. V2 combines a [localized brow correction](brows-v1/README.md),
stitched eyelids, separate grey-blue eyes and an oral reconstruction with live
seal/open-A controls. [Live player captures](face-integration-v2/unity-review/live/)
verify actual mouth and blink rendering. Intermediate gaze still exposes an
iris/sclera intersection and remains unaccepted. Fitted V5 hair is an optional
construction preview; its 45,536 triangles exceed the new complete-character budget.

The [final local mouth revision](mouth-v1/README.md) uses a 55-edge native aperture,
removes a folded commissure web, and adds a clean cavity, dental arches and tongue.
The [new face UV layout](face-uv-v1/README.md) has one continuous front island with
small peripheral/corner pieces; [mouth UV integration v3](face-uv-v1/oral-integration-v3/README.md)
maps the new cavity without overlapping the surviving native layout.

The [Tripo texture trial](tripo-texture-v1/README.md) completed for 30 credits.
The viewer now defaults to clay left/static Tripo right, with the original artist
visible. The returned head has 8K maps but reads too realistically and has flat
grey eyes. The [browser viewer](../../../../../README.md#browser-texture-viewer)
also opens that exact provider GLB. Both preserve the working head separately.
Its temporary flat rose lip region is too broad to serve as finished painting;
the first image painting did not pass direct projection alignment.

Next comes a complete visible Ren head with refined hair, cap, jewelry and anime
painting. The user targets approximately 40,000 rendered triangles for all of Ren's
LOD0, including clothing/accessories, with detail prioritized on the face. Full
neutral likeness, English/Japanese speech, emotions, body integration and phone
performance remain required by the goal.

## First visible milestone

Produce a recognizable neutral Ren head in Unity with separately fitted hair,
eye/lid/lash assemblies, and an oral region that can deform. Keep all vendor
originals immutable. Compare front, three-quarter, and profile to the artist's
design before expanding the expression set.

The first implementation exercise then combines:

1. One smooth partial/full blink and independent gaze.
2. One swappable eye assembly preserving a distinct graphic expression.
3. One continuous speaking mouth pose and a genuine lip seal.
4. Mouth movement continuing through the eye-state transition.

This is a construction milestone, not full bilingual coverage. The finished rig
must satisfy the [English/Japanese articulation matrix](../../../../../docs/BILINGUAL_SPEECH_RIG.md).

## Geometry and texture sequence

Review the hair-free reference first, then prepare full-resolution per-view
generation inputs and separate hair references. Use an open-mouth construction
reference when an oral opening is needed; any change from the rest pose must keep
Ren's neutral contours recoverable. A separately generated mouth pose may guide
fitting, while the deforming mouth retains its own stable vertex identities.

Preserve native P2 FBX quad geometry and make clearly labeled triangulated viewing
copies as needed. Audit connected parts, lid/iris separation, facial loops, and UVs.
Fix the accepted geometry and unwrap before using the HOS reference-projection
texture experiment. Texture images must be reviewed both unlit and under moving
colored lights in Unity.

The eye system may switch between different meshes. Compatible blink shapes stay
with their corresponding eye assembly; gaze remains independent. Mouth expression
and speech use continuous blendshapes. Unity-chan supplies construction guidance,
while Ren's source drawing supplies the proportions and visible contours.

## Evidence

Store source audits under `audits/`, preserving input hashes, actual topology and
UV measurements, and rendered views. Record real generation task IDs and receipts
with the resulting source files. Update this checkpoint as each visible milestone
is demonstrated; pending assets must not be described as completed controls.
