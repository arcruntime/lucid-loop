# Lucid Loop

## Project direction

- Create the game as a Unity LTS project in a subfolder of this repository (`B:\lucid-loop`), rather than at the repository root.
- Use [osu-framework-unity-di](https://github.com/splatterfacegames/osu-framework-unity-di) as a core dependency for dependency injection.
- Choose the exact Unity LTS release and project subfolder name when project setup begins; neither is locked yet.

## Context

- Game design source: the Game Design Document, REWIND gameplay concept, and Character Guide sections of the [design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit).
- Exploratory work exists separately at `B:\openai-hackathon-game`.

## Current status

Project direction recorded only. Unity project creation and implementation are on hold pending further instruction.

## Git LFS

Binary art and production assets are tracked with Git LFS; see `.gitattributes`.
Install Git LFS before cloning, run `git lfs install`, and use `git lfs pull`
to download assets in an existing checkout. Commit the LFS pointer files with
their associated changes; `git push` uploads the binary objects through LFS.
