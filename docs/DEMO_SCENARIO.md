# Demo scenario: A Quiet Way Out

**Authored September 14, 2026, at the user's request.** This document supplies the victim, lethal action, and playable prevention route that the source material leaves open. These are new project decisions, not claims about what was written in Figma or Google Docs. The [GDD](GDD.md) remains the source synthesis; this scenario specializes its single-club demo.

## The fixed truth

Theo is having an affair with the non-interactable person beside him at the VIP seating. Maya recognizes them and starts recording. Theo fears public exposure and tries to take her phone. Luca intervenes to protect Maya. Theo deliberately shoves Luca aside; Luca falls backward against the low VIP table and dies. **Luca is the victim; Theo is responsible.** The player is investigating and preventing this escalation, rather than finding a randomized culprit.

Show the reach, Luca stepping between them, Theo's shove, and Luca's backward fall. Cut sound and begin the rewind before graphic injury. The accidental severity of the fall does not excuse Theo's deliberate violence. The table is a fixed staging prop, not a physics lottery or an interactable weapon. No additional combat system is required.

Ren sees the approach, intervention, shove, and fall from her booth. She cannot identify dialogue she did not hear. She does not know that a loop has occurred. The player retains the observed clue: **“Theo reached for Maya's phone. Luca stepped in. Theo shoved him.”** Other NPCs' memories and physical states reset.

## Opening and failure

The first-loop onboarding route leads the player and following Maya toward Theo. Recognition requires Maya to be in the recognition area and actually see both Theo and the affair partner; it happens only once per loop. Recognition starts recording by default. The opening establishes these beats, measured in unpaused simulation time after recognition:

| Offset | Beat | Required physical condition |
| --- | --- | --- |
| 0 seconds | Maya recognizes them and raises her phone. | Valid recognition observation. |
| 4 seconds | Theo moves toward Maya. | A movement request, not a teleport or immediate attack. |
| 10 seconds | Theo reaches toward the phone; Maya keeps her distance and objects. | Theo has reached Maya's conversation radius. |
| 14 seconds | Luca moves between them to interrupt the intimidation. | Theo is near Maya; she is recording; Theo has not agreed to keep his distance. |
| 20 seconds or later | Theo shoves Luca; the staged backward fall is fatal. | Theo is near Maya, Luca is between them, Ren can see the staging, recording continues, and no distance agreement has been made. |

The timing values are tunable defaults. A missed path or absent staging observation must not fabricate a completed approach or fall. Crossing the confrontation zone alone does nothing lethal. An accepted change in behavior may interrupt the opening; the guided first route is not permission to discard successful player actions.

The catastrophe freezes the encounter. Present a brief rewind transition, close the active voice session, then reset through the authoritative reset action. The scenario module intentionally waits for that reset action so the presentation can finish; it does not run a wall-clock timer behind the player's back.

## The playable prevention route

1. **Create time.** On the next attempt, ask Maya to wait near the entrance. Her accepted action visibly changes from following to waiting. Take the left route to the bar and booth.
2. **Change the social conditions.** Ask Ren for Intimate music. The lighting and track change together. This makes the calm route available, but does not independently prevent violence.
3. **Get a grounded clue.** Ask Luca directly what Theo is worried about. Under Intimate music, Luca shares a limited personal observation: he heard Theo demand that the affair stay out of public view. This is a confirmed disclosure with provenance; general rumors or an unvalidated transcript line cannot substitute for it.
4. **Agree on a private approach.** Explain the exposure risk to Maya and ask her to avoid a public accusation. An accepted request records a private plan. She can follow the player again. She must still recognize Theo and the affair partner; the demo is not won by hiding her forever.
5. **Interrupt the phone dispute.** With Maya's private plan established and Intimate music active, ask Theo to keep his distance and speak without grabbing the phone. Theo explicitly agrees through a validated action. Maya can then agree to stop recording and lower the phone. She keeps the evidence already captured; deletion or surrender of her phone is not a success condition.
6. **Finish the exchange.** Bring Luca into conversation range with Maya and Theo. Ask him to mediate a private exchange. He commits only when recognition occurred, the private plan and distance agreement exist, recording has stopped, Intimate music is active, and all three are physically present. Theo admits the encounter; Maya hears him and chooses to leave the conversation with the player. No reconciliation or forgiveness is required.
7. **Separate and finish the set.** Maya and Theo move to distinct safe destinations; the world adapter confirms separation. The encounter is resolved, and everyone remains alive through the end of the set.

Dialogue wording is flexible. Runtime AI interprets requests and answers in character; validated intents commit the authored state changes. No exact sentence, voice transcript substring, charisma roll, or model-declared “you win” is a rule. The player can discover the route on an initial loop; a compulsory death is not required to unlock it. Waiting is useful preparation, not a mandatory checkbox.

## Success, failure, and time

One set lasts **180 seconds of active simulation**, configurable from 30 to 900 seconds for development. Pause the entire simulation clock during an active foreground conversation, provider connection/reconnection, explicit pause, and application suspension. Voice latency must not consume the intervention window. Outside those pauses, movement and encounter time advance together. Keep a visible timer or clear set-progress indicator; never run NPC escalation while the scene is visually frozen.

Victory requires all of: completed mediation after recognition, stopped recording, Theo's distance agreement, physically confirmed separation, no catastrophe, and reaching the set's end. A safe arrangement established early remains binding for this short authored set; no random second attack is introduced. A later Aggressive track does not revoke an already completed agreement.

If the set ends with Maya still waiting, or the encounter remains unmediated, show **“The set ended, but the danger is unresolved.”** Offer another attempt. This is neither victory nor an invented offscreen murder. A catastrophe likewise offers the rewind. A player-requested restart resets loop-local facts, plans, commitments, staging, timer, and conversations while retaining player discoveries. The actual club need not be empty or evacuated to count as separated: Maya must be safely away from Theo and he must no longer approach her.

## State and implementation contract

[demo-scenario.mjs](../server/src/demo-scenario.mjs) exports `createDemoDefinition()` and the `createDemoScenario()` encounter wrapper. It adds deterministic scenario state to the existing encounter interface while presenting one loop/revision fence. Its [tests](../server/test/demo-scenario.test.mjs) exercise causal failure, changed attempts, evidence gates, pauses, reset retention, and the complete success predicate.

| Entry point | Owner and effect |
| --- | --- |
| Existing `wait`, `follow`, `request_music` | Authenticated Maya or Ren action; changes movement or mood. |
| `ask_about_exposure` | Luca; a proposal to answer a direct question about Theo's fear of exposure. Requires Intimate mood and discloses only the fixed `exposure_fear` fact. The accepted event includes its canonical text for the dialogue bridge. |
| `agree_private_approach` | Maya; requires Intimate mood and the confirmed exposure clue. |
| `agree_distance` | Theo; requires recognition, Maya's private plan, and Intimate mood. |
| `stop_recording` | Maya; requires Theo's accepted distance agreement. |
| `mediate` | Luca; requires the complete social and physical predicates above. Commits `mediation_completed` with the canonical `private_exchange` fact: Theo admits the affair and Maya chooses to leave with the player, keeping her evidence. The player, Maya, Theo and Luca learn it; Ren does not. The dialogue bridge supplies that confirmed outcome to Luca. This does not establish separation or victory. |
| `discloseClue` | Trusted dialogue adjudicator; validates Luca's direct-question disclosure under Intimate music. |
| `observeVisibility` | Trusted world observation; starts once-per-loop recognition and default recording. |
| `observeStage` | Trusted world adapter; confirms actual approach, intervention, Ren's view, mediation range, and final separation. |
| `advance` | Trusted server clock; consumes elapsed simulation seconds only when unpaused. |

The world adapter must derive positions and visibility from validated scene state. A client/model assertion that everyone is nearby is not evidence. The generic internal `confirmFact` method remains trusted; the live route uses `ask_about_exposure`, not unrestricted fact insertion. `discloseClue` remains a trusted internal alternative. A model's supplied fact ID, text, or `directQuestion` flag cannot choose what the action reveals. The wrapper's full `scenario` record is server state, not an NPC prompt. NPC context receives only its own authorized knowledge and current action.

**Implementation status:** The server world adapter, collision/sightline checks, clock, Unity encounter scene and timer, and dialogue-tool wiring are integrated; the [validation ledger](IMPLEMENTATION_VALIDATION.md) records real-provider prevention and Editor scene checks. The mediation exchange is a server-confirmed narrative outcome, delivered as context for Luca's response; it is not yet a separately voiced three-person performance. Final character animation and physical iPhone acceptance remain pending. Rewind clears participants' exchange knowledge while retaining the player's discovery from the earlier loop.

## World adapter contract to build

The following records the authored adapter contract. The server world and Unity integration are now implemented; see [the integration guide](UNITY_ENCOUNTER_INTEGRATION.md) for current runtime details. The server owns a small planar world simulation for this encounter; Unity sends movement intentions and renders accepted positions/actions. Keep the scene definition in one versioned manifest consumed by both sides, with positions in metres on the XZ floor plane, +X to the player's right and +Z toward Ren. The manifest must contain walkable bounds, obstacle footprints, named actor spawn positions, the recognition polygon, `vip` safe destination, the VIP table collision footprint, and a valid Luca fall staging anchor beside that table. Place the scene so the opening approach naturally reaches that anchor and Ren has a clear line to it. Geometry values are configurable art-layout data; do not duplicate arbitrary coordinates in dialogue prompts.

**Input:** `{ type: 'move_to', loopId, sequence, destination: { x, z } }` or `{ type: 'stop', loopId, sequence }`. Authenticate the game credential, reject stale loops and non-increasing input sequences, reject non-finite/out-of-bounds coordinates, and project destinations onto server walkable space. These world-input messages are separate from the NPC action protocol. Never accept actor positions, stage booleans, or elapsed time as authoritative client input. The player may only move the player. The server sets NPC destinations from the latest accepted action snapshot.

**Simulation:** Step at 10 Hz using monotonic server elapsed time, with player/NPC speed, acceleration, collider radius, and stopping distance in the same manifest. Limit catch-up steps after stalls; suspension/reconnect must not fast-forward the encounter. Pause both movement and the scenario clock when a foreground conversation, explicit pause, or suspension is active. Render accepted poses with Unity interpolation. Use 1.25 m as the initial conversation approach distance, 2.5 m as Theo's agreed distance, and 4 m as final safe separation; tune the manifest and staging together if character proportions require changes.

| Snapshot action | Executor behavior |
| --- | --- |
| `idle` / `wait` | Stop locomotion at the current accepted position. |
| `follow`, `targetId: 'player'` | Update Maya's destination behind the player; stop inside follow distance. |
| `approach`, `targetId: 'maya'` | Move Theo or Luca to Maya's conversation radius using collision-aware motion. Luca receives this automatically after Theo accepts distance and Maya lowers the phone, so the safe route can actually assemble its mediation group. |
| `keep_distance`, `targetId: 'maya'` | Theo stops closing in and backs away if necessary to maintain at least the agreed distance. This appears immediately when his agreement commits. |
| `intervene`, `targetId: 'maya'` | Luca moves to the free point between Theo and Maya. The adapter also knows the fixed threatening actor is Theo; no model selects a victim or shove direction. |
| `fall` | Luca stops locomotion; present the authored backward fall at the validated staging anchor. Do not simulate randomized injury physics. |
| `separate`, `targetId: 'vip'` | Theo goes to the named VIP safe destination. |
| `separate`, `targetId: 'player'` | Maya accompanies the player away from Theo. The player must move far enough away to complete separation. |

After advancing positions, derive observations from the same accepted world frame. Read the latest scenario revision before each method call; accepted observations themselves increment that revision, so never reuse one fence across several commits. Send stage changes as one atomic observation rather than mixing positions from different frames.

| Derived observation | Exact rule for the first adapter |
| --- | --- |
| `observeVisibility` | Maya is inside the manifest's recognition polygon; unobstructed eye-height rays from Maya reach both Theo and the affair partner. Submit only until recognition succeeds. |
| `theoAtMaya` | Theo is within 1.5 m of Maya and the segment between them has no solid obstacle. |
| `lucaBetween` | Luca lies within 0.45 m of the segment from Theo to Maya, with segment parameter strictly between 0.15 and 0.85; he is within the authored fall anchor's permitted staging tolerance and the configured backward fall path terminates at the VIP table. Proximity alone must not invent a table collision elsewhere in the room. |
| `renCanSee` | Unobstructed eye-height ray from Ren to Luca's current staging location. The authored opening must arrange this sightline; a blocked ray is a staging failure to fix, not a request to fabricate testimony. |
| `allInMediation` | Luca is within 2 m of Maya, Theo is between 2 and 3.5 m of Maya, and unobstructed conversation-space segments connect all three. |
| `separated` | Mediation has committed, Maya is at least 4 m from Theo, Theo has reached the `vip` safe destination within its configured tolerance, and neither is approaching the other. |

Feed those booleans to trusted `observeStage({ loopId, revision, stage })`, then call `advance({ loopId, revision, seconds: 0.1, paused: false })` for the simulation step. While paused, no repeated zero-time commits are necessary. Do not call public dialogue endpoints with these trusted observations. Subscribe Unity to actor transforms and public scenario snapshots containing `phase`, `recording`, `catastrophe`, and `victory`; the implemented world transport also supplies actor transforms and elapsed time to Unity.

The emitted phases are `exploring`, `recording`, `theo_approaching`, `phone_dispute`, `luca_intervening`, `separating`, `resolved`, `catastrophe`, `victory`, and `unresolved`. Phases select presentation; they are not substitutes for the spatial predicates. On reset, discard pending movement inputs and interpolation from the old loop, restore manifest spawns and action state, and wait for a fresh accepted snapshot before resuming playback.

## Art and audio work implied by this decision

Use the existing [animation handoff](../art/animation-handoff/README.md) as input, subject to character-art validation. The backward-death reference now targets Luca; it must be retargeted and staged on the shared male skeleton. The supplied clips do not by themselves prove that a phone recording pose, Theo's reach/shove, Luca's interception, Maya lowering the phone, or three-person mediation is ready. These are explicit remaining animation/staging needs, and minimal authored poses or blended locomotion are acceptable for the demo if the causal sequence stays readable.

The VIP table, separate safe destinations, unobstructed Ren sightline, and one non-interactable affair-partner actor are required scene elements. Ren's DJ loop remains an existing missing asset. Keep the fall non-graphic, duck the music at catastrophe, use the retained-clue beat to communicate the cause, and let the approved art agent own import, retargeting, and visual acceptance.
