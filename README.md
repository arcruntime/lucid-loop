# Lucid Loop

Two playable Unity gyms are implemented: an offline nightclub for movement and character conversations, and a separate live voice studio backed by the project's OpenAI relay.

**Try them:** open `Unity/` in Unity 6000.3.24f1, load `Assets/Gyms/Scenes/CharacterGym.unity`, and press Play. Click/tap to walk, hold a character to approach and talk, and use the top-right button to switch gyms.

**Builds:** [Unity builds on GitHub Actions](https://github.com/jethac/lucid-loop/actions/workflows/unity-builds.yml) is configured for every push to `main`, same-repository pull requests, daily at 03:17 JST, and manual runs. Successful runs publish a Windows ZIP with a commit ID and SHA-256 checksum. Mac builds are deferred. Download them from the run's **Artifacts** section; diagnostics are uploaded on failures too.

**Windows CI is provisioned:** the dedicated runner on stadia-testbed has Unity 6000.3.24f1 with Personal activated. Remote tests and player packaging have passed. The runner requires its Windows user session to remain logged in. Mac provisioning and builds are deferred. See [CI setup and host status](docs/CI.md).

## Project direction

- The **Unity 6.3 LTS** project lives in the `Unity/` subfolder.
- Target phones first, in **landscape orientation**, with a **16:9 target aspect ratio**.
- Minimum-spec phone: **iPhone 15 Plus**.
- Use [osu-framework-unity-di](https://github.com/splatterfacegames/osu-framework-unity-di) as a core dependency for dependency injection.
- Project: `Unity/`, pinned to **Unity 6000.3.24f1** (Unity 6.3 LTS), with URP 17.3.

## Context

- Game design source: the Game Design Document, REWIND gameplay concept, and Character Guide sections of the [design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit).
- Earlier exploratory work lives in the separate `openai-hackathon-game` repository.

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
