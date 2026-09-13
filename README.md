# Lucid Loop

Two playable Unity gyms are implemented: an offline nightclub for movement and character conversations, and a separate live voice studio backed by the project's OpenAI relay.

**Try them:** open `Unity/` in Unity 6000.3.24f1, load `Assets/Gyms/Scenes/CharacterGym.unity`, and press Play. Click/tap to walk, hold a character to approach and talk, and use the top-right button to switch gyms.

**Builds:** [Unity builds on GitHub Actions](https://github.com/jethac/lucid-loop/actions/workflows/unity-builds.yml) is configured for every push to `main`, same-repository pull requests, daily at 03:17 JST, and manual runs. Successful runs publish a Windows ZIP with a commit ID and SHA-256 checksum. Mac builds are deferred. Download them from the run's **Artifacts** section; diagnostics are uploaded on failures too.

**Windows CI is provisioned:** the dedicated runner on stadia-testbed has Unity 6000.3.24f1 with Personal activated. Remote tests and player packaging have passed. The runner requires its Windows user session to remain logged in. Mac provisioning and builds are deferred. See [CI setup and host status](docs/CI.md).

## Project direction

- The **Unity 6.3 LTS** project lives in the `Unity/` subfolder.
- Target phones first, in **landscape orientation**, with a **16:9 target aspect ratio**.
- Minimum-spec phone: **iPhone 15 Plus**.
- Performance target: **sustained 30 fps on iPhone 15 Plus**.
- Characters require **full expressive real-time lip-sync** for OpenAI live speech, with **English and Japanese facial coverage**.
- Lip-sync must use **open-source or project-owned code; no commercial lip-sync libraries**.
- Use [osu-framework-unity-di](https://github.com/splatterfacegames/osu-framework-unity-di) as a core dependency for dependency injection.
- Project: `Unity/`, pinned to **Unity 6000.3.24f1** (Unity 6.3 LTS), with URP 17.3.

## Context

- Game design source: the Game Design Document, REWIND gameplay concept, and Character Guide sections of the [design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit).
- Earlier exploratory work lives in the separate `openai-hackathon-game` repository.
- Current character work: [Ren implementation goal](docs/REN_CHARACTER_GOAL.md) and [Japanese creator workflow research](research/japanese-anime-character-workflows-2026-09.md).
- Ren's complete LOD0 target is **40,000 rendered triangles**, including all clothing and accessories. See the [face-prioritized allocation and Tripo reduction study](docs/REN_POLYGON_BUDGET.md).

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
Tripo H closed rest is the preferred construction reference among these whole
busts. Three older Studio H/P2 busts still await export. The separate-parts
experiment below uses newly generated API assets and has its own review.

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

## Current status

Two gyms are implemented in `Unity/Assets/Gyms/Scenes/`:

- **CharacterGym**: offline nightclub blockout, tap/click navigation, hold a character to approach and enter a close-up conversation study.
- **LiveGym**: separate voice studio with character selection, microphone streaming, captions, mute and explicit session close through the project-key backend. No SSO or API key in the Unity client.

Open `Unity/` in Unity Hub, then open `CharacterGym.unity` and press Play. Use the top-right button to switch gyms. Windows development builds are written to `Unity/Builds/Windows/LucidLoopGyms.exe`.

See [gym setup and validation](docs/gyms.md) and [relay setup](server/README.md). Primitive characters and audio-driven mouth motion are temporary gym fixtures; final expressive character rigs and iPhone performance qualification remain production work.

## Git LFS

Binary art and production assets are tracked with Git LFS; see `.gitattributes`.
Install Git LFS before cloning, run `git lfs install`, and use `git lfs pull`
to download assets in an existing checkout. Commit the LFS pointer files with
their associated changes; `git push` uploads the binary objects through LFS.
