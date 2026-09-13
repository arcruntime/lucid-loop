# Lucid Loop worklog

## 2026-09-12 — Foundation, design intake, and art collection

### Agreed foundation

- Build in `B:\lucid-loop`, with a **Unity 6.3 LTS** project in a subfolder rather than at the repository root. The gym implementation now uses Unity 6000.3.24f1 in `Unity/`.
- Target **phones first**, in **landscape orientation**, with a **16:9 target aspect ratio**. The minimum-spec phone is **iPhone 15 Plus**.
- Use [osu-framework-unity-di](https://github.com/splatterfacegames/osu-framework-unity-di) as a core dependency for dependency injection.
- Use the nightclub environment and character designs collected from the team's FigJam board.
- Use OpenAI's live API. This records the user's stated technology direction; the exact API/product surface, model, transport, and integration design are not yet selected or verified.
- Game design remains in flux. The environment, characters, and live API are the current fixed foundation; gameplay details below are proposals rather than final requirements.
- Project creation and implementation remain on hold pending further instruction. Documentation, reference collection, and repository setup were authorized and completed.

### Design sources read

Read the Game Design Document [Draft], REWIND — Gameplay Concept, and Character Guide [Draft] sections of the [team design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit). The [character guide tab](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit?tab=t.tgzadh94t25y) was explicitly identified by the user.

The shared premise is a nightclub time-loop mystery: music changes emotions, which changes behavior, information flow, relationships, and the chain of events leading to a catastrophe. The player gathers knowledge across loops and intervenes to prevent the murder or attempted murder and survive the set.

The concise GDD uses the tentative title **Before the Drop**. It proposes an investigator player, trusting/aggressive room moods, catastrophe-triggered resets, and a persistent journal/clue board. Its MVP scope is one space, five characters, two moods, one catastrophe type, a loop reset, and an AI call that changes real game state.

The **REWIND** concept instead makes Ren the playable DJ. She mixes euphoric, intimate, aggressive, or melancholic music, sets a drop-point checkpoint, activates auto-mix to leave the booth, investigates and influences conversations, then returns to rewind while retaining knowledge. Its proposed loop is DJ → explore → discover → manipulate → rewind → try again.

The GDD's proposed AI director resolves consequences from authored character rules and current mood, location, knowledge, trust, evidence, and actions. Outputs would be constrained to engine-supported actions and state changes, with no invented facts, characters, motives, or catastrophe types. This contract has not been implemented or reconciled with the live API direction.

### Character context

Visual designs are agreed references; narrative details remain draft.

| Character | Draft role and behavior |
| --- | --- |
| Maya | Close friend and initial witness; warm, playful, loyal, emotionally trusting; may reveal information under pressure. |
| Ren | Charismatic, observant DJ; trusts what she sees. May be the player or the person controlling the loop, depending on the design chosen. |
| Luca | Calm, sharp, guarded bartender; collects fragments of gossip and serves as an information hub. |
| Theo | Charming, manipulative regular/socialite; spreads information strategically. His exact involvement in the incident remains open. |
| Anonymous player avatar | Visual turnaround exists on the board; its use depends on the eventual player POV. |

### Open design decisions

- Investigator versus Ren as the player; Ren's relationship to the loop and whether it is a late reveal.
- Exact gameplay loop, player verbs, music mechanics, and time manipulation.
- Two moods in the concise MVP versus four in REWIND.
- Automatic catastrophe resets versus player-created drop points; what state persists across rewinds.
- Final cast/incident roles: the GDD lists five roles, while the character guide names four characters plus a player.
- Theo's incident connection, character secrets, motivations, and the final catastrophe chain.
- Final title, camera, demo scope, AI schema, and live API integration.

The user's subsequent Unity 6.3 LTS decision resolves the earlier document's Unity/Godot question. Phone-first targeting, landscape orientation, a 16:9 target aspect ratio, and an iPhone 15 Plus minimum-spec phone are now fixed requirements. The environment and character selections are also explicit, even though the documents remain drafts.

### Exploratory art work reviewed

`B:\openai-hackathon-game` is the separate exploratory repository. Reviewed its original character sheet, prepared A-pose, baseline/refined Unity captures, and `docs/ART_DIRECTION.md` and `docs/ASSET_GENERATION.md`.

Existing pipeline: model sheet → ImageGen front A-pose → Meshy reconstruction → reference-guided retexture → focused face repair projected and baked in Blender → Unity URP realtime shading.

The Marvel Tokon-style study uses separate light/shadow palettes, hard diffuse transitions, selective highlights, inverted-hull outlines, and specialized face-lighting controls. The refined renders visibly improve eyes, neutral expression, hair color, and distracting under-eye shadows. Remaining issues include hair/fur edge noise, ears and side-face quality, and baked clothing marks.

The exploratory documentation reports Unity 6000.3.24f1 / URP 17.3.0, a roughly 41.5k-triangle unrigged model, and 4k refined color textures. These are prior-study details, not newly selected project settings. Lucid Loop's phone requirements were set independently; the exploratory build results have not been revalidated for Lucid Loop.

The study inspected a supplied Tokon reference for technique; its notes state that reference geometry and textures were not copied into the generated character. This study has not been migrated into Lucid Loop. The FigJam board separately labels its character-sheet direction as Arcane-inspired; a final rendering specification has not been locked.

### FigJam art saved

Source: [team FigJam board](https://www.figma.com/board/DpY7FoS77Uq9LAobMj47aN/Untitled). No direct Figma connector was available; the board was accessed through the user's signed-in Chrome session and exported using its UI.

Saved eight full-resolution embedded originals, without resampling or re-encoding:

- `art/characters/`: Maya, Ren, Luca, and Theo model sheets; anonymous player avatar; cast lineup.
- `art/environments/`: nightclub interior concept and nightclub isometric layout.

Also saved `art/source/figma-board.jam`, the complete `art/source/figma-board.png` (6286 × 12384), and `art/source/manifest.json` with original archive member names, dimensions, and SHA-256 hashes. Duplicate thumbnails remain in the archive and were omitted from the extracted design folders. All eight extracted images and their hashes were verified; the complete board image was also checked for image integrity.

The full board preserves its environment legend, movement/layout notes, character table, and annotations. Its roughly 45-degree high-angle/isometric gameplay camera plus close-up AI conversations remains a source proposal. See [the art index](art/README.md) for links.

Existing `art/ren.png` and `art/concepts.png` were retained and included in the initial commit alongside the exports.

### Git and LFS setup

- Remote: [arcruntime/lucid-loop](https://github.com/arcruntime/lucid-loop), via `git@github.com:arcruntime/lucid-loop.git`.
- Initialized Git LFS locally and added `.gitattributes` for image/source-art formats, models, audio, video, design archives, and binary packages. Text remains in ordinary Git.
- Added clone/download guidance to `README.md`.
- Initial commit: `88740dbb16d5c495c0d3dee7335e334ce8b92c0c` — `Add project direction and design references with Git LFS`.
- Uploaded all 12 LFS objects, about 68 MB. The first Git connection closed while LFS uploaded; the upload completed, and retrying the branch push with SSH keepalives succeeded.
- Verified `origin/main` matched the local commit and the working tree was clean immediately after that push. Local LFS integrity and staged whitespace checks passed.

### Status when writing this log

Reference collection and the initial repository push are complete. No Unity project, live API integration, gameplay implementation, rigging, or art-pipeline migration has been performed in this repository during this work.

An untracked `research/` directory was present when this log was started. It was not inspected, changed, or included in the earlier initial commit by this task. This worklog is a new local documentation change; the verified push above predates it.

## 2026-09-12 — Two gyms and live authentication requirement

The user confirmed that the character/navigation gym must remain offline and requested a second gym for live conversation. The desired second-gym flow is OpenAI SSO using each player's own ChatGPT quota.

Updated the character-gym spec and recorded the separate live-gym requirement under `docs/superpowers/specs/`. Official GPT-Live documentation currently describes project API-key authentication and API billing; the checked ChatGPT sign-in documentation applies to Codex surfaces. A public third-party SSO entitlement for GPT-Live was not established. Hackathon-specific SDK/access documentation may resolve this dependency. No substitute billing flow was selected, no credentials were accessed, and neither gym has been implemented yet.

## 2026-09-12 — Build the offline and live gyms

The user withdrew the earlier SSO/ChatGPT-quota requirement and approved a project API key stored as the GitHub repository secret `OPENAI_API_KEY`, with no SSO. They then explicitly authorized building both gyms.

Created `Unity/` with Unity 6000.3.24f1 (Unity 6.3 LTS), URP 17.3 and the core osu-framework-unity-di dependency pinned to e68a6e4a2c3ac3d2ea4b42e50b1000bceec1df29. Its R3 prerequisites are pinned and included with NuGet manifests/licenses; managed DLLs use Git LFS. The DI root supplies the offline interaction session through the package's generated hierarchy-based resolution.

CharacterGym is an offline 3D nightclub blockout based on the saved environment references: central magenta dance circle, raised rear DJ stage and steps, left cyan bar, right VIP seating, entrance, lights and temporary partygoers. Click/tap navigation follows a baked NavMesh. Holding a named character approaches them, stops at conversation distance and blends into a close-up with their model sheet and labeled sample dialogue. The overview uses a 45-degree pitch. Other characters are hidden during the solo conversation study and restored on return. Maya, Ren, Luca, Theo and the player use replaceable primitive stand-ins.

LiveGym is a separate voice studio with character selection, runtime relay address/access token, microphone permission, streaming PCM playback, captions, mute acknowledgment and explicit close handling. The Node 24 relay owns prompts and the OpenAI key; Unity never receives that key. It bounds pending connections, upstream sessions, messages and session duration. Scene exit/app suspension stops local capture and releases the connection. The relay continues a bounded graceful close to collect final usage after client disconnect. No automatic reconnection is performed.

Added a Docker image and GitHub Actions backend checks. An opt-in upstream smoke job references the assumed `OPENAI_API_KEY` secret, but no secret was read and no real OpenAI request was made. GitHub secrets do not themselves host the runtime backend; no public deployment target was supplied or deployed. Local fixture transport testing is separate from real voice quality and billing verification.

The user also requested deleting the superseded Arkane Studios research and pushing that deletion. Removed its report and remaining research cross-references while retaining Arcane/Fortiche research. Commit 25b2e16 contains the deletion; origin/main was verified at aaa5c15 after preserving an incoming voice-casting commit. Gym work was excluded from that push.

See [gym launch and validation instructions](docs/gyms.md). Windows builds and raw logs/captures are local ignored artifacts under `Unity/Builds/` and `.local/`. Final expressive English lip-sync, final hero/environment art and measured iPhone 15 Plus sustained 30 fps remain separate production work.

Validation: Unity EditMode suite passed 8/8 (gesture discrimination/cancellation and PCM buffer conversion/bounds); Windows development build succeeded. The built-player acceptance run verified DI resolution, baked navigation to all four characters including the stage, arrival-triggered conversations, player visibility restoration, scene switching, and Unity WebSocket startup/audio/captions/graceful close against the free local fixture. Gameplay, all four close-ups and the live studio were captured and visually inspected. Backend suite passed 23 local tests with one explicitly skipped real OpenAI smoke test; Docker build passed. Independent review reproduced and then verified fixes for graceful shutdown races, unauthenticated connection bounds, invalid character identifiers and malformed-frame rejection. Physical touch, microphone recording/playback quality, real OpenAI connectivity and phone performance were not qualified.

## 2026-09-13 — Continuous desktop gym builds

The user requested a README update and continuous Windows/macOS builds on the existing stadia-testbed Windows VM and Mac mini infrastructure. Added a dedicated Unity builds workflow for main pushes, same-repository PRs, daily runs and manual dispatch, with isolated platform jobs, EditMode test gating, build-file verification, packaged players, commit metadata, checksums and retained diagnostics. The build-only Unity entry point consumes the committed gym scenes rather than regenerating them.

Inspected the hosts directly: stadia-testbed runs the dockurr win-soccer Windows VM; existing Windows and Mac runners are scoped to other repositories. Lucid Loop runner registration requires repository admin access, while the connected account currently has push access only. The VM lacks Unity at the standard install path. The Mac's prior Unity volume is unmounted and its internal disk has about 2.4 GiB free. No runner was re-registered away from another project, no disk repair was attempted and no remote successful build is claimed. Requested repository admin access and a usable Mac storage target; see docs/CI.md for the concrete runner/Unity prerequisites.
