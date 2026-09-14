# arcruntime checkpoint review and selective integration

Reviewed `codex/btd-checkpoint-one` at `95aae5d` against our authoritative encounter branch, 2026-09-14. The six collaborator commits add a complete locally driven presentation checkpoint. This integration preserves her contributions selectively; it is not a wholesale merge of the alternative game implementation.

| Contribution | Decision and reason |
| --- | --- |
| Five-character portrait atlas | Imported unchanged with metadata; adapted into the authoritative conversation header, including phone layout and character switching. Atlas order: Maya, Luca, Theo, Ren, player. |
| Procedural backspin | Imported unchanged and played on an observed authoritative loop change; first snapshot and same-loop updates do not trigger it. Pause/disable stops it. Uses the existing music volume preference. |
| Painted club, expanded painting and shaders | Preserved under `art/collaborator-checkpoint/` for layout adaptation. The paintings were composed for a different map; placing them over our server geometry would misrepresent walkable space. Not included in Unity Resources. |
| Sixteen recorded cast lines | Preserved with catalog and source sidecars outside shipping Resources. Several are useful opening performances; timing, listening and authoritative phase integration remain necessary. Her private/space/fine lines cannot replace our explicit admission and decision to leave. |
| Procedural music | Preserved with original generator. Existing encounter tracks and tested pause/catastrophe mix remain in use. |
| AI action observer | Useful demo/debug concept; not copied as a shipping HUD because it is coupled to the alternative state machine and desktop layout. Our HUD already reports accepted/refused actions. |
| `FirstLoop`, `LoopState`, scene/builder, expanded navigation | Not merged. They require a compulsory first death and resolve from a private approach plus Luca preparation. Our authored game permits initial-loop prevention and requires exposure evidence, calm music, distance agreement, stopped recording, physical mediation, separation and set end. |
| `CastVisual` | Not merged: destroys existing visual children and clears the mouth binding. Its local NavMesh velocity source conflicts with server-driven movement and the artist's rigs. |
| Alternate dialogue and `/mvp-live` server | Not merged: trusts client state/history and client commit acknowledgements; the acknowledgement path installs state before checking pending correlation. Also restricts voice clients to loopback, incompatible with iPhone LAN development. Preserve our authoritative registry/leases and correction handling. |
| `LiveConnection` overload and package scripts | No current integration need. Keep current iOS LAN support and all existing live test lanes; collaborator `test:live` omits four of ours. |

The [asset manifest](../art/collaborator-checkpoint/manifest.json) records original paths, destination paths and actual file SHA256 values. [Original provenance](../art/collaborator-checkpoint/docs/LICENSE-AND-PROVENANCE.md) and [art prompts](../art/collaborator-checkpoint/docs/ART-PROMPTS.md) retain collaborator attribution and claims scoped to her checkpoint. Those provenance statements do not describe every asset in our larger branch. Original procedural music uses no third-party samples; generated images/speech retain their service terms.

The `.sha256` files accompanying story recordings identify script/voice/model inputs, not WAV integrity. Our manifest records the audio file hashes separately. No additional provider calls were made for this review.

The original branch remains available intact for comparison. Deferred assets are intentionally outside the Unity project; only integrated portraits and backspin are bundled in this checkpoint. No character-art files or existing main scene geometry were replaced.

## Validation

Actual main Unity Editor: [86 EditMode tests passed](validation/arcruntime-integration/editmode.xml), [three music PlayMode tests passed](validation/arcruntime-integration/mood.xml), and [phone encounter smoke passed](validation/arcruntime-integration/phone.xml) against the local authoritative relay. The cue test checks initial-snapshot suppression, loop-change playback, same-loop suppression, preference-scaled volume and pause stop. The complete scene smoke retains opening/catastrophe/rewind/clues.

The first scene attempt timed out after a hidden Game view delayed its capture step; opening Game view and rerunning completed in 32 seconds. [Actual HUD capture](validation/arcruntime-integration/portrait-hud.png) confirms the portrait appears. Cropped UI at the Editor capture edge remains a presentation issue; this is not physical iPhone layout acceptance. All 19 imported WAV files were checked as nonempty PCM. No new live-provider or subjective recorded-voice acceptance is claimed.
