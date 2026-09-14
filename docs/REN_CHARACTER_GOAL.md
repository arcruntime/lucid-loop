# Ren character implementation goal

See [current H construction status](REN_H_CURRENT_WORK.md) for the latest frozen
source handoffs, unresolved work and the scene that can actually be opened.

## Current acceptance and delivery requirements

The latest user feedback says the art delivered so far is not good enough.
Slight Arcane influence is permissible in paint and lighting; the designer's
American-anime eye silhouettes and Marvel Tōkon-style NPRS remain the visual
requirements. No current technical checkpoint constitutes likeness approval.
Review the actual Unity face beside the artist's drawing at matched framing,
including lid contours, iris exposure, lash masses and smoky eye shading.
Preserve the accepted H cheeks, jaw, nose and mouth while correcting the eyes.

The lead relayed two further user instructions on the shared project bus:
Japanese voice work is deferred for this release, and project work belongs in
`B:\lucid-loop`. Continue English articulation; preserve Japanese experiments
as deferred evidence. Migrate the working mouth review and its dependencies into
`B:\lucid-loop\Unity`, preserving GUIDs and unrelated main-project changes.
External scratch scenes are historical work pending verified migration, not
the intended delivery location. Older bilingual requirements below describe
the broader pipeline and do not authorize resumed Japanese-specific work.

The user's latest triangle targets, relayed by the lead and recorded in
[iOS rendering requirements](IOS_RENDERING.md), supersede the historical 40k
allocation below: **50–80k triangles for the complete Ren**, with approximately
20–25k for head/face, 15–30k for hair and 15–25k for visible body/clothing.
Choose component allocations whose sum stays within the overall target; account
for eyes, oral parts, accessories and rendered outline shells explicitly.
Keep the whole frame within approximately **250–300k triangles**. These are
budgets, not evidence of sustained 30fps on iPhone 15 Plus.

## Next goal: a recognizable, complete Ren head for visual review

Produce one head-and-neck Ren asset with her pale-blond asymmetric shag, main-look
black cap, designer-faithful anime eyes and rose lips, and coherent painted skin.
Show it beside the original artist sheet in the browser and Unity, with working
independent blinks, clean gaze, closed rest and the existing open-A mouth prototype.
This is the immediate milestone within the broader Ren implementation below.

### Reference animation requested on 2026-09-14

The user approved the exact H source's overall face shape after the matched
comparison. Build the corrected head from that surface, restore detailed hair,
and apply the painterly NPR treatment before judging the animated result.
P2 remains a historical construction scaffold, not the neutral shape authority.

The attempted P2 surface transfer failed around the mouth, nostrils and eye
surroundings. The small native H mouth patch preserved those central features,
but its visible junction with P2 introduced gaps and pinching. Both trials are
rejected. Continue with a coherent H-native visible face and neck surface,
retaining original H triangle/UV provenance and placing extraction boundaries
under hair or behind the ears. Reconstruct local eye and oral interfaces around
that surface. P2 may provide hidden support only where it cannot alter the
approved face or hair silhouette. Dense intermediate geometry is acceptable for
this review; it does not satisfy the final 40k whole-character budget.

Use `B:\lucid-loop\DanielDuguay87_2093375826557296673.mp4` as the
performance reference for a control-driven test animation. Preserve the supplied
15.093-second clip unchanged (SHA-256
`d479c1eeb5eab7a15fa0c489766ef1e22983289d33a5ef51ab32398dc73ea2f1`).
Record observed head motion, gaze, blinks and mouth/expression changes against
actual source timestamps. Distinguish manually interpreted acting from measured
motion and audio analysis. Do not describe audio energy as phoneme recognition.

Deliver an actual Unity scene with replay, pause and scrubbing, showing the
reference alongside Ren in a landscape 16:9 layout, plus a captured test clip.
Revalidate H's neutral shape, mouth closure, blinks and gaze after fitting;
transferred P2 controls do not inherit their earlier validation. Clearly report
reference hand/body gestures that the bust cannot reproduce, missing facial
targets, and any remaining visual artifacts. This reference test does not by
itself establish complete English/Japanese speech coverage or phone performance.

### Latest visual correction: hair detail, jaw and NPR shading

The user rejected the V2 hair's loss of detail, found the jaw too pointed, and
identified that the intended NPR shading is still missing. These are the next
acceptance requirements; the technical control checks do not resolve them.

1. Recover the native layered shag, narrow tapered locks and irregular gaps.
   Compare a locally repaired Tripo result to the dense source. Do not force the
   provisional 8k hair allocation by flattening or replacing those features with
   broad ribbons. A roughly 9–10k hair candidate can be reviewed while the full
   dressed-character budget remains approximately 40k triangles.
2. Preserve the full facial shape of the user's selected
   [Tripo Studio H A-open result](https://studio.tripo3d.ai/workspace/generate/4dd5c828-eed8-4ded-9aa2-b2c3ef66c6be),
   including cheek volume, cheekbone placement, jaw taper and chin. The user
   explicitly identified both cheeks and jaw, not merely a pointed chin. This
   asset is distinct from the current P2 head and the downloaded H closed-rest
   bust. Its local provenance is `bust-comparison-v1/tripo/studio-h3.1-a-open/`;
   original export was recovered on 2026-09-14 JST, SHA-256
   `9158e7e90ee22bce64154e2c2fe6d8880e9ab66de6f1a6c437c816f077049406`.
   Preserve that exact source, inspect matched front/quarter/profile views, and fit facial
   topology to its selected surface. Do not substitute speculative chin rounding
   or assume an existing head is the same source. Current lower-jaw vertices
   match the P2 original within floating-point precision; the mismatch was
   inherited from that source, which does not make it the correct Ren shape.
3. Implement an explicit painterly NPR material study in Unity: artist-controlled
   light/shadow colors and transitions, stable facial lighting, restrained broad
   highlights, and readable response to moving nightclub lights. The existing
   diffuse shader and browser Lambert mix are diagnostic previews, not the
   intended style. Compare old/new shading on identical geometry and textures
   first; identify baked paint that also needs correction. Preserve the anime
   designer's identity rather than equating NPR with generic thick-outline cel
   shading or photorealistic skin.

Show the separated shape and shading comparisons before combining the selected
corrections into another complete-head revision. Keep V2 and its review evidence.

The assembled working review now has a separate `RenCompleteHead.unity` scene
and a browser viewer at port 8768, with [explicit launch instructions](../README.md#launch-the-ren-complete-head-review).
It retains the facial surface and adds painted skin, reduced eye shells, cap,
ear jewelry and local low-poly hair. Continuous iris rotation and independent
blink/mouth controls have been exercised in actual browser and Unity rendering.
Hair remains too schematic and full-blink paint stretches; these are open art
issues, so this checkpoint does not complete the visual milestone.

A [30-credit Tripo hair-reduction trial](../art/generated/characters/ren/parts-workflow-v1/tripo-hair-lowpoly-v1/README.md)
returned 9,026 triangles for an 8,000 target. It preserves the layered source shag
better than the local reconstruction, but requires crown/internal-geometry cleanup,
head/cap fitting and UVs. Use that evidence to guide the next hair pass. Preserve
the old working mesh as control evidence while adapting the facial construction
to the user's selected H cheek/jaw surface.

The user now targets approximately **40,000 rendered triangles for Ren's complete
LOD0**, including face, eyes, mouth interior, hair, body, all clothing and accessories.
This uses Unity's triangle count as the working interpretation of "polygons";
quad authoring faces are counted as two triangles after triangulation. Preserve
face detail first because close conversation views concentrate attention there.
The [polygon budget and reduction study](REN_POLYGON_BUDGET.md) allocates 16,800
triangles to the head and facial assemblies, 8,000 to hair, and the remainder to
the dressed body and accessories, with a 1,400-triangle integration reserve.
The current 13,790-triangle head is a topology/budget reference, not authority for
neutral shape after the user's H selection. Preserve detail around eyes and lips
while fitting construction to the selected surface. Tripo retopology is an option
for isolated static parts, subject to visual review, rather than a verified way
to preserve facial blendshapes.

The Tripo texture trial completed for 30 credits and is visible in the browser.
The Unity comparison is also built: clay on the left, static Tripo on the right.
It supplies real 8K head maps, but the makeup/lips read too realistically and the
eyes remain unsuitable. Small returned geometry drift and missing UVs on several
parts mean the output is a static comparison/possible texture donor. It must not
replace the working deformable head. See the
[actual result review](../art/generated/characters/ren/parts-workflow-v1/tripo-texture-v1/review/README.md).

### Work in order

1. Preserve and commit the current source, paid result, viewers and review evidence
   with explicit launch instructions. Keep unrelated work out of the checkpoint.
2. Assemble the whole visible identity before judging a finished face: refine the
   fitted hair's broad bangs and regular clumps into Ren's asymmetric shag, add
   her black cap and visible ear jewelry, and provide a cap-off toggle for inspection.
   Compare front, three-quarter and profile to the original artist sheet. Correct
   demonstrated shape mismatches locally and record them; preserve vendor originals.
3. Finish the facial painting on the working head's stable UVs. Use the Tripo result
   only where its paint is useful and can be transferred without changing geometry.
   Match the artist's brow shape, half-lidded grey-blue eyes, tapered eyeliner,
   compact nose and muted rose lip contour. Remove photorealistic makeup detail,
   misplaced highlights and seams; use graphic painted planes under scene lighting.
   Keep separate authored eye parts with explicit UVs and readable iris detail.
4. Fix the known intermediate-gaze iris/sclera intersection. Verify partial and
   complete independent blinks, gaze through its full travel, and closed/open-A
   mouth composition on the painted head. Retain stable facial topology and the
   shared-female-rig compatibility needed by later work.
5. Deliver matching browser and Unity views with the original design visible,
   neutral/base-color/nightclub lighting, and close-up plus intended game framing.
   Save actual front/quarter/profile screenshots and a short blink/gaze/mouth clip.
   Commit and push the reviewable milestone with source and launch instructions.

### Completion boundary

The deliverable is one complete, recognizable Ren head ready for the user's
visual review, with coherent paint/hair/cap and clean basic facial movement.
An 8K map, a successful export or a blendshape count alone does not satisfy it.
Record remaining likeness differences explicitly; do not claim exact artist
approval or phone performance from desktop screenshots.

Full six-expression acting, complete English/Japanese speech shapes, body fitting,
Walt idle integration and iPhone 15 Plus performance qualification follow this
visual milestone. They remain part of the broader Ren goal, not completed work.

## Separate-parts implementation context

Updated 2026-09-13. The user authorized committing and pushing the entire bust
comparison, with explicit launch instructions, then starting the new Ren workflow.
Thirteen selected head sheets and three imported Tripo/Meshy busts are available
in the Unity comparison. Tripo H closed rest is the provisional construction
reference from the inspected whole-bust results. Three older Studio H/P2 busts
still await export. The newly funded API has now produced separate P2 head and
hair FBXs for 200 credits total; these are distinct from those Studio jobs.
The artist's original Ren design remains authoritative; the earlier Meshy surface
remains an immutable shape comparison. This is not final likeness approval.

The [separate-parts checkpoint](../art/generated/characters/ren/parts-workflow-v1/README.md)
records the new reference sheets, native source audits, and rigid assembly study.
The head's local repairs now include a reduced brow ridge, separate grey-blue
eyes, and an attached oral cavity with teeth, tongue, seal and open-A prototypes.
The latest mouth revision removes a folded native commissure web and uses a
55-edge aperture. A dedicated facial UV layout and cavity mapping pass overlap
checks. The combined V2 face study includes stitched eyelids, independent blink
controls and optional fitted hair. Live mouth/blink rendering is verified, but
intermediate gaze still exposes iris/sclera intersections. Its broad temporary
lip coloring is not accepted final painting. The first painted view remains
unaligned for direct projection; the separate Tripo texture candidate is now
available in browser and Unity comparisons. Neither is final likeness approval.

The [face study launch instructions](../README.md#launch-the-ren-face-study),
[mouth reconstruction](../art/generated/characters/ren/parts-workflow-v1/mouth-v1/README.md),
and [facial UV study](../art/generated/characters/ren/parts-workflow-v1/face-uv-v1/README.md)
record current evidence. Real Walt/Kimodo idle and GUARDED takes have also been
retargeted onto the unchanged 54-bone shared female skeleton; body fit, hand-to-mouth
contact and character-level animation acceptance remain required. Walt integration
is separate staging work and is not claimed by the face/texture checkpoint.

Apply the [reviewed creator workflow](../research/japanese-anime-character-workflows-2026-09.md):

1. Separate the hair-free head, hair, and later body; review each against the
   artist's shape and graphic features. Use the inspected P2 sources as construction
   candidates; preserve their originals and document local repairs in matched views.
2. Fit independent eye/lid/lash assemblies. Use full eye mesh swaps for distinct
   anime poses, smooth blink shapes within compatible assemblies, and independent
   iris gaze. Preserve left/right control and seams against the fixed head.
3. Fit one deformable mouth region to approved reference poses, keeping stable
   topology for continuous speech and emotion blendshapes. Include the oral cavity,
   teeth, and tongue. Generated pose meshes are useful fitting references.
4. On the accepted UV-mapped head, test HOS's multi-view texture projection and
   bake while preserving its shape. Inspect actual imported detail and moving-light
   response in Unity.
5. Demonstrate neutral, blink, alternate eye state, speech, and their combinations
   before completing all six emotions, bilingual controls, and Ren's body/idle.

The [bust comparison README](../art/generated/characters/ren/bust-comparison-v1/README.md)
records completed sources and export limitations. The [root README](../README.md#launch-the-ren-bust-comparisons)
gives exact viewer launch instructions.

## Broader implementation following source selection

Build and visually verify an expressive, animated Ren for Lucid Loop in Blender and Unity 6.3 LTS, preserving the artist's likeness and the source selected through the current Unity comparison. Use Unity-chan as an implementation guide for eyes, eyelids, lashes, irises, and mouth deformation. Use Ren's original artist sheet as the authority for expression design, facial details, and aesthetic. Finish Ren before extending this work to the other characters.

## Shape and aesthetic constraints

### Latest user correction: designer eye silhouettes and NPRS

The user rejected the current H v1/v2 eyes as quasi-realistic and structurally
wrong for the designer's **American-anime** aesthetic. They are rejected style
history, not a baseline to promote through thicker eyeliner or palette changes.
Ren's smoky, aloof, sensual eye design must match the designer's drawn
silhouettes directly. Compare matched reference views of upper/lower lid contours,
corner angles, iris exposure, lash masses and smoky shading before accepting
another eye construction. Technical blink/gaze success does not establish this
match. Use actual Unity-chan construction as an implementation guide, without
copying its character proportions. Do not claim a 1:1 match from subjective
impression or a numerical test that does not measure the artist reference.

The user accepts the current mouth, cheeks, jawline and nose; preserve those
forms during the eye redesign. Speech articulation work may continue without
silently changing their accepted neutral identity.

**NPRS is required.** The primary shading reference is the Marvel Tokon-esque
implementation in `B:\openai-hackathon-game`, especially `ART_PIPELINE.md`,
`docs/ART_DIRECTION.md`, `Unity/Assets/Shaders/CharacterToon.shader` and
`Unity/Assets/Shaders/InkOutline.shader`. Slight Arcane influence is allowed,
but must remain subordinate to the designer's anime forms and this graphic
NPRS direction. Earlier Arcane-oriented material studies are historical evidence,
not aesthetic acceptance. Inspect the actual reference implementation before
adapting palettes, bands, highlights, linework and outlines for Ren and iOS.

- Standardize on exactly one shared male skeleton and one shared female skeleton;
  Ren uses the shared female rig. Character-specific facial controls and accessories
  must not create a different body skeleton for each character.
- Ren's baseball cap is a separate accessory, attached to a socket on the head
  bone and independently toggleable. Do not merge it into the character mesh.
  A bust prototype must expose the equivalent explicit head/socket hierarchy for
  later binding. Cap-on hair accommodation is a separate state; toggling the cap
  off restores the uncovered hairstyle without resetting facial controls.
- Keep the historical source `art/generated/characters/ren/meshy/model.glb` and its original textures immutable. A new candidate becomes the construction baseline only after its head proportions, face silhouette, eyes, nose, lips, jaw, and identity have passed the current visual review.
- Do not reuse the rejected generic fitted head or transplant Unity-chan's facial proportions. Build deformation topology around the selected Ren source. Any necessary local topology changes must preserve its reviewed neutral surface and be demonstrated in matched before/after renders.
- Preserve the designer's anime aesthetic, including Ren's elongated half-lidded grey-blue eyes, tapered upper lash lines, and distinctive rose lips. Painterly shading and nightclub lighting must support these features.
- Inspect the supplied Unity-chan assets for construction techniques. Their presence does not establish URP compatibility or justify replacing Ren's design.

## Required implementation

The September 13 Unity review rejected the current face for texture quality and likeness. Neutral visual quality is the immediate priority: replace the fragmented facial UV layout and undersampled feature painting with a dedicated face texture and designer-faithful eyes, lashes, and lips. Preserve the original geometry as the shape comparison; preserving its damaged texture is not a requirement. Working blink or speech controls do not establish likeness. Validate the neutral face against the artist sheet in Unity before extending facial animation.

1. Create coordinated eye/lid/lash assemblies, independent irises and gaze, smooth left/right idle blinking, and reliable full closure without exposed iris slivers or detached lashes. Eye mesh swaps may preserve distinct artist-defined silhouettes; speech must continue independently across the swap.
2. Create a deformable mouth with continuous lip contours and an appropriate oral interior. Preserve Ren's neutral lip shape; teeth and tongue must support speaking without changing her neutral likeness.
3. Implement all six artist expressions: NEUTRAL, AMUSED, SKEPTICAL, FOCUSED, ALERT, and GUARDED. Preserve the source gaze, brow, mouth, and head acting; include GUARDED's hand-to-mouth pose.
4. Supply genuine blendshapes for the separate noncommercial realtime lip-sync engine, covering English and Japanese, including vowel shapes, lip seals, labiodental/interdental shapes, and the agreed language-specific controls. Verify transitions and expression/blink/speech composition, not just target names.
5. Integrate Ren with the project's shared female rig and Unity character viewer. Provide her distinctive idle using Walt/Kimodo through the authorized Devin workflow, with independent idle blinks.
6. Use materials compatible with Unity 6.3 LTS and the project's URP configuration. Keep eyes and mouth readable under neutral and dynamic nightclub lighting. Design for landscape 16:9 and sustained 30 fps on iPhone 15 Plus.

## Evidence required before completion

- Show actual rendered Ren models beside the untouched Meshy baseline and corresponding artist references. Include front, three-quarter, and profile views under matched neutral lighting.
- Show neutral likeness before expanding facial engineering. Record any neutral-surface changes; reject unexplained proportion or silhouette drift.
- Show all six expressions, intermediate and full blinks, gaze extremes, representative English/Japanese speech poses and transitions, and combined expression/speech/blink playback.
- Inspect both close-up and gameplay-scale Unity captures, including nightclub lighting. Technical tests or blendshape counts alone do not establish visual acceptance.
- Verify saved Blender assets, exported geometry, rig compatibility, and the actual Unity viewer. Report unresolved artifacts honestly; do not label prototypes finished or claim a numerical aesthetic match without evidence.
- Measure sustained performance on an actual iPhone 15 Plus before claiming the device target is met. Desktop captures are not device performance evidence.
- Commit and push the completed, reviewed Ren work under the user's existing authorization, preserving unrelated concurrent work. List any remaining limitations explicitly.

## Scope boundary

The other four major characters and partygoing NPCs remain part of the broader project, but are outside this focused implementation goal. The separate lip-sync analyzer remains in `B:\unity-realtime-lipsync`; this goal supplies its Ren facial targets and integration surface, not a commercial lip-sync dependency.

After Ren's pipeline works and passes visual/runtime review, document its source
selection, native-surface construction, facial topology, paint, NPR, accessory,
rig, expression/speech and verification steps. Apply that same validated workflow
to the other four main cast members through dedicated subagents, preserving each
artist design and the two shared skeletons. Do not begin that rollout by copying
Ren's unresolved construction experiments or substituting a generic face.
