# September 14 MVP update

Primary change: Jetha’s supplied Suno tracks replace the procedural music placeholders. `Untitled` is Aggressive; `Midnight Warmth` is Intimate. Both are bundled, level-matched loops; gameplay does not require Suno access. Normal requests retain the backspin/crossfade. Music source/edit notes: `docs/licenses/Team-Music.md`.

Also included:
- CC0 record scratch for rewind, with music suppression and restart cleanup.
- Slower Maya opening delivery; public recognition of Theo (“famous… AND married”); natural dialogue punctuation.
- Ren’s completed line and short pause before rewind.
- Luca runs to intervene, then resumes normal walking speed.
- Theo’s short moving coat panels and dark trousers remove the apparent extra pair of legs.
- Supplied title/team logos for CapCut, a recording checklist, and updated submission copy.

Arc approved the music/transition, Ren, punctuation and Luca running. Unity two-loop, audio timing, movement, voice UI fixture and Theo geometry checks passed. Simple avatars and the larger map remain. Production avatars, VIP recognition coverage and HUD obstruction are follow-ups; no new map rebuild is included in this snapshot.

Open `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity` within the `Unity` project. See `docs/RECORDING-READY.md`. This is an Editor-tested MVP, not an iPhone-validated build. Kept separate from Jetha’s newer gym/character pipeline; merge useful changes deliberately.

## Follow-up: visibility and route polish

- Memories are collapsed behind a button; story portraits stay compact at lower left.
- During authored world beats, the observer moves left so Theo/AP remain visible. Interactive conversation keeps its existing observer column.
- AP remains non-interactable; pale turquoise clothing now distinguishes the partner from the warm VIP furnishings.
- Removed the walkable shortcut beside the VIP stairs. Added six planter exclusion areas along the main routes. Theo must use the front stair approach; this is navigation aligned to the painted scene, not a new 3D stair/environment rebuild.
- Moved Continue clear of the music/restart buttons.

The existing VIP recognition-coverage issue is separate and remains open. Full environment reconstruction is deferred.

## Conversation and DJ-stage follow-up

Conversation portraits move beside the chat panel instead of covering input/voice buttons; authored-story portraits retain the lower-left placement. Name labels now use consistent white text with black outlines, without rectangular backplates. Ren requests use a fixed spot on the dancefloor, with Maya following there; the stage-front shortcut is removed. A depth-only mask aligns Ren with the painted decks. Before her final line the camera pans to Ren, holds through her line, then rewinds and returns to the player.

For prevention in loop two, the implemented condition is Maya agreeing to a discreet/phone-away approach plus Luca agreeing to prepare a calm intervention, before recognition. Music changes and asking Maya to wait are optional preparation tools; if she waits, ask her to follow before approaching the encounter.

## Latest handoff

Maya’s arrival delivery emphasizes excitement with her friend; AP contrast and outlined cast names are improved. Backdrop import preserves native resolution without compression (additional texture memory; mobile unvalidated). See `docs/JETHA-HANDOFF-2026-09-14.md` for the concise integration guide and source-checkpoint validation limits.
