# Checkpoint three — local art and VIP pass

Local branch only. Open `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity` in the mvp-checkpoint Unity project and press Play. Run the dialogue server as described in CHECKPOINT-TWO.md; its health revision should be `vip-v3`.

## What changed

- Painted nightclub backdrop derived from the existing gameplay camera, with the team's concept as lighting/material reference. Live 3D characters, navigation, collider geometry and architectural depth occlusion remain underneath. The camera is fixed at a 16:9 composition, with letterboxing on other aspect ratios. This is a 2.5D presentation, not a fully modeled recreation of the concept.
- Lightweight faceted cast figures: Maya's pink ponytail/pale trousers, Theo's green coat/silver hair/glasses, Luca's apron and white shoulder towel, Ren's cap/headphones, investigator's ponytail/mask. Procedural stride, opposite arm swing, idle breathing, ponytail sway, crowd dancing and authored speaking gestures. Figures turn with navigation; they are not imported production rigs or Transistor assets.
- Illustrated portraits for the five main characters, shown during conversations and authored beats. One neutral portrait per character; no emotional variant system or lip sync in this pass.
- VIP is visually screened with brass partitions and plants. Theo can offer a supported invitation. The player must press Follow Theo into VIP to accept; the actual route runs, then conversation reopens at the destination. Maya's wait agreement is honored; a following Maya moves with the group.
- Ending that private conversation sends Theo back to his original spot. Recognition still requires Maya's encounter area and Theo being near the affair partner; escorting alone is not a success condition. Waiting/avoiding the encounter still does not win.

## Focused human check

1. Play the opening: judge character scale, readable silhouettes, walking and the illustrated beats.
2. After reset, ask Maya to wait. Approach Theo, ask to speak privately, accept the VIP invitation. Check that Theo and PC move into seating while Maya stays behind.
3. Converse, scroll history, then End conversation. Theo should return to his original spot.
4. Complete the existing prevention route: Maya agrees to put the phone away; Luca agrees to help; ask Maya to follow and approach the encounter. Ren's two music buttons remain available.
5. Flag art/collider mismatch if a character appears to walk through painted furniture, or a visible open gap cannot be walked through. The painted backdrop follows the geometry guide closely but is not guaranteed pixel-identical.

## Verification and remaining limits

Unity EditMode: 14/14 tests passed. Server dialogue regressions: 7/7 passed. Automated full-scene tests passed the opening/rewind, conversation cancellation/history, music switching, VIP invitation and accepted movement with Maya waiting and following, then the safe ending. Live Theo checks returned an invite before arrival and a stationary conversation after arrival. No new larger-mystery story or success predicate was added.

This remains an Editor prototype: mobile frame rate, device build/signing, voice integration, production rigging and lipsync are not verified. Static painted lighting does not relight with music; the music and live figures can change, the room painting remains fixed. Generated painting can introduce small discrepancies against depth/collision geometry. Final art polish and a timed human playthrough remain review items.

## Asset provenance

Original team references: `art/characters/cast-lineup.png`, `art/characters/player-avatar.png`, `art/environments/nightclub-isometric-layout.png`. Transistor and Arcane references informed direction only; no commercial-game screenshots/assets are imported.

Generated with the built-in image_gen tool:
- `Unity/Assets/Gyms/Resources/MvpArt/Portraits.png`: five-column portrait atlas, derived from team cast references.
- `Unity/Assets/Gyms/Resources/MvpArt/ClubBackdrop.png`: painting over a clean render of this scene, using the team environment as style reference.

Prompts are preserved in `docs/ART-PROMPTS.md`. The backdrop is installed through a camera-aligned quad; original invisible geometry writes depth so live characters can be hidden by architectural foreground elements. Original source art is preserved.
