# Ren face in the Live API gym

Open `B:\lucid-loop\Unity` in Unity 6000.3.24f1 and load
`Assets/Gyms/Scenes/LiveGym.unity`. Enter Play Mode. Ren is selected first.
Shortcut: **Lucid Loop → Ren LOD0 → Open Live gym** opens the saved scene and
starts Play without rebuilding character assets.
Enter a working relay WebSocket address and its access token if required, then
press **Connect**. The OpenAI key belongs in the relay process environment,
never in Unity or the gym access-token field.

The scene now uses the complete `RenLOD0` prefab: approved-body-derived
female-v3 body, expressive head, skinned hair, detachable cap and headphones.
The camera follows a head anchor. This does not establish final artistic approval.

## Face and audio

- 42,170 Unity triangles for the complete character.
- English speech analysis uses the existing pinned Gaussian analyzer and its
  bundled model. Playback-consumed samples drive the timeline; silence inserted
  during underruns does not advance the speech clock.
- The adapter maps the model's 15 acoustic labels onto Ren's authored speech
  targets, including Unity FBX name prefixes. The analyzer does not distinguish
  every English articulation independently; DD/NN share the authored L contact.
- Disconnect, speaker changes and queue overflow reset speech. Bilabial contact
  excludes open vowels. The adapter sends speech snapshots to the assembled
  character controller; its mixer owns final speech/expression/blink weights.
  Body idle and secondary hair motion remain active.
- Closed-eyelid color correction is included. Remaining eyelid geometry and
  likeness issues are still subject to visual review. Live Japanese analysis is
  not installed; Japanese vowel geometry does not imply Japanese speech inference.

## Rebuild and checks

Use **Lucid Loop → Ren Live Face → Import into Live API gym** to reimport the
standalone FBX and bind the scene. This opens and saves LiveGym; preserve any
unsaved scene work first. **Capture gym camera** writes an image under
`Assets/CharacterArt/Generated/RenLiveGymFace/Evidence`.

**Lucid Loop → Validate Ren live speech** runs the adapter's Unity tests.
The final 2026-09-14 run passed all four tests, including script-reload cache
recovery; nine offline playback tests also passed.
These are local binding/queue checks, not proof of a real microphone-to-API call.

The actual imported Ren asset also passed a local analyzer check using 166,068
samples of recorded English PCM: the bundled model loaded, speech weights reached
100%, and the A target moved the skinned face by about 20 mm. A subsequent
script-reload regression exposed partial binding-cache restoration; runtime
caches are now explicitly nonserialized and rebuilt on enable. Play Mode pose
captures are separate from the analyzer test.

The current face-review shader uses directional shadow bands and additional
lights without receiving Unity screen-space shadows. It is a review presentation,
not validation of final nightclub shadow behavior or iPhone frame rate.

The local review relay uses `ws://127.0.0.1:8790/live`. Its launcher reads
`OPENAI_LUCIDLOOP_KEY` from BWS and passes it as `OPENAI_API_KEY` only in the
server process environment. Local readiness and an actual Ren upstream session
start/ready/close passed, with final usage confirmation. The evidence is
`art/generated/characters/ren/live-gym-face-v1/relay-session-check.json`.
No microphone or speech audio was used in that check; spoken conversation
remains to be verified. See [local relay setup](IOS_LOCAL_RELAY.md) for server
configuration.
