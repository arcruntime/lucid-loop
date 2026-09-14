# Before the Drop — local polish checkpoint

September 14, 2026. Branch `codex/btd-checkpoint-one`. Open the **mvp-checkpoint/Unity** project in Unity 6000.3.24f1 and play `Assets/Gyms/Scenes/BeforeTheDrop.unity`. Arc approved this MVP for branch publication on September 14. Later polish is tracked in EVENING-POLISH.md.

## What changed

- **Larger club based on the actual setting concept.** World-space painted scenery, new walkable aisle geometry, bar route, central dancefloor, rear DJ platform, and VIP reached through the front stairs. Furniture/partition collision and approximate depth occlusion prevent the main routes from cutting through the cocktail tables or VIP wall. Live crowd figures are scattered rather than arranged in a grid. The fixed-angle orthographic camera smoothly follows the investigator, clamps at painting edges, and frames the confrontation before returning to exploration. Floor lighting has a restrained pulse and a warmer Intimate treatment.
- **Live voiced conversations in loop two.** Approach Maya, Luca or Theo and select **Start voice**. Speak naturally; use headphones. Existing voices are retained: Maya `gleam`, Luca `meridian`, Theo `vesper`, Ren `quartz`. Start/stop and mute are explicit; closing, pausing, switching characters or rewinding cleans up capture/playback and pending work. Stop voice to type instead. Ren retains the two music-choice buttons.
- **Voice changes game actions.** The separate `/mvp-live` endpoint uses GPT-Live client delegation, sends current-character knowledge/history to the existing validated dialogue backend, and waits for Unity to acknowledge a supported action before returning the approved response for speech. The live model can paraphrase that response; listening still needs to confirm that its interim speech doesn't imply an unsupported agreement. Prior-loop transcripts remain readable to the player but are not seeded into NPC knowledge.
- **Recorded opening/DJ lines.** All 16 fixed lines were generated using the existing cast voices and saved only after their returned transcripts matched the script. They are bundled in `Resources/MvpAudio/Story`, so the authored opening and DJ acknowledgments do not depend on per-line generation while playing. Continue skips/stops the current line. Music ducks under conversation/story audio; the original two placeholder tracks and backspin remain.
- **Simple avatars deliberately retained.** The Quaternius costume audition was rejected because the clothing looked puffy. It is not used by this scene. Imported rigs and the separate audition remain local for possible later art work; they are not required by this checkpoint. No Adobe setup or new model/clothing pipeline is required.

## Setup and controls

Start `tools/start-dialogue.command` in Terminal. It requests the API key with hidden input if needed. The key remains in the process environment, not Unity, source or a saved key file. The launcher watches server source changes and restarts with the same environment. Keep Terminal open while using AI conversations. `/health` should report `polish-v5` and `ready:true`.

Click the floor to walk; click a main character to approach. **Continue / Space** advances the authored opening. After the reset, head left to prepare. **Start voice**, **Mute mic**, **Stop voice**, **End conversation** control live sessions. **Follow Theo into VIP** accepts his invitation and performs actual movement. **Restart demo** resets story and transcript. Music choices alone do not solve the encounter; the original phone-away plus calm intervention prevention rule is unchanged.

## Human acceptance check

1. Wear headphones. Play the opening once. Listen for the voiced lines, music ducking, backspin/reset, and any line that starts late or gets clipped. Watch camera movement, character facing, feet/furniture alignment and crowd placement.
2. In loop two, talk to Maya before she sees Theo. Start voice and say something like “Wait here while I ask the bartender something; keep your phone away.” She should be puzzled/helpful, not already know about the affair. End conversation, walk away, and confirm she stays.
3. Ask Luca to keep an eye on you and your friend if trouble starts. He should not invent Theo as the threat. Listen to a response, interrupt or mute once, and check captions/history. If voice fails, stop it and use typing.
4. Request Intimate from Ren, then ask Maya to follow and approach Theo. Check the changed outcome. Optionally test Theo's private invitation and the VIP route.
5. Report anything confusing or broken with a screenshot and actual versus expected behavior. The final timing, microphone permission, echo, conversational latency and music balance need this human run.

## Evidence

- Unity EditMode: **14/14 passed** after the environment/voice changes.
- Backend: **33 passed, 1 opt-in test skipped**. New fixtures exercise commit-before-result, duplicate suppression, corrections/disconnect cancellation and current-history seeding with the documented delegation envelope.
- Full scene fixture: opening/recognition/catastrophe/reset, waiting, music-only non-success, dialogue cancellation/history, VIP acceptance with waiting and following Maya, and prevention all passed in the expanded layout.
- Final scene run: routes to all four main NPCs; voice UI/captions; applied action; duplicate/old-loop rejection; cleanup and shutdown passed. A teardown null-reference found during verification was fixed and the run repeated cleanly.
- **Live API bridge tested**, separately from fixtures: synthetic spoken input “Could you wait here while I ask the bartender something? Please keep your phone away” produced Maya's contextual response and `wait` + `private_approach`. Input, output and audio are saved locally under `.local/voice-smoke/`. That test does not validate the user's microphone or subjective voice quality.
- All **16 fixed voice lines** completed with transcript matching. No extra cast voices were selected.

## Integration and limits

Core changes are `ExpandedClub.cs`, the `FirstLoop.Voice.cs` / `FirstLoop.StoryAudio.cs` partials, and `server/src/mvp-voice.mjs` / `story-voice.mjs`. Existing CharacterGym and LiveGym remain intact; LiveConnection has an additional startup-message overload, preserving its original call signature. Optional avatar-audition files are not required by the playable scene.

This is a 2.5D Editor prototype, not a fully reconstructed 3D club or a Transistor-quality character-art claim. Scenery and much of its lighting are painted; depth masks are approximate. Ren's production mixing animation, facial rigs/lipsync and finished world-character art remain outside this pass. No new murder mystery subplot or win condition was added. iPhone performance/build/signing have not been tested. The loopback server URLs must be configured for a reachable relay before a device build.

## Provenance

`Resources/MvpArt/ClubExpanded.png` was generated with the built-in image-generation tool by cleaning the team's `art/environments/nightclub-isometric-layout.png`; exact prompt is in ART-PROMPTS.md. Original concept and previous backdrop are preserved. No Transistor/Arcane game assets are imported. Cached speech was generated through GPT-Live using the existing cast configuration and the fixed script in `server/src/story-voice.mjs`; `.sha256` sidecars identify script/voice versions. The temporary Apple-synthesized utterance was test input only and is not game audio.

## Demo observer and saved credentials (September 14)

The **Show AI observer** button toggles a recording-oriented panel. Voice and typed decisions show the current character/music context, player request, and actions only after validation. Failed requests are labeled; story events, rewind and button-selected music are explicitly authored/game-rule events. No synthetic tension score or model reasoning is displayed. The panel preserves the last result when returning to the map; a new request or story event replaces it. Portrait proportions are preserved in the reduced space.

See [LOCAL-SETUP.md](LOCAL-SETUP.md) for the exact project/scene and one-time Keychain setup. The updated launcher checks for an existing healthy server, then uses the environment or the named Keychain item, with hidden session-only input as a fallback. No credential was saved automatically.

## Team checkout

Repository: `https://github.com/jethac/lucid-loop`. Review branch: `codex/btd-checkpoint-one`. Clone/check out that branch with Git LFS installed, run `git lfs pull`, open the checkout’s `Unity` folder in Unity 6000.3.24f1, then open `Assets/Gyms/Scenes/BeforeTheDrop.unity`. Run `npm ci` in `server`; the local dialogue launcher is `tools/start-dialogue.command`. Each developer supplies their own credential locally.

This checkpoint is based on main at `1abc228`. At publication preparation, Jetha’s `feat/character-and-live-gyms` was 67 commits beyond that base. Those newer gym/art changes are not integrated or overwritten here. Review or cherry-pick deliberately; this is not a claim that both branches have been combined or tested together.

Latest observer smoke passed both loops, navigation, voice-action UI, duplicate/stale rejection and prevention. Arc’s accepted remaining issues include VIP recognition coverage and Maya’s opening delivery; see EVENING-POLISH.md.
