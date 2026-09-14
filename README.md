# Lucid Loop

![Before the Drop](art/beforethedrop.png)

The project logo appears while the game connects or resumes a night, with a Cancel button and no added wait time. The original is preserved in `art/beforethedrop.png`.

The [arcruntime checkpoint review](docs/ARCRUNTIME_REVIEW.md) records the selective integration of her conversation portraits and rewind cue, plus preserved paintings and voiced story assets. The authoritative encounter and iOS relay remain the gameplay implementation.

The [dressed nightclub](docs/ENVIRONMENT_ART.md) supplies the 3D room, furnishings, raised VIP area, lighting artwork and conversation backgrounds. [Unity captures](docs/validation/nightclub-v2/overview.png) and the geometry audit document the environment independently of the unfinished character models.

The primary game prototype is **Before the Drop**: a server-authoritative nightclub encounter with an authored catastrophe, rewind, retained clues, and OpenAI Live conversation integration. The original offline nightclub and separate Live voice studio remain available as diagnostic gyms. Final character animation and physical iPhone acceptance remain in progress. Japanese voice support is excluded from shipping and its implementation is paused.

**Try the encounter:** open `Unity/` in Unity 6000.3.24f1 and load `Assets/Gyms/Scenes/BeforeTheDrop.unity`. Start the local relay and follow the [encounter quickstart](docs/ENCOUNTER_QUICKSTART.md). Use **Walk toward Theo** to see the opening, then **Rewind** to begin another attempt with the witnessed clue retained. Real NPC conversations require an OpenAI key in the relay process environment; the GitHub CI secret is not automatically available locally.

**iOS is the primary target.** Use the [iOS build instructions](docs/IOS_BUILD.md) and [local development relay setup](docs/IOS_LOCAL_RELAY.md). Nightclub lighting uses a shared Forward pipeline with bounded local lights and shader feature stripping; see the [rendering policy](docs/IOS_RENDERING.md). [Validation evidence](docs/IMPLEMENTATION_VALIDATION.md) separates passing tests from pending device and presentation checks.

The complete unsigned iOS app [compiled successfully in hosted Xcode CI](https://github.com/jethac/lucid-loop/actions/runs/34786581140), including UnityFramework and GameAssembly. The [build workflow](docs/IOS_EXPORTED_APP_CI.md) retains the unsigned app and checksum. Signing, installation and physical iPhone acceptance remain pending.

The complete prevention route has passed [real-provider CI](https://github.com/jethac/lucid-loop/actions/runs/34777703855), including nearby conversations, committed actions and survival through the full loop. Phone exploration now uses a compact HUD with expandable conversation and clue panels. Physical iPhone testing remains pending. The [Japanese speech investigation](docs/JAPANESE_SPEECH_PRODUCER.md) records why the available Japanese model still needs quality work before integration.

Select a character and tap **Walk & talk** to walk to a server-chosen speaking position and begin talking once in range; nearby characters show **Talk**. You can cancel or tap elsewhere to walk. Conversation feedback reports whether a requested action was accepted; **Leave** resumes the night and any agreed character movement.

Successful mediation now confirms Theo's admission and Maya's choice to leave with the player, keeping her evidence. Luca receives that authored outcome for his response; the participants remember it until rewind. Separation and surviving the set are still required for victory. See the [demo scenario](docs/DEMO_SCENARIO.md).

**Shipping voice scope: English.** Japanese voice and lip-sync work is paused by the user's September 14 decision and is outside this release. The [Japanese investigation](docs/JAPANESE_NPC_ALIGNMENT.md) remains archived evidence, not a release gate.

The player has a green overhead beacon. NPC speech bubbles show speaking availability: dim when out of range, outlined when available, and filled for an available selected character. See the [phone-layout capture](docs/images/encounter/interaction-markers.png).

Accepted mood changes now correctly update music and lighting. A protocol-casing regression was reproduced and fixed; the [validation ledger](docs/IMPLEMENTATION_VALIDATION.md) records the before/after checks.

Catastrophe fades the club music to silence over 0.12 seconds, including while paused. Rewind fades it back over 0.8 seconds without changing the saved music-volume setting.

**Pause** closes the current conversation and opens a menu with Resume, Settings, Controls and Main Menu. Settings saves music volume on this device. Main Menu returns to connection setup while preserving the resumable night. Resume continues the world without reopening voice; reconnecting restores the server's pause state. Starting a new night starts unpaused. Quit is desktop-only.

Encounter guidance follows the current phase: it explains responding to the confrontation, walking away after mediation and letting the set finish after separation. The opening shortcut is available only before first-loop recognition. Guidance does not reveal undiscovered clues or declare victory before the server does.

Reopening a conversation now supplies that NPC's own accepted commitments: Maya's private approach and recording state, Theo's distance agreement, or Luca's mediation. Rewind clears these commitments; other characters' private agreements stay out of the context.

Luca's refused requests now explain when to ask Ren for calmer music or leave the conversation so the mediation group can assemble. The HUD retains this guidance even if speech delivery fails; no refused request changes game state.

A [real spoken-input test](https://github.com/jethac/lucid-loop/actions/runs/34779053805) also passed: streamed synthetic speech triggered native Live delegation and a confirmed game action without a typed command. Physical microphone, interruption and conversational-quality acceptance remain separate checks.

The [spoken correction test](https://github.com/jethac/lucid-loop/actions/runs/34779808422) also passed: a pending wait request was reinterpreted as follow, with no stale wait action committed. This test deliberately controls response timing; it does not measure natural interruption latency.

An audio-device change now stops conversation playback and closes the session; tap **Talk** to restart. The controller lifecycle passed an Editor PlayMode check against the local protocol fixture. Physical headset routing and iOS echo cancellation remain unverified; see [validation evidence](docs/IMPLEMENTATION_VALIDATION.md).

The [native iOS voice backend](docs/IOS_NATIVE_VOICE.md) now routes microphone capture and NPC playback through one voice-processing engine after explicit Mic enable and permission. Mic off sends silence while playback continues. The plugin and complete Unity app compile against Apple's iOS SDK. Signing and physical iPhone audio acceptance remain pending.

The HUD now bundles licensed Noto Sans JP. Actual Editor phone-preview checks cover Japanese transcripts, wrapping, Latin text and uncommon BMP kanji; [captures and limitations](docs/IMPLEMENTATION_VALIDATION.md) include a supplementary character that the current text renderer omits. Japanese lip-sync quality and device keyboard behavior remain separate work.

The [Japanese NPC voice capture workflow](docs/JAPANESE_NPC_VOICE_EVIDENCE.md) collects bounded evaluation recordings from the four configured voices, with raw transcript and packet timing evidence. It is manually triggered and does not enable the unaccepted Japanese lip-sync model.

The [checked Japanese reading prototype](docs/JAPANESE_READING_FRONTEND.md) now rejects unknown spoken text, supplies explicit cast-name readings and preserves pronunciation metadata before the upstream mora parser. It remains experimental and has no audio timing or production language switch.

The [GDD](docs/GDD.md), [technical design](docs/TECHNICAL_DESIGN.md), [authored demo scenario](docs/DEMO_SCENARIO.md), and [source audit](docs/DESIGN_SOURCE_AUDIT.md) describe what is being built. Figma takes precedence over the Google document. The [animation handoff](art/animation-handoff/README.md) contains 17 downloaded source FBX files and their unresolved mapping/retargeting notes.

**Try the original gyms:** load `Assets/Gyms/Scenes/CharacterGym.unity` and press Play. Click/tap to walk, hold a character to approach and talk, and use the top-right button to switch gyms.

**Builds:** [Unity builds on GitHub Actions](https://github.com/jethac/lucid-loop/actions/workflows/unity-builds.yml) is configured for every push to `main`, same-repository pull requests, daily at 03:17 JST, and manual runs. Successful runs publish a Windows ZIP with a commit ID and SHA-256 checksum. Mac builds are deferred. Download them from the run's **Artifacts** section; diagnostics are uploaded on failures too.

**Windows CI is provisioned:** the dedicated runner on stadia-testbed has Unity 6000.3.24f1 with Personal activated. Remote tests and player packaging have passed. The runner requires its Windows user session to remain logged in. Mac provisioning and builds are deferred. See [CI setup and host status](docs/CI.md).

## Project direction

- The **Unity 6.3 LTS** project lives in the `Unity/` subfolder.
- Target **iOS** first, in **landscape orientation**. Use 16:9 as a composition reference while adapting UI to the device's actual aspect ratio and safe area.
- Minimum-spec phone: **iPhone 15 Plus**.
- Performance target: **sustained 30 fps on iPhone 15 Plus**.
- Characters require **full expressive real-time lip-sync** for OpenAI live speech, with **English shipping support**. Japanese voice work is deferred for this release.
- Lip-sync must use **open-source or project-owned code; no commercial lip-sync libraries**.
- Use [osu-framework-unity-di](https://github.com/splatterfacegames/osu-framework-unity-di) as a core dependency for dependency injection.
- Project: `Unity/`, pinned to **Unity 6000.3.24f1** (Unity 6.3 LTS), with URP 17.3.

## Context

- Game design source: the Game Design Document, REWIND gameplay concept, and Character Guide sections of the [design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit).
- Earlier exploratory work lives in the separate `openai-hackathon-game` repository.
- Current character work: [Ren implementation goal](docs/REN_CHARACTER_GOAL.md) and [Japanese creator workflow research](research/japanese-anime-character-workflows-2026-09.md).
- Main cast targets are **12k triangles at gameplay LOD1** and **40k in solo conversation close-ups**; crowd members are **at most 6k each**. The whole-frame target is **250–300k**. See the [current allocations](docs/IOS_RENDERING.md#user-specified-geometry-targets).

## Open the current Ren character review

Open the project `B:\lucid-loop\Unity` in Unity **6000.3.24f1**, then open
`Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerBlinkReview.unity`
and press **Play** after import/compilation finishes. Use the open-A and seal
controls for the mouth, the independent blink sliders for the eyes, and the
**Idle blink** toggle. Reuse the existing Editor if it is open. The earlier
mouth-only scene remains alongside it as `RenDesignerMouthReview.unity`.

The scene and its dependencies have been copied and hash-verified in this project;
main Editor import and Play Mode mouth validation passed. An [actual main-project
render](docs/validation/ren-main-review/main-render.png) is preserved. Its previous captured Windows player
is `.local/ren-designer-mouth-v1/RenDesignerMouthReview.exe`. Ren's likeness and
NPR shading remain unfinished. The blink scene has actual main Editor static and
five-second motion captures, but gaze, full speech and the six-expression system
remain incomplete. The local clip is
`.local/ren-main-blink-v1/Ren-Designer-Blink-and-Mouth-5s.mp4`.
See [current work and evidence](docs/REN_H_CURRENT_WORK.md).

## Launch the Ren bust comparisons

The comparison includes **Tripo H closed rest**, **Meshy closed rest**, and
**Meshy open A**, alongside the original Ren design and the generated reference
sheets. These are the actual high-detail sources with 8K base-color textures.

1. Download the binary assets with `git lfs pull` from the repository root.
2. Open the repository's **`Unity/`** project in **Unity 6000.3.24f1**.
3. In Unity's Project window, open
   **`Assets/CharacterArt/Generated/Preview/Scenes/RenBustComparison.unity`**.
4. Press **Play**. For the intended layout, select **16:9** in the Game view.

Use the arrows above either model to choose candidates. Drag either model to
orbit both, and scroll or use **Zoom** to move closer. **Front**, **Three-quarter**,
**Profile**, and **Other side** provide matching views. Switch between **Base color**,
**Neutral lighting**, and **Nightclub lighting**. **Show reference** opens the center
panel; its arrows cycle the artist sheet, exact generation inputs, and thirteen
expression sheets. Source normal maps default to off and can be enabled in lit modes.

On this workspace, the verified Windows viewer can also be launched from the
repository root in PowerShell:

```powershell
& ".\.local\ren-bust-viewer\RenBustComparison.exe"
```

That local executable is a build output and is not included in Git. To create
it after a fresh checkout, follow the [Windows build instructions](Unity/Assets/CharacterArt/Editor/REN_BUST_COMPARISON.md#build-and-capture).
The checked-in Unity scene is ready to open without rebuilding it.

See the [comparison results and provenance](art/generated/characters/ren/bust-comparison-v1/README.md)
and [visual recommendation](art/generated/characters/ren/bust-comparison-v1/REVIEW.md).
The earlier comparison preferred Tripo H closed rest among those whole busts.
The user subsequently selected the exact
[Studio H A-open original](art/generated/characters/ren/bust-comparison-v1/tripo/studio-h3.1-a-open/README.md)
as Ren's **full face-shape master**, including cheeks, jaw and chin. It has been
recovered but is not yet integrated into this older comparison scene. Two older
Studio P2 busts still await export. The separate-parts experiment below uses
newly generated API assets and has its own review.

## Review the selected H face shape

The matched [H/P2 shape comparison](art/generated/characters/ren/parts-workflow-v1/face-source-h-audit-v1/comparison.html)
shows front, three-quarter and profile clay renders of the exact H source, P2
open A, and P2's current closed rest. Open the HTML file directly, or serve it:

```powershell
python -m http.server 8769 --bind 127.0.0.1 --directory art/generated/characters/ren/parts-workflow-v1/face-source-h-audit-v1
```

Then open **http://127.0.0.1:8769/comparison.html**. The comparison uses an
approximate uniform registration; H's open mouth is not a closed-rest target.
The [audit](art/generated/characters/ren/parts-workflow-v1/face-source-h-audit-v1/README.md)
records the alignment limits. The H surface is approved; fitting controls and
rebuilding its final paint remain in progress.

## Inspect the rejected H eye prototype

**The user rejected this eye construction and shading.** Keep this viewer as
diagnostic history. The next eye design must match the artist's American-anime
silhouettes, with Tokon-esque NPRS; it is not a refinement approved by this viewer.

On this workspace, the actual Unity Windows review player is available at:

```powershell
Set-Location B:\lucid-loop
& ".\.local\ren-h-reference-v1\RenHReference.exe" -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900
```

This is an H-derived construction head with separate cap, blink/gaze and basic
closed/open-A mouth controls. Actual running-player renders are in
`.local/ren-h-reference-v1/live-review/`. It remains a construction prototype:
lip paint, eye styling, hair shading and support seams need correction, and
17 reference-performance channels are still unsupported. It does not establish
full bilingual speech or iPhone performance; the dense head alone has 334,768
triangles. The current shipping target remains iOS.

The new `RenHReferenceAnimation.unity` scene has built in the isolated scratch
project. Copying its generated assets into the main project is pending the
engineering test window; the older scenes below do not contain this H assembly.
See the [visual review](docs/REN_H_ASSEMBLED_VISUAL_REVIEW.md) for outstanding
corrections. UI replay/scrub verification is still in progress.

## Launch the Ren NPR shading study

Open **`Assets/CharacterArt/Generated/Preview/Scenes/RenNprReview.unity`**
in Unity, press **Play**, and select **16:9**. Both panels use the same older
V2 geometry and paint so the old diffuse and painterly NPR materials can be
compared directly. This is a shader study; it does not contain the corrected H
face or recovered detailed hair.

Use the facial controls and moving nightclub lights to compare shape readability.
The scene selects its own preview pipeline with soft main-light shadows.
[Build and capture instructions](Unity/Assets/CharacterArt/Editor/RenNprReview.md)
and [actual rendered evidence](art/generated/characters/ren/parts-workflow-v1/npr-style-v1/STYLE_CONTRACT.md)
describe the verified paths and limitations. Baked lip highlights still require
paint correction, and iPhone performance has not been measured.

## Launch the Ren parts study

Open **`Assets/CharacterArt/Generated/Preview/Scenes/RenPartsStudy.unity`** in the
same Unity project, press **Play**, and use **16:9** in the Game view. Run
`git lfs pull` first on a fresh checkout. The scene opens with the original artist
reference, a head-only view, and the same head with separately placed hair.
Orbit, zoom, reference selection and lighting controls work as in the bust viewer.

These are **untextured P2 construction sources**: the head has an open mouth, and
the viewer exposes unfinished eyes, raised brows, mouth interior and hair defects.
It does not yet demonstrate Ren's finished face, blinking or speech. Both source
FBXs and their paid task receipts are preserved; the two jobs cost **200 credits**.

See [the parts checkpoint and next work](art/generated/characters/ren/parts-workflow-v1/README.md),
[actual Unity captures](art/generated/characters/ren/parts-workflow-v1/unity-review/),
and [study setup](Unity/Assets/CharacterArt/Editor/RenPartsStudy.md).

## Launch the Ren face study

Open **`Assets/CharacterArt/Generated/Preview/Scenes/RenFaceStudy.unity`** in the
same Unity project, press **Play**, and select **16:9**. It opens with the corrected
clay head on the left and the **static Tripo texture trial** on the right. The
original artist sheet appears in the center. Orbit, zoom, and neutral/nightclub
lighting work as in the other studies.

Use either panel's arrows to choose clay, temporary colors, or the Tripo trial.
The working face has mouth-seal/open-A, independent blink, idle blink, gaze and
optional fitted-hair controls. Controls are hidden for the static texture result.
Intermediate gaze has a known iris/sclera intersection; final speech, expressions
and body animation remain unfinished. Tripo's 8K paint is more realistic than the
intended anime aesthetic, and its eyes are unaccepted. This is a review checkpoint.

On this workspace, launch the verified interactive Windows build with:

```powershell
& ".\.local\ren-tripo-texture-viewer\RenFaceStudy.exe" -force-d3d11 -screen-fullscreen 0
```

The executable is a local build output. Fresh checkouts can open the checked-in
scene after `git lfs pull`; see [face study setup and rebuild instructions](Unity/Assets/CharacterArt/Editor/RenFaceStudy.md).
Actual live-control evidence is in [the V2 face captures](art/generated/characters/ren/parts-workflow-v1/face-integration-v2/unity-review/).
See the [Tripo result, cost and limitations](art/generated/characters/ren/parts-workflow-v1/tripo-texture-v1/README.md).

### Browser texture viewer

The browser viewer loads the original Tripo GLB beside Ren's artist sheet. From
the repository root, install its pinned Three.js dependency once, then run:

```powershell
npm --prefix tools/character_art/viewers ci --no-audit --no-fund
python tools/character_art/serve_ren_texture_viewer.py --port 8767
```

Open **[http://127.0.0.1:8767/](http://127.0.0.1:8767/)**. Drag to rotate, scroll
to zoom, and use the front/quarter/profile and lighting buttons. The reference
toggle switches between the original design and the painting input. Keep the
server running while viewing; it listens only on this computer. This viewer
shows the static provider result and does not drive facial blendshapes.

## Launch the Ren complete-head review

Open **`Assets/CharacterArt/Generated/Preview/Scenes/RenCompleteHead.unity`** in
Unity and press **Play** with a **16:9** Game view. The original artist sheet is
shown beside the assembled painted/clay candidates. The earlier Tripo texture
and face studies remain available for comparison.

This working review adds painted skin, hair, a cap and ear jewelry to the repaired
face. Controls include independent blink, idle blink, continuous gaze, closed
rest/open A, and separate cap/hair toggles. The cap toggle also fits the hair under
the hat. Hair likeness and eyelid painting still need refinement; full expressions,
English/Japanese speech shapes, body fitting and phone performance remain unfinished.

For the browser review, install the same dependency used by the texture viewer,
then run this in a second terminal:

```powershell
npm --prefix tools/character_art/viewers ci --no-audit --no-fund
python tools/character_art/serve_ren_texture_viewer.py --port 8768 --head
```

Open **[http://127.0.0.1:8768/](http://127.0.0.1:8768/)**. This viewer drives the
actual GLB's facial shapes and gaze hierarchy. Keep the server running. See
[Unity setup, local player paths and evidence](Unity/Assets/CharacterArt/Editor/RenCompleteHead.md).

The current V2 assembly is **26,426 source triangles / 26,422 imported Unity
triangles**. See its [source package and actual Unity screenshots](art/generated/characters/ren/parts-workflow-v1/complete-head-v2/README.md)
or the [five-second Unity motion clip](art/generated/characters/ren/parts-workflow-v1/complete-head-v2/unity-review/motion/RenCompleteHead-v2-motion.mp4).
The clip demonstrates gaze, blink and one mouth gesture; it does not measure phone frame rate.

The [Tripo hair-reduction experiment](art/generated/characters/ren/parts-workflow-v1/tripo-hair-lowpoly-v1/README.md)
compares the original hair, Tripo's reduced result and the local low-poly version.
It is a separate source comparison; its returned hair is not yet fitted or painted.

## Current status

The main encounter and two diagnostic gyms are implemented in `Unity/Assets/Gyms/Scenes/`:

- **BeforeTheDrop**: the primary server-authoritative encounter, with catastrophe, rewind, retained evidence and a tested prevention route.
- **CharacterGym**: offline nightclub blockout, tap/click navigation, hold a character to approach and enter a close-up conversation study.
- **LiveGym**: separate voice studio with character selection, microphone streaming, captions, mute and explicit session close through the project-key backend. No SSO or API key in the Unity client.

Open `Unity/` in Unity Hub, then open `CharacterGym.unity` and press Play. Use the top-right button to switch gyms. Windows development builds are written to `Unity/Builds/Windows/LucidLoopGyms.exe`.

See [gym setup and validation](docs/gyms.md) and [relay setup](server/README.md). Primitive characters and audio-driven mouth motion are temporary gym fixtures; final expressive character rigs and iPhone performance qualification remain production work.

## Git LFS

Binary art and production assets are tracked with Git LFS; see `.gitattributes`.
Install Git LFS before cloning, run `git lfs install`, and use `git lfs pull`
to download assets in an existing checkout. Commit the LFS pointer files with
their associated changes; `git push` uploads the binary objects through LFS.
