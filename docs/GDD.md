# Before the Drop — Game Design Document

**Working title:** Before the Drop  
**Project:** Lucid Loop  
**Synthesis date:** September 14, 2026  
**Status:** Consolidated design baseline; unresolved decisions are called out below.

## 1. Source authority and reading guide

Companion documents: [technical design and build scope](TECHNICAL_DESIGN.md), [source coverage audit](DESIGN_SOURCE_AUDIT.md), and [downloaded animation handoff](../art/animation-handoff/README.md).

**Authored demo baseline:** After this source synthesis, the user authorized filling the missing story decisions. [A Quiet Way Out](DEMO_SCENARIO.md) specifies Luca as the victim, Theo's shove/fall sequence, and an active mediation route. Where this document calls those decisions unresolved, that describes the original sources; the new scenario supplies the implementation baseline. It is clearly labeled new design rather than attributed to Figma. iOS is the primary platform; [iOS rendering policy](IOS_RENDERING.md) limits feature variants while preserving the two moods.

This GDD synthesizes the [BTD Battlemap FigJam board](https://www.figma.com/board/pCX5z7C3Izwe6sfIg53nC1/BTD-Battlemap?node-id=0-1) and the [Scratchpad Google document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit?tab=t.0#heading=h.qv84v78m0tc3). **Figma takes precedence wherever the sources disagree.** The board's own labels—draft rules and proposed opening for team review—remain relevant: source precedence does not mean every proposal has received final approval.

Source references used throughout:

- **[F]** Figma: battlemap, Draft Rules, Starting Loops, AI adjudication note, and Music/Mood Matrix.
- **[D]** Google Doc: GDD (draft), Character Guide (draft), Game Design, and September 12–13 Sync notes.
- **[P]** Google Doc: UI, Art, Animation List, Tech Stack, and Programming tabs.
- **[R]** [Repository README](../README.md): current project constraints and implementation context, included separately from the source design.

Unqualified gameplay descriptions below consolidate the sources. **Proposed implementation** identifies details added to make the design actionable; **Open** identifies a decision the sources do not settle. Research examples and discarded concepts in the scratchpad are not requirements. In particular, the mansion characters in Programming are architectural examples, not this game's cast.

## 2. Game overview

Before the Drop is a single-player social mystery set during one repeating night in a nightclub. The player arrives with Maya, who notices Theo with his affair partner and records them. Exposure provokes a confrontation; Luca intervenes; a lethal outcome in DJ Ren's view sends the night back to its beginning. On subsequent attempts, the player uses retained knowledge, movement, conversation, and music requests to change what people do before the confrontation unfolds. [F, D]

The mystery has authored facts. Runtime AI interprets the player's language and adjudicates character responses within those facts and legal game actions. Persuading Maya to wait must make her remain in place; changing the music must alter the conditions under which a conversation occurs. Dialogue becomes gameplay through observable consequences. [F, D, P]

**Player fantasy:** Read the room, learn what makes people act, and use another chance to prevent a disastrous night.

**Overall objective:** Resolve the dangerous encounter and reach the end of the DJ's set without murder or the selected catastrophe. The older GDD specifies surviving the set; the board explicitly says avoiding recognition is not enough to complete the objective. The exact resolution predicate is still open. [F, D]

**Format:** Small, replayable, mobile-first Unity game with an isometric club view and character conversations. Investigator POV, Unity, and mobile were decided in the September 12 sync. [F, D]

## 3. Design pillars

1. **Knowledge is progression.** Repetition teaches the player about evidence, character motivations, timing, and the consequences of disclosure. A journal or retained-clue display carries learning across resets. [D, F]
2. **Words change actions.** Runtime interpretation produces a visible change in behavior, knowledge, or relationships; conversation alone is insufficient. [F, D]
3. **Music changes the social context.** The same approach can land differently under Intimate or Aggressive music. Character identity, evidence, exposure, and the player's approach still matter. [F]
4. **Consequences are legible.** Recognition, recording, approach, intervention, and catastrophe form a readable causal chain. Crossing an invisible zone cannot independently kill someone. [F]
5. **Concentrate the experience.** One polished club encounter establishes the hook, failure, rewind, and a materially different second attempt. Larger mysteries and extra spaces follow only after this works. [F, D]

## 4. Setting, camera, and spatial design

The playable space is one nightclub containing a dance floor, DJ stage/booth, bar, and the affair encounter near a VIP seating area. The board depicts additional tables and patrons as context; these do not imply separate gameplay systems or a larger playable level. Entrance, mezzanine, and backstage exploration remain stretch content under the earlier GDD. [F, D]

| Location or region | Design purpose |
| --- | --- |
| Start/reset point, lower entrance to the room | Places the player and Maya together at the beginning of an attempt. |
| Direct approach toward Theo and the affair partner | Establishes the first-loop recognition route. |
| Left-side route toward the bar | Gives later attempts preparation time before approaching the encounter. |
| Recognition area | Maya can recognize Theo and the affair partner when the visibility conditions are met. |
| Confrontation/catastrophe area | Stages the social escalation and intervention; entry alone does not trigger violence. |
| Luca's bar position | Supports direct questioning and a readable route to intervene. |
| Ren's booth and sightlines | Supports music requests and visibility of the proposed lethal opening. |

The Figma arrows convey causal routes and sightlines, not final coordinates, collider sizes, pathfinding data, or a complete visibility algorithm. Ren is positioned above the floor, Luca on the left, and Theo and the affair partner to the right. [F]

**Presentation direction:** Preserve the board's readable isometric arrangement and use close character framing for expressive conversations. Maintain sufficient room context for players to understand who moved and who could witness an event. Exact camera transitions remain to be specified. [F, P]

## 5. Player verbs and controls

| Verb | Player intention | Required observable consequence |
| --- | --- | --- |
| Move / approach | Choose a route, reach a character, or prepare before recognition | Player changes location; Maya follows unless her behavior has been changed. |
| Talk / ask | Learn facts, make requests, explain, conceal, or disclose | Character responds in context; any accepted gameplay consequence changes validated state. |
| Ask Maya to wait or do something else | Separate her movement from the player's route | On agreement, Maya changes behavior visibly rather than continuing to follow. |
| Request music from Ren / the booth | Change the encounter's social conditions | An accepted request changes the active music/mood and its presentation. |
| Use information or evidence in conversation | Affect belief, trust, or perceived exposure | Only the involved or witnessing characters receive the relevant knowledge. |
| Review retained clues | Plan a different attempt | Player can recall what was learned without repeating the same discovery. |

The board explicitly supports clicking Ren/the booth to request music and natural-language requests to Maya. It does not specify an inventory interface, physical evidence-dragging mechanic, or player-operated recording control. Maya's recording is the default opening beat. [F]

**Current repository controls:** Click/tap to walk and hold a character to approach and talk are implemented in the character gym. These are the starting control convention, not proof that the full encounter exists. [R]

## 6. Core gameplay loop

1. Begin at the club's reset point with Maya and the current loop's initial state.
2. Explore and talk; learn evidence, relationships, and timing.
3. Choose an approach: who should wait, who should hear something, whether to request different music, and when to approach Theo.
4. Resolve player requests through runtime AI constrained by authored character rules.
5. Execute accepted actions in the world and show their consequences.
6. If the authored catastrophe occurs, rewind the encounter and retain player learning.
7. Retry with a different intervention; resolve the danger and survive the set to win. [F, D]

### 6.1 Proposed opening: demonstrate failure and rewind

The board proposes the following introductory sequence, potentially guided to ensure the rewind is demonstrated:

**Player + Maya approach → Maya records → Theo approaches Maya → Luca intervenes → lethal outcome in Ren's view → reset.** [F]

The opening begins with **Aggressive** music and no retained clue. The sources do not identify the victim, exact lethal action, or final responsibility for the death. Do not invent these through AI generation. The opening's degree of player control is also undecided. [F]

### 6.2 Second attempt: demonstrate agency

The board proposes taking the left route, requesting music and/or asking Maya to wait, then approaching the encounter under changed conditions. Its concrete AI example is a request for Maya to remain while the player speaks to Luca. If she agrees, she stays while the player moves. [F]

The second attempt must show a changed intended action and its execution. It need not guarantee that every polite request succeeds or that Intimate music automatically prevents catastrophe.

### 6.3 Encounter rules

- Recognition triggers **once per loop**, when Maya enters the recognition area and can see **both Theo and the affair partner**.
- Maya follows the player unless she agrees to wait or perform another action.
- Avoiding the recognition area buys preparation time; it does not complete the objective.
- Entering the confrontation area alone does not trigger violence. Character actions do.
- The proposed opening's lethal outcome occurs in Ren's view. The board does not establish what happens to catastrophes outside her view. [F]

**Proposed implementation:** Track recognition as a per-loop flag. Keep recognition, recording, perceived exposure, threatening behavior, intervention, and catastrophe as distinct events. This prevents a location trigger from silently substituting for the social sequence.

## 7. Cast and information behavior

The playable investigator is separate from Ren. The encounter uses **four named interactive NPCs—Maya, Ren, Luca, and Theo—plus a non-interactable affair partner**. This reconciles the board and September 12 decision with the older generic five-character list. Theo's spouse belongs to the affair's background but is not established as an additional interactive demo NPC. [F, D]

| Character | Role and personality | Information and encounter behavior |
| --- | --- | --- |
| Player | Investigator; Maya's friend; anonymous avatar direction | Retains learning between attempts and changes the sequence through movement and social intervention. |
| Maya, 26 | Warm, playful, loyal companion | Trusts emotionally; recognizes the affair when the trigger conditions hold; records in the default opening; can be persuaded to wait, approach, or speak differently. |
| Ren | Charismatic, observant, cool DJ | Receives music requests; watches the room from the booth; witnesses the proposed catastrophe. The character draft links her to control of the loop. |
| Luca, 40 | Calm, sharp, guarded bartender | Holds fragments of gossip rather than the whole truth; may share limited information when asked directly; may intervene in a physical threat. |
| Theo, 32 | Charming, manipulative socialite/regular | Directly connected to the affair; reacts to recording, exposure, and approach; has the highest violence potential in the character draft. |
| Affair partner | Non-interactable encounter participant | Must be present and visible with Theo for Maya's recognition. Name, motives, and further behavior are unspecified. |

Maya's draft description of knowing the incident at loop start is superseded by the board's explicit recognition condition. Luca's intervention is compatible with his low violence potential: intervening does not establish him as the aggressor. [F, D]

**Ren's unresolved story role:** The character draft proposes a late reveal that Ren secretly controls the loop; the older GDD leaves grounded DJ versus knowing orchestrator open. The board supports her music and sightline roles but does not settle the full reveal. Keep the demo's visible rewind while leaving the explanation uncommitted.

## 8. Music and mood

The demo has two moods: **Intimate** and **Aggressive**. The board's Intimate replaces the older GDD's Trusting label. Music influences disposition and perceived privacy; it does not replace evidence or individual character rules. [F]

| Character | Intimate | Aggressive | Observable consequence |
| --- | --- | --- | --- |
| Maya | More reflective; more receptive to a discreet approach | More impulsive, outspoken, and inclined to confront | Player requests can affect waiting, approaching, or speaking publicly; recording remains the default opening beat. |
| Theo | Easier to address calmly; audible accusations can still raise exposure anxiety | More defensive and forceful; loud music may make him feel less observed | Response depends on recording, perceived exposure, and approach as well as mood. |
| Luca | More willing to share limited information when asked directly | Remains guarded and becomes alert to threatening behavior | May disclose a fact or intervene when someone is physically threatened. |

The UI notes connect mood to lighting: **Intimate uses rose pink and violet with selective green contrast; Aggressive uses crimson red and ultraviolet purple.** Euphoric and Melancholic appear in the UI explorations but are outside the two-mood demo. [P, F]

**Proposed implementation:** Make music, lighting, and the displayed mood agree when a transition completes. Provide a text/icon mood cue in addition to color. Do not silently change character rules before the player can perceive the transition.

**Open:** Music-request acceptance rules, transition duration, cooldowns, and whether Ren changes tracks autonomously in the demo. The older concept's room-reading DJ must not obscure the board's explicit player request mechanic.

## 9. Evidence, knowledge, and loop persistence

The affair recording is the central concrete evidence beat. Track who has observed the affair, who knows about the recording, and who perceives a threat of exposure. Hearing an accusation and witnessing the affair are different sources of knowledge. Theo must not gain global awareness of everything the player tells Luca. [F, D, P]

The journal/clue board persists across loops in the older GDD; the battlemap HUD explicitly includes a retained clue. The minimum demo can use a compact retained-clue presentation rather than a full investigation-board interface. [D, F]

**Proposed implementation of reset boundaries:**

| Reset each attempt | Retain between attempts |
| --- | --- |
| Character positions and current actions | Loop count |
| Recognition flag and opening event progress | Authored clues the player has discovered |
| Current-loop recording state | Player-facing knowledge of the prior outcome |
| Temporary trust, exposure, and escalation changes | Journal entries grounded in observed events |
| Music and set progress, restored to the configured starting state | Nothing else unless explicitly authored |

A retained memory of a recording is not automatically possession of the physical recording in the new loop. Likewise, the player's retained knowledge does not mean Maya, Theo, or Luca remembers prior attempts. Both are important distinctions to preserve unless the story explicitly establishes exceptions.

**Open:** Exact clue list, wording of the first retained clue, physical evidence persistence, and Ren's memory across loops.

## 10. Runtime AI and authoritative simulation

The AI's essential job is to interpret a player's request in context, decide a character's response within authored rules, and propose an observable legal action. The canonical demonstration is Maya agreeing or refusing to wait and then behaving accordingly. [F]

The game server owns authoritative world state. Model output must not create new facts, characters, motives, evidence, or catastrophe types. The director can reason about the room; an individual speaking NPC receives only the knowledge and beliefs appropriate to that character. The Programming tab recommends on-demand voice sessions reconstructed from persistent character state, with server validation of consequential tool calls. Sections 10.3–10.6 preserve that implementation direction; they do not assert that it is already implemented. [D, P]

### 10.1 Responsibility split

| AI interpretation and performance | Authoritative game simulation |
| --- | --- |
| Interpret open language and conversational approach | Store authored truth, clues, positions, and per-character knowledge |
| Evaluate a request against personality and current context | Enforce visibility, movement, prerequisites, and allowed actions |
| Propose bounded trust/knowledge changes and an NPC action | Validate and commit changes, then execute them |
| Generate in-character dialogue consistent with the accepted result | Decide catastrophe, reset, progression, and victory |
| Explain the decision through an internal reason code | Preserve a trace that can diagnose inconsistent outcomes |

### 10.2 Proposed adjudication contract

This is a design contract, not a finalized API schema or claim about implemented tools.

**Input:** Loop identifier and state revision; active mood; relevant positions and visibility; current actions; character personality and disclosure rules; character-specific knowledge; evidence/recording state; bounded relationship and escalation state; recent player action; currently allowed actions and their prerequisites.

**Output:** Character response; interpreted intent; accepted/refused request; one permitted action with valid actor and target; bounded state-change proposals; references to existing facts/evidence; reason code; input state revision.

**Illustrative legal actions:** Continue following, wait, approach an existing character, disclose an existing fact, speak publicly, intervene, or request an existing mood. The final list and exact eligibility rules remain open; a model cannot expand that list at runtime.

**Proposed validation:** Reject unknown identifiers, impossible movement, unearned facts, excessive state changes, and results generated for an obsolete loop/state. Commit each result once. Dialogue and animation must agree with the committed action: Maya must not say she will wait while continuing to follow.

**Fairness:** Identical model outputs are not guaranteed. Authored constraints, bounded consequences, and visible causes should make outcomes consistent enough to learn. Do not promise strict language-model determinism. [D]

**Proposed failure behavior:** On a timeout or invalid response, preserve the last valid state and offer a clear retry. Do not fabricate a successful request or trigger catastrophe because inference failed. Handling of simulation time during inference must be decided alongside the set timer.

### 10.3 Server-owned truth and per-NPC context

The Programming tab separates **authoritative game state** from **NPC context**. The server stores the actual incident, timeline, discovered clues, alive/dead state, player inventory, and progression. A character's conversational context contains what that character knows, believes, conceals, wants, and feels. The voice model is a performer and interpreter, not the mystery's database. [P: Programming]

Construct an NPC-specific projection before starting a conversation. Do not send the entire solution to every NPC and rely on an instruction telling them not to reveal it. If Luca does not know a fact, omit that fact from his context; a mistaken belief can still appear as a belief rather than truth. The source recommends including these fields: [P: Programming]

| Context field | What it supplies |
| --- | --- |
| Role | Character identity and place in the encounter |
| Objective | What the character wants from this conversation |
| Personality | Speech style, temperament, and mannerisms |
| Known facts | Authored facts this character has learned |
| Beliefs | What the character thinks, including possible errors |
| Secrets | Known information the character wants to conceal |
| Lies | Claims the character is willing to make despite knowing otherwise |
| Current situation | Relevant time, location, mood, recent events, and evidence |
| Disclosure rules | Conditions under which particular information may be revealed |
| Game rules | Stay in character; preserve established facts; do not invent evidence, reveal private instructions, or claim inaccessible knowledge |

The source also includes relationships, prior player interactions, and relevant player discoveries in the context-building examples. **Proposed integration rule:** Filter those discoveries through what the NPC could observe or has been told; the player's journal is not automatically shared knowledge. [P: Programming; integration with F]

### 10.4 On-demand voice-session lifecycle

The Programming tab recommends the following flow for Realtime/Live conversations: [P: Programming]

1. The player selects or approaches an NPC.
2. The backend loads canonical world state and that NPC's persistent memory.
3. It builds the filtered NPC context and supplies it as session instructions **before the player begins speaking**.
4. It creates/configures the voice session and connects the player's microphone.
5. During conversation, the model performs the character and proposes game-relevant consequences through validated tools.
6. When relevant world state changes, the backend refreshes the session instructions so subsequent responses account for the new situation without requiring a new conversation.
7. When the conversation ends, the backend persists validated consequences and relevant character memory, then destroys the session.

The source illustrates instruction setup and refresh with `session.update` and `session.instructions`. These names are retained as source implementation notes, not a transport-complete API recipe; the source itself cautions that exact session/event details depend on the connection. Final integration must use the project's selected API and transport contract.

**Proposed integration with the encounter:** Refresh an active conversation when an accepted music change alters the mood, the NPC witnesses recording or a threat, or an allowed disclosure changes knowledge. Resolve accepted actions while they matter to gameplay; do not postpone Maya's wait action until an arbitrarily long conversation has ended. Invalidate an active session on loop reset so it cannot keep speaking or submitting actions from the previous attempt.

### 10.5 Disposable conversations, persistent character memory

A later conversation should reconstruct the same character from stored state rather than depend on keeping a large audio/chat session alive. The Programming tab's memory example stores prior topics, claims made, whether those claims were lies, the character's attitude toward the player, trust, and secrets already revealed. This lets an NPC remember an earlier exchange and remain consistent when approached again. [P: Programming]

For example, Luca can retain that the player asked about the incident, which authored fragment he disclosed, and how his attitude changed. Reopening his voice session must preserve that history; it must not reset his trust or let him contradict the established disclosure simply because the model session is new.

**Loop boundary:** Persistence between conversations is distinct from persistence between time loops. The Programming tab describes the former; it does not grant every NPC memory of prior loops. Apply section 9's reset policy to ordinary NPC memory, preserve the player's retained clues separately, and leave any Ren exception subject to the story decision.

### 10.6 Server-validated tools for consequential actions

The Programming tab explicitly recommends tools instead of allowing voice output to mutate critical mystery state directly. Its example tool surface is: [P: Programming]

| Source example | Intended responsibility of the server |
| --- | --- |
| `reveal_clue(clueId)` | Decide whether an existing clue is eligible for disclosure before recording it as revealed. |
| `record_npc_claim(claim)` | Persist what the NPC claimed without making the claim authoritative truth. |
| `change_relationship(npcId, delta)` | Validate the target and proposed relationship change before committing it. |
| `end_conversation(reason)` | Handle the conversation's completion and associated lifecycle/state updates. |

These are source examples, not finalized endpoint names. Waiting, following, approaching, intervention, and music requests need equivalent validated action handling to realize the Figma encounter. Tool names, argument schemas, and whether voice and adjudication run in one model session or separate services remain implementation decisions.

The essential division is **model = acting, dialogue, and interpretation; server = truth, rules, and progression**. A plausible spoken admission is not sufficient to unlock a clue unless the authoritative rules permit it. Store a lie as a character claim, not as a rewritten world fact. [P: Programming]

## 11. Interface, art, animation, and audio

The minimum play interface communicates the current loop, current mood/music, retained clue, available interaction, and the outcome of a request. These combine the board's HUD labels and gameplay requirements with the Google Doc's interaction, conversation, transition, and pause-menu explorations. [F, P]

**Proposed HUD:** Loop number; Intimate/Aggressive label and icon; retained-clue access; an understandable set-progress indicator once the timer rules are settled. Show player-facing consequences, not internal model scores or reason codes.

**Conversation presentation:** Clear speaker identity, readable text alongside speech, and a visible listening/responding state. The UI notes allow a 2D portrait fallback if the 3D models are unsuitable and request a symmetrical portrait frame. Mockups include name/role, captions, typed reply, microphone/send, History and Leave controls, and a transition from club overview to close conversation. Enter to send and hold V to speak are desktop examples requiring mobile equivalents. Prompt for Astra explicitly asks for automatic conversation transcripts and a store of facts the player knows or confirms; embedding storage is a suggestion, not a required implementation. [P]

**Interaction and pause references:** A green bobbing/pulsing player beacon remains readable across moods. Speech bubbles identify people; diamond/dot markers identify objects. Outlined, filled, and dim states distinguish available, focused, and out-of-reach interactions, with one prompt at a time. Pause mockups show Resume, Settings, Controls, Main Menu, and Quit; platform behavior must be specified before implementation. Mockup dialogue and the sample phone do not establish additional canonical clues. [P]

**Voice direction:** Prompt for Astra requests Live for all four NPCs: Luca uses `meridian` (measured North American, dry humor); Maya `gleam` (lively North American, affectionate teasing); Ren `quartz` (concise Australian, dry amusement); Theo `vesper` (polished British, quieter when discussing secrets). Keep Luca's heard-versus-known distinction and Ren's seen-versus-unheard distinction in both context and performance. These are source voice identifiers; deployment availability remains an integration check. [P]

**Character presentation:** Distinct silhouettes and expressive faces must work in both the club view and close conversation framing. Ren's production workflow is intended to establish the approach for the remaining cast. [D, P]

**Animation handoff:** The linked workbook has now been reviewed in full: 25 rows, 23 marked Ready and two To Do, referencing 17 unique Drive FBX files and two video references. All 17 FBX originals are downloaded with source-row mappings and checksums in the [animation handoff](../art/animation-handoff/README.md). Ready is the sheet author's status, not evidence of import, retargeting, or visual acceptance. Walking clips are linked to listening rows; a Theo talking clip is linked to Maya's upset reaction; player fast walk shares Luca's walk. Ren's DJ mixing remains To Do with a video reference, and the rewind reference is not an executable effect. Maya recording and Luca intervention still need explicit coverage. See the handoff flags before wiring animation states.

**Audio:** Music must communicate the two moods while keeping dialogue understandable. Use a clear reset cue to distinguish rewind from an ordinary scene transition. Exact tracks, mix behavior, and transition timing are open.

**Repository constraints:** Mobile landscape, 16:9 target aspect, iPhone 15 Plus minimum-spec target, sustained 30 fps, and expressive real-time English/Japanese lip-sync using open-source or project-owned code. These are current project constraints from the README, not newly inferred Figma decisions. [R]

## 12. MVP scope and completion criteria

The MVP is one complete social encounter with a visible failure/reset and a meaningful changed attempt. [F, D]

**In scope:**

- One club space and the routes/regions needed for the encounter.
- Investigator player; Maya, Ren, Luca, Theo; non-interactable affair partner.
- Intimate and Aggressive music/mood states.
- Maya following, conditional recognition, default recording, and an adjudicated request that changes behavior.
- Theo's response to the encounter and Luca's intervention.
- One authored catastrophe type and repeatable reset support; at least one demonstrated rewind.
- Retained player learning and a readable clue display.
- Runtime OpenAI inference that changes actual game state through validation.
- A resolved-encounter success state and end-of-set completion once their exact conditions are authored.

**Deferred:** Additional rooms, extra moods, playing as Ren, extra interactive spouse/catalyst roles, a larger serial-killer mystery, procedural mystery generation, multiplayer, and a full evidence-board system beyond the demo's needs. The serial-killer idea appears as a possible post-demo expansion in the September 13 notes, not an MVP commitment. [D]

### Proposed playable acceptance checks

1. **Opening causality:** The intended first-loop route visibly produces recognition, recording, Theo's approach, Luca's intervention, catastrophe, and rewind in the authored order.
2. **Recognition:** Player-only entry does nothing; Maya must satisfy the zone and visibility conditions; recognition fires at most once per attempt.
3. **Preparation:** The left route permits preparation. Waiting outside the recognition area cannot award victory.
4. **AI agency:** At least one natural-language request is interpreted at runtime and produces a validated behavior change, such as Maya remaining while the player walks away.
5. **Character consistency:** Intimate changes dispositions without guaranteeing compliance; Theo's evidence/exposure context and Luca's threat response still apply.
6. **Action-based danger:** Merely entering the confrontation area never independently causes catastrophe.
7. **Knowledge boundaries:** A private disclosure does not update uninvolved NPCs without an authored communication or witnessing event.
8. **Reset integrity:** The next attempt restores its configured initial state, clears recognition, and retains the intended player clue. Late AI results from the previous loop have no effect.
9. **Visible alternative:** The second attempt demonstrates a different sequence or response with an understandable cause.
10. **Completion:** A valid resolution can reach success; passive avoidance cannot. The final predicate must be specified before this check can pass.
11. **Session reconstruction:** End and reopen a conversation in the same loop. The NPC retains validated claims, disclosures, and relationship changes despite receiving a fresh voice session.
12. **Context refresh:** Change a relevant fact or mood during a conversation. Subsequent behavior uses the updated NPC projection without exposing facts that character cannot know.
13. **Tool authority:** Reject an ineligible clue disclosure or invalid relationship change; record an NPC lie as a claim without changing the authored truth.

These are completion criteria for the future playable build, not claims that the current repository has passed them. The README currently documents separate movement/conversation and live-voice gyms. [R]

## 13. Decisions still required

| Priority | Decision | Why it matters |
| --- | --- | --- |
| Before encounter implementation | Who dies, who performs the lethal action, and what precisely causes it? | Establishes the authored truth, animation requirements, and escalation chain. |
| Before encounter implementation | What actions resolve the danger, and what constitutes surviving the set? | Prevents indefinite avoidance from becoming a win and gives the demo a clear endpoint. |
| Before encounter implementation | Exact escalation, intervention, and disclosure prerequisites | AI needs enforceable rules; the sources supply dispositions but no complete thresholds. |
| Before encounter implementation | Whether the first failure is forced, guided, or preventable | Determines tutorial agency and how reliably the rewind is demonstrated. |
| Before encounter implementation | Recognition visibility rules and the significance of Ren's sightlines | The board specifies visibility, but not occlusion details or offscreen catastrophe behavior. |
| Before encounter implementation | Set duration and whether time runs during conversation, menus, or inference | Determines pacing, fairness, and timer behavior. |
| Before demo lock | Music-request rules and exact wait/resume behavior | Prevents ambiguous control over Ren and a companion who can become stranded. |
| Before demo lock | Retained clues and evidence reset rules | Makes repeated attempts coherent and teachable. |
| Before demo lock | Final legal-action schema, bounds, model configuration, and fallback behavior | Makes the AI contract implementable and reviewable. |
| Before narrative lock | Ren's loop awareness, twist, and reveal timing | Draft sources differ; it is not required to invent a larger explanation to demonstrate the loop. |
| Later | Final title and post-demo mystery | Does not block the contained encounter. |

The September 13 sync asks whether the board's opening, matrix, and rules can be locked; it does not record final answers. This GDD uses them as the requested authoritative baseline while preserving those remaining decisions.

## 14. Reconciliation record

| Earlier Google Doc statement | Consolidated treatment | Basis |
| --- | --- | --- |
| Trusting and Aggressive moods | Intimate and Aggressive | Figma matrix takes precedence. |
| Investigator versus Ren POV unresolved | Investigator | Figma's separate player/Maya/Ren roles and September 12 decision. |
| Engine and platform unresolved | Unity, mobile-first | September 12 decision; repository adds current technical targets. |
| Generic five-character cast including spouse and catalyst | Four named interactive NPCs plus the non-interactable affair partner; player separate | Figma and September 12 Theo/affair decision. |
| Theo's affair involvement uncertain | Theo is the affair participant Maya recognizes | Explicit Figma recognition rule and sync decision. |
| Maya knows the incident at loop start | Knowledge follows recognition; recording is the default opening beat | Explicit Figma trigger. |
| Catastrophe threshold/reset described abstractly | Author character actions and escalation; zone entry alone cannot cause violence | Explicit Figma rule. |
| Survive until the set ends | Retain the end-of-set goal, but require encounter resolution rather than avoidance alone | Figma's no-avoidance-win rule; exact predicate remains open. |
| DJ primarily reads the room autonomously | Include an explicit player music-request interaction | Figma booth interaction and second-attempt plan. |
| General inquiry about AI changing mysteries each run | Fixed authored mystery with bounded AI social consequences | Later GDD contract and Figma's concrete encounter. |
| Ren secretly controls the loop / grounded stance unresolved | Preserve the proposed reveal as unresolved story direction | Figma confirms visibility and reset staging, not a full explanation. |
