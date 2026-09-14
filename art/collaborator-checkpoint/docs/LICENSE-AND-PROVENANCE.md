# License and provenance record — approved MVP

Recorded September 14, 2026. This record describes the review branch, not every local experiment or later integration from another branch.

## Team confirmation

Arc confirmed directly on September 14:

- No code/assets from the separate `openai-hackathon-game` exploratory repository were copied into Lucid Loop; the team started fresh with this repository.
- Steph created the Figma character and club source art using ChatGPT, and the team has permission to use it.
- No team-authored material predates the official start, September 11 at 8 PM JST.

These statements come from the team, not an inference from commit dates. WORKLOG independently records that the exploratory study was not migrated. Git records the initial Lucid Loop commit on September 12 and the MVP work on September 13–14. Open-source dependencies remain pre-existing and are disclosed separately.

## Direct dependencies and evidence

| Component | Version/reference | License / evidence |
|---|---|---|
| Unity / Unity packages | 6000.3.24f1; Unity/Packages/packages-lock.json | Applicable Unity terms and individual package notices. Do not describe all Unity packages as MIT. |
| osu-framework-unity-di | e68a6e4a2c3ac3d2ea4b42e50b1000bceec1df29 | MIT; [preserved pinned upstream notice](licenses/osu-framework-unity-di-MIT.txt). [Upstream](https://github.com/splatterfacegames/osu-framework-unity-di/blob/e68a6e4a2c3ac3d2ea4b42e50b1000bceec1df29/LICENSE). |
| R3 | dc69078430149db8cd93069f502cefe148745b4b | MIT; [bundled notice](licenses/R3-MIT.txt). |
| ws | 8.21.0 | MIT; [installed-package notice](licenses/ws-MIT.txt). |
| Newtonsoft.Json | Unity package 3.2.2 | MIT for Newtonsoft and the listed third-party components; [notices](licenses/Newtonsoft-third-party-notices.md). Unity packaging uses the [Unity Companion License](licenses/Unity-Newtonsoft-license.md). |
| Node.js | Node 24 server runtime | MIT with bundled third-party notices; runtime is installed separately. [Official notice](https://github.com/nodejs/node/blob/v24.0.0/LICENSE). |
| Microsoft/.NET support libraries | Bundled Unity/Assets/Plugins/R3 manifests | Individual LICENSE and THIRD-PARTY-NOTICES files preserved in that directory. |

## Content and tools

- Original source images: team-owned/authorized per Arc, created by Steph with ChatGPT. Source export manifest and hashes: art/source/manifest.json. Figma was used for the design board.
- Portrait atlas and club paintings: generated from those sources with OpenAI image generation; prompts retained in ART-PROMPTS.md. Service terms apply; do not invent a CC0/MIT license for these outputs.
- Opening speech: cached OpenAI-generated cast lines; live speech/decisions use the OpenAI API. No secret credentials are included.
- Music and backspin: original procedural generation in tools/audio/generate_placeholders.py, no third-party samples.
- Codex assisted implementation, debugging and generation; CapCut planned for the final edited video. Tools/services have their own applicable terms.
- Quaternius rigs/clothing experiments, Steph animation downloads and nested empty Unity projects are local experiments not included in this published MVP branch. If later versions add them, update the disclosure and retain their actual licenses before submitting that version.
- No external dataset was introduced by the MVP pass. The earlier MusicalDM project was inspected for observer presentation ideas; its implementation was not copied into this branch.

The submission's short disclosure is in SUBMISSION-DRAFT.md. Preserve notices with the source handoff. Recheck the final asset/dependency set if Jetha combines this checkpoint with later gym/art work.
