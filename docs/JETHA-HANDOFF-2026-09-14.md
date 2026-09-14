# September 14 integration handoff

Branch: `codex/btd-checkpoint-one` (jethac/lucid-loop).

Main at `a2a3499` selectively imports assets from our older `95aae5d` checkpoint via `3ccafba`, keeping the server-owned encounter and iOS relay. This branch has newer playable-MVP polish. Adapt the useful assets/behaviors into that encounter; a wholesale replacement of main is not required.

## Priority updates since the imported checkpoint

| Update | Where to find it / integration intent |
| --- | --- |
| Team-supplied Suno tracks | `Unity/Assets/Gyms/Resources/MvpAudio/Aggressive.wav` (Untitled) and `Intimate.wav` (Midnight Warmth). Already edited into level-matched loops; retain the existing backspin/crossfade. No Suno credentials needed at runtime. Source notes: `docs/licenses/Team-Music.md`. |
| Dialogue and rewind timing | `server/src/story-voice.mjs`, `Resources/MvpAudio/Story/`, and `Runtime/Mvp/FirstLoop.cs`. Maya recognizes a public figure, not a personal acquaintance; arrival delivery is excited. Ren's full line and landing pause complete before rewind, with camera focus on Ren. Keep cached clips paired with their text/direction signatures. |
| Movement | `FirstLoop.cs`, `ExpandedClub.cs`: Luca runs to intervene and returns to normal speed; planter exclusion zones and the VIP front-stair route prevent obvious shortcuts; PC/Maya request music from the dancefloor. |
| UI readability | `FirstLoop.cs`: chat portraits clear input/voice controls, compact story portraits, collapsible memories, observer placement that preserves the encounter view. `CharacterActor.cs` + `OutlinedName.shader`: white names with black outlines; ClubArt enables HighContrastLabel for the cast. |
| Cast readability | `CastVisual.cs`: Theo's moving short coat avoids the extra/static legs appearance; AP wears pale turquoise to contrast with VIP furnishings. Simple avatars remain replaceable. |

The latest small additions are the excited arrival clip/direction, pale turquoise AP, outlined names, and native-resolution/uncompressed backdrop import settings. The backdrop is still only 1672×941; those settings preserve detail but do not create additional resolution and increase texture memory.

## Validation and recording

The source checkpoint passed Unity two-loop, recognition/prevention, rewind/audio timing, urgent intervention, planter/VIP navigation, DJ approach, portrait layout and voice-event fixture checks. Labels and AP contrast were visually inspected. This is not validation of the integrated main scene, live microphone flow, or iPhone performance; recheck the paths you adopt in main. Maya's new arrival delivery remains a listening judgment.

Open the `Unity` project, then `Assets/Gyms/Scenes/BeforeTheDrop.unity` for this branch's demo. Recording/submission references: `docs/RECORDING-READY.md` and `docs/SUBMISSION-DRAFT.md`.

Known follow-up: VIP recognition coverage still needs review. For this checkpoint's prevention path, secure Maya's discreet/phone-away agreement and Luca's calm-intervention agreement before recognition; music alone does not prevent the shove. Main's server-owned encounter remains authoritative for the integrated version.
