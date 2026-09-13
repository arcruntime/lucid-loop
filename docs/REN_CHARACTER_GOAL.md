# Ren character implementation goal

## Current milestone: complete Ren through the separate-parts workflow

Updated 2026-09-13. The user authorized committing and pushing the entire bust
comparison, with explicit launch instructions, then starting the new Ren workflow.
Thirteen selected head sheets and three imported Tripo/Meshy busts are available
in the Unity comparison. Tripo H closed rest is the provisional construction
reference from the inspected results. Additional H/P2 results await export.
The artist's original Ren design remains authoritative; the earlier Meshy surface
remains an immutable shape comparison. This is not final likeness approval.

Apply the [reviewed creator workflow](../research/japanese-anime-character-workflows-2026-09.md):

1. Separate the hair-free head, hair, and later body; review each against the
   artist's shape and graphic features. Inspect P2 geometry when exports arrive.
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
