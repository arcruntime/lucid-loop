# Design source audit

Reviewed 2026-09-14. This records source coverage and engineering consequences for the [GDD](GDD.md) and [technical design](TECHNICAL_DESIGN.md). It is an extraction record, not a claim that the game or downloaded animations have passed acceptance.

## Authority and coverage

The [BTD Battlemap FigJam](https://www.figma.com/board/pCX5z7C3Izwe6sfIg53nC1/BTD-Battlemap?node-id=0-1) overrides the [Scratchpad Google Doc](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit). Draft/proposed labels remain provisional. The board's 25 selected objects were extracted and the layout inspected visually; no comment threads were visible in its comment panel.

All 14 visible Google Doc tabs were reviewed, including eight Art images and seven UI images. The all-tabs comment panel was reviewed; one collapsed reply in the interrogation discussion was not successfully exposed. The linked Character Models folder was identified but its model binaries were not audited in this design pass; character import belongs to the character artist. External articles linked inside research notes were not independently validated. Their model names, benchmarks, API examples, and contest rules are source notes rather than verified current technical or legal facts.

| Google Doc tab | Material extracted / treatment |
| --- | --- |
| Team Logistics | Collaboration context; personal contact details omitted from design artifacts. |
| Sync notes | September 12 investigator POV, Unity 6.3 LTS/mobile direction and non-interactable affair partner; September 13 opening/matrix proposals, Ren-first workflow and bug-report expectations. Serial-killer expansion remains post-demo. |
| Contest | Runtime generative AI, working demo, short explanation and track/OpenAI-use disclosure; material pre-existing components/licenses need attribution. Copied final deadline and earlier internal 22:00 target are distinct source dates, not newly verified submission advice. |
| AI and Tools | Production tool inventory, not compulsory dependencies. Looping music requires listening checks. |
| Tech Stack | Unity direction and DI library reference; earlier Godot/repository alternatives are historical. Resolved access comment is not an outstanding blocker. |
| Game Design | Full research/ideation text reviewed: small playable scope, camera/control/character feel, bounded generation and information propagation. Discarded settings, procedural mysteries and research benchmarks are not accepted feature requirements. |
| GDD (draft) | Fixed mystery, repeating club encounter, bounded director. Generic cast and obsolete mood vocabulary yield to Figma. |
| Character Guide | Personality, information behavior and proposed Ren reveal; conflicting age and POV details retained as conflicts. |
| Art | Eight visual references: layout, club look, four NPC sheets, player turnaround and background patrons; model-folder handoff identified. |
| Animation List | Followed workbook link and extracted all 25 rows, including status, priority, trigger, rough duration and references. |
| UI | Seven visual references plus text: mood lighting, beacon, interaction states, conversation variants/camera, pause and 2D portrait fallback. |
| Programming | Server truth versus filtered NPC context; on-demand voice lifecycle, context refresh, persistent character memory and validated consequential tools. Mansion examples are architectural examples only. |
| Submission Template | Working demo and explanatory submission content; not another gameplay subsystem. |
| Prompt for Astra | All four NPCs use Live, exact voice directions, automatic transcripts and confirmed player-fact context. Embedding DB is tentative. Placeholder VICTIM does not identify the victim. |

Comments add discussion about talking over a suspect, avoiding a technology-only demo, and prototype versus polished scope. They do not establish a new interruption mechanic. A mistaken character-name comment does not add a cast member.

## What must survive implementation

Figma establishes a spatial causal encounter: Maya recognizes Theo with the affair partner once per loop on entering the recognition region with visibility; default recording leads into the proposed approach/intervention/lethal/reset sequence. Mere presence in the confrontation region does not itself trigger violence. Maya follows until she accepts waiting or another action. Avoidance prepares a changed attempt; it is not the final objective. The proposed second loop must visibly change Maya's behavior while the player takes the left route to Luca or requests music.

Intimate and Aggressive change character dispositions, not truth. Maya becomes more reflective/discreet or impulsive/public; Theo's response also depends on recording, exposure and approach; Luca's disclosure and intervention remain contextual. Ren's sightline is relevant to the opening, but the board does not define universal hearing, offscreen death behavior or the final loop explanation.

Programming requires server-owned incident/timeline/discovery/progression state and filtered NPC projections. Preserve persona, goals, secrets, knowledge, beliefs, emotional state, relationships and prior interactions without giving everyone the solution. Session disposal must not erase same-loop memory. Loop reset is a separate boundary. The example tools reveal clues, record claims, change relationships and end conversations; implementing follow/wait, music and intervention needs additional validated actions. API event names in source snippets are not a verified transport contract.

Prompt for Astra adds a meaningful gap beyond short captions: ordered automatic conversation history and confirmed player knowledge supplied as appropriate context. Player knowledge, NPC claims and authoritative facts must remain distinguishable. Voice mapping is Luca/meridian, Maya/gleam, Ren/quartz, Theo/vesper. Embeddings are optional; a small authored fact store can satisfy the source intent.

## Visual and character details

| Reference | Build implications and limits |
| --- | --- |
| Club plan | Entrance, dance floor, DJ, bar, VIP, tables, restrooms, backstage and exit are visual zones. This does not authorize making every room playable in the demo. |
| Mood lighting | Intimate pink/violet and Aggressive crimson/UV guide the two active presets. Euphoric and Melancholic reference palettes are deferred. Keep faces, markers and captions legible in both active moods. |
| Player | Anonymous masked, dark-haired silhouette; green bobbing/pulsing beacon for navigation readability. |
| Maya | 26, photographer, pink hair, purple/black jacket and white trousers. Happy, teasing, concerned, serious, surprised and soft expressions. |
| Ren | 29 in art/prompt, short blonde hair, headphones, dark clothing and cap. Neutral, amused, skeptical, focused, alert and guarded expressions. Source explicitly limits her hearing; she trusts what she sees. Current user-confirmed production direction requires a separate toggleable cap. |
| Luca | 40, black shirt, burgundy apron, cloth over shoulder. Neutral, friendly, listening, doubtful, worried and distracted expressions. Hearing a report does not make it true. |
| Theo | Green suit, amber glasses and jewelry; welcoming, laughing, amused, talking, surprised and glasses-lowered expressions. Art/prompt say 30; Character Guide says 32. Age remains an authoring conflict with no simulation dependency. |
| Background patrons | Twenty visual variants are reference variety, not twenty required live AI characters. |
| Interaction | Speech-bubble/person and diamond-dot/object markers; outlined available, filled focused, dim out of reach; soft fades/pulses and one prompt at a time. Focus ring does not settle movement-destination semantics. |
| Conversation | Gold/black Art Deco identity panel, captions, text reply, microphone/send, History and Leave; optional framed 2D portrait. Overview-to-close camera remains in the club. Desktop E/Enter/V shortcuts need touch equivalents. |
| Pause | Resume, Settings, Controls, Main Menu and Quit shown. Define pause effects on simulation/voice and adapt platform-specific entries. |

The sample phone and conversation dialogue in mockups do not establish new authored evidence or story events.

## Animation extraction and production boundary

The [MVP Animation Handoff sheet](https://docs.google.com/spreadsheets/d/1pdyPs5SUAmrL_mCwHOMPpTHCXX6Ez9td/edit?gid=1109901444#gid=1109901444) contains 25 rows: 23 Ready, two To Do. All 17 unique Drive FBX files were downloaded; two rows are video references. [Original row data](../art/animation-handoff/sheet-rows.json), [file manifest](../art/animation-handoff/manifest.json), and the [readable handoff](../art/animation-handoff/README.md) preserve provenance, deduplication, checksums and inspection flags.

Do not treat suggested GIF names as actual formats. Listening rows reuse Maya walking; Maya's upset reaction points at Theo talking; player fast walk shares Luca's standard walk. One argument clip does not establish a synchronized two-person performance. The Theo open-reaction row remains To Do despite referencing a downloaded shared clip. Ren mixing is To Do with a video reference; the Ready rewind row is also a video reference, requiring an effect to be built. Maya recording and Luca intervention need deliberate animation coverage. Rough durations are authoring targets, not verified playable trims.

The character artist owns import, retargeting, facial performance and visual acceptance. User-confirmed rig direction is one shared male skeleton and one shared female skeleton, Ren on the female skeleton, with a separate Head/CapSocket accessory. Downloaded source skeletons must be adapted to those contracts; individual clips must not silently redefine character rigs. Binary metadata inspection is not deformation, root-motion, loop-seam or contact validation.

## Build decisions still needing authorship

The sources do not settle victim/lethal action, exact successful ending, set duration, escalation and disclosure thresholds, recognition occlusion, intervention prerequisites, music acceptance/cooldown, pause behavior, or Ren's cross-loop memory/reveal. The technical design names these as open decisions instead of allowing runtime AI to invent them. Spatial authority, protocol schemas, turn interruption and session recovery likewise require explicit integration decisions.

The repository already has useful movement and live-voice gyms. The [technical design's implementation evidence and acceptance gates](TECHNICAL_DESIGN.md) identify the work needed to join them into the authored encounter. Separate character-art progress is coordinated on the `lucid-loop` agent bus between `astra-lead-engineer` and `astra-character-artist`; this audit does not overwrite that team's assets or assert their acceptance status.
