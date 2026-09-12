# Lucid Loop

## Project direction

- Create the game with **Unity 6.3 LTS** in a subfolder of this repository (`B:\lucid-loop`), rather than at the repository root.
- Target phones first, in **landscape orientation**, with a **16:9 target aspect ratio**.
- Minimum-spec phone: **iPhone 15 Plus**.
- Use [osu-framework-unity-di](https://github.com/splatterfacegames/osu-framework-unity-di) as a core dependency for dependency injection.
- Project: `Unity/`, pinned to **Unity 6000.3.24f1** (Unity 6.3 LTS), with URP 17.3.

## Context

- Game design source: the Game Design Document, REWIND gameplay concept, and Character Guide sections of the [design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit).
- Exploratory work exists separately at `B:\openai-hackathon-game`.

## Current status

Two gyms are implemented in `Unity/Assets/Gyms/Scenes/`:

- **CharacterGym**: offline nightclub blockout, tap/click navigation, hold a character to approach and enter a close-up conversation study.
- **LiveGym**: separate voice studio with character selection, microphone streaming, captions, mute and explicit session close through the project-key backend. No SSO or API key in the Unity client.

Open `Unity/` in Unity Hub, then open `CharacterGym.unity` and press Play. Use the top-right button to switch gyms. The local Windows development build is `Unity/Builds/Windows/LucidLoopGyms.exe`.

See [gym setup and validation](docs/gyms.md) and [relay setup](server/README.md). Primitive characters and audio-driven mouth motion are temporary gym fixtures; final expressive character rigs and iPhone performance qualification remain production work.

## Git LFS

Binary art and production assets are tracked with Git LFS; see `.gitattributes`.
Install Git LFS before cloning, run `git lfs install`, and use `git lfs pull`
to download assets in an existing checkout. Commit the LFS pointer files with
their associated changes; `git push` uploads the binary objects through LFS.
