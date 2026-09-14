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
- AP remains non-interactable; burgundy clothing distinguishes the partner from the background crowd.
- Removed the walkable shortcut beside the VIP stairs. Added six planter exclusion areas along the main routes. Theo must use the front stair approach; this is navigation aligned to the painted scene, not a new 3D stair/environment rebuild.
- Moved Continue clear of the music/restart buttons.

The existing VIP recognition-coverage issue is separate and remains open. Full environment reconstruction is deferred.
