# Authoritative club world

`server/src/encounter-world.mjs` implements the bounded planar world for [A Quiet Way Out](DEMO_SCENARIO.md). The server moves actors and derives recognition, intervention, and separation from those accepted positions. Unity renders them with interpolation. World transport and approach-to-talk are integrated and tested in the actual Unity scene; final visual staging and device playtesting remain acceptance work. See [validation evidence](IMPLEMENTATION_VALIDATION.md).

## Host interface

```js
const world = createEncounterWorld({ registry, credentials, config: CLUB_WORLD_CONFIG });
world.input({ type: 'move_to', loopId, sequence: 0, destination: { x: 10, z: -1.7 } });
world.tick(monotonicElapsedSeconds, { paused, voiceActive, suspended });
world.canConverse('ren');
world.snapshot();
world.reset({ loopId, revision });
```

Create one world per authenticated game, using the same registry credentials and scenario instance as dialogue. Do not expose `trustedWorld`, `trustedFull`, config, tick, or arbitrary method dispatch to clients or models. The host owns a 100 ms timer and supplies monotonic elapsed time. Tick consumes fixed 0.1 s steps and at most five steps per call; it drops excess stall time. The same pause flags stop movement and scenario time. `voiceActive` must include connecting, connected, and reconnecting foreground conversation states; `suspended` includes application suspension and loss of the authoritative world connection. A pause clears fractional accumulated time. Do not derive these flags from model output.

`input` returns `{ accepted, reason? , loopId?, sequence? }`. Commands are `move_to` with a finite `{ x, z }` destination, semantic `approach` with an interactable `npcId`, or `stop`. Sequences are nonnegative safe integers, strictly increasing within each loop. Rejected commands do not consume the sequence. Stale loops, unknown fields/action types, and blocked/out-of-bounds/unreachable destinations are rejected. This implementation rejects a destination on furniture rather than silently projecting it somewhere else; the client should keep the previous marker when rejection occurs. Clients cannot submit positions, NPC destinations, elapsed time, facts, or stage booleans.

`canConverse(npcId)` returns `{ ok: true }` only for Maya, Ren, Luca, or Theo within 2.2 m of the player and with an unobstructed eye-height sightline. Reject the live-session attach before connecting to a provider when this gate fails. Recheck against the current world when switching NPCs. The affair partner is not interactable. Terminal encounters reject conversations and movement.

`reset` delegates to the registry revision fence, then restores original positions, destinations, stage cache, elapsed-step accumulator, and input sequence. The adapter also notices a loop reset performed by another trusted caller. Player discoveries are retained by the scenario, never by this geometry module.

## Approach-to-talk wire protocol

On the authenticated `/game` connection, send `{ type: 'game.approach', loopId, sequence, npcId }`. It shares the sequence stream with `game.walk` and `game.stop`. The server responds:

```json
{
  "type": "game.approach_result",
  "accepted": true,
  "loopId": "game-id:loop:1",
  "sequence": 7,
  "frame": 42,
  "npcId": "ren",
  "destination": { "x": 2.02, "z": 5.41 }
}
```

The example destination is illustrative; the server computes the actual point. The bounded solver samples 64 stand points on two rings inside the existing 2.2 m interaction radius, filters out furniture, bounds violations and blocked sightlines, and chooses the shortest reachable route among them. It may fail closed for an unusual narrow layout rather than claim that an unreachable NPC can be approached. An already eligible approach chooses the player's current position, cancelling a previous walk without moving away from the NPC. Approach never relaxes or bypasses the live-session gate.

Rejected replies carry `accepted: false` and a `reason`: `stale_loop`, `stale_sequence`, `unknown_npc`, `invalid_input`, `no_reachable_conversation_point`, `encounter_ended`, or `paused`. The wire reply echoes correctly typed request `loopId`, `sequence`, and `npcId` even on rejection, so an old reply cannot cancel a newer client intent. Walk and approach are rejected during explicit world pause without consuming sequence. `game.stop` remains available to cancel queued movement during pause; it uses `{ type: 'game.stop', loopId, sequence }` and replies with `game.move_result`.

Every `game.world` event includes `world.conversations`, a map for `maya`, `ren`, `luca`, and `theo`, each containing `{ eligible: true }` or `{ eligible: false, reason }`. This projection calls the same eligibility helper as `canConverse`; it is a current-frame observation, not a future conversation reservation. The server still checks the live-session gate when a client opens Live.

The client should keep an explicit Talk intent keyed by loop, sequence, and NPC. Approach resolves one destination from the NPC's current position; it is not continuous pursuit and never opens voice by itself. A newer walk, approach, or stop replaces that destination. Reset clears it. When cancelling Talk for selection, pause, disconnect, or another interaction, clear the client intent; use a newer stop if movement must also stop. Open voice only for the matching accepted intent and a current eligible world frame. The first snapshot after accepting movement can still report the prior `idle` motion, so do not interpret idle as a failed approach until `world.frame > approach_result.frame`. If a later stopped/blocked frame is ineligible because the NPC moved, cancel the intent and let the player explicitly request Talk again.

## Public world frame

```json
{
  "ok": true,
  "version": "club-plan-1",
  "loopId": "game-id:loop:1",
  "frame": 12,
  "sequence": 0,
  "elapsedSeconds": 1.1,
  "actors": {
    "player": {
      "position": { "x": 0, "z": -8 },
      "motion": "moving",
      "action": { "type": "idle" }
    }
  }
}
```

All six actor IDs appear: `player`, `maya`, `ren`, `luca`, `theo`, `affair_partner`. `motion` is `idle`, `moving`, `blocked`, or `fallen`. The player's scenario action stays `idle`; its locomotion comes from world input and `motion`. NPC `action` is the accepted scenario intention. Use this world frame alongside the registry's public encounter snapshot (`mood`, `phase`, `recording`, `catastrophe`, `victory`, discoveries). Do not forward the trusted scenario/NPC memory record to the player or another NPC. Frames increase across resets; always discard interpolation history when `loopId` changes. Host publication must follow the completed world tick, rather than serializing the intermediate registry callback between observation and clock commits.

## Geometry and movement

`CLUB_WORLD_CONFIG` is the versioned source of this module's geometry. Coordinates are XZ metres, +Z toward the booth. It uses the existing `Unity/Assets/Gyms/Editor/GymBuilder.cs` Character gym layout:

| Actor | X | Z | Presentation Y |
| --- | ---: | ---: | ---: |
| Player | 0 | -8 | 0 |
| Maya | -2.8 | -2 | 0 |
| Ren | 2.5 | 7.4 | 0.6 |
| Luca | -7 | 2 | 0 |
| Theo | 7 | -1 | 0 |
| Affair partner, newly staged | 8 | -0.8 | 0 |

Bounds are X ±13, Z ±11, inset by a 0.35 m actor radius. The module includes conservative rectangular furniture footprints for the bar, booth, speakers, stools, sofas, four tables and their seats, and entrance columns. Low furniture blocks locomotion but not eye-height sightlines; speakers and entrance columns block both. The raised DJ platform and its steps remain traversable in this planar approximation. Unity must apply suitable elevation presentation when an actor reaches the platform; treating Y=0 as authoritative everywhere would clip into it.

Movement uses a small visibility graph around inflated static obstacle corners. Player speed is 3.7 m/s and NPC speed is 3 m/s, both capped per simulation step. It does not duplicate Unity NavMesh, implement acceleration, dynamic crowd collisions, moving obstacles, or a physics fall. Decorative patrons do not block this world. Keep Unity's local NavMeshAgent from independently moving these actors. Any scene-layout changes must update and validate this config and its tests together; currently the config is an exported JS object, not yet a shared Unity-imported manifest.

Following stops within 1.2 m; approach stops at 1.25 m. Theo backs away to 2.5 m after agreeing to keep distance. Luca's intervention destination is the midpoint between Theo and Maya; blocked paths produce `blocked`, not a fabricated arrival. Idle/wait stops, fall freezes, and separation moves Theo to VIP `(10.5, 4)` while Maya follows the player. Destination `(10, -1.7)` is the tested first-loop guided approach. The default opening reaches the fatal staging after actual traversal; 20 seconds is a minimum after recognition, not a teleport deadline.

## Derived predicates

Recognition requires Maya inside the authored VIP recognition rectangle (X 6.8–10.2, Z -2.5–0.5), within 7 m of both Theo and the affair partner, with both eye-height segments clear. It commits once per loop.

The adapter derives stage changes atomically from one accepted world frame, using a fresh revision fence for every trusted call:

- `theoAtMaya`: within 1.5 m and no furniture footprint between them.
- `lucaBetween`: segment parameter between 0.15 and 0.85, at most 0.45 m from the Theo–Maya segment, within 0.9 m of fall anchor `(8.65, -1.5)`, and a fall segment to south VIP table `(9, -3)` that hits no other furniture. Configuration validation requires the fall endpoint inside that specific table footprint. Thus an intervention elsewhere cannot invent a table collision.
- `renCanSee`: Ren's eye-height segment to Luca is unobstructed. This conveys vision, never overheard dialogue.
- `allInMediation`: Luca within 2 m of Maya, Theo 2–3.5 m from Maya, and all three connecting conversation-space segments unobstructed by furniture.
- `separated`: mediation committed, Theo within 0.2 m of the safe VIP destination, Maya at least 4 m from Theo, and both executing the accepted separation actions.

Only changed stage booleans create stage observations. Each active step advances the scenario by 0.1 s; terminal frames freeze immediately. The scenario remains responsible for social gates and the end-of-set victory predicate. Being in a zone cannot itself kill anyone.

## Verification and integration limits

Run `node --test server/test/encounter-world.test.mjs`. Tests cover invalid and spoofed inputs, speed and obstacle bounds, dropped catch-up time, pause coherence, complete default opening, blocked recognition, full prevention and physical separation, all four conversation gates, and reset fencing. Tests execute original geometry rather than supplying arbitrary stage observations.

These checks establish deterministic server causality. They do not establish that the final animation falls against the table, that character bodies never overlap during staged intervention, or that the club sightlines match final character eye heights. Verify those in the Unity scene. The host must also size registry revision capacity for multiple 180-second loops: clock commits alone consume about 1,800 revisions per completed set.

## Reachable prevention route with conversation proximity

The test `second-loop prevention is reachable with every action spoken nearby and voice time paused` verifies the complete route with production configuration and scenario rules. It first walks to `(10, -1.7)`, observes the actual catastrophe, resets through the registry fence, and checks the retained `shove_seen` discovery. It never injects facts, positions, visibility, or stage flags. Before **every** NPC action it requires `canConverse(npcId).ok`; each conversation simulates 60 seconds of provider/voice time before the action and another 60 afterward, checking that neither actor positions nor the active timer advance.

The measured second-loop route is:

| Active time | Player destination, XZ | Nearby conversation / movement |
| --- | --- | --- |
| 1.3 s | `(-2, -4)` | Ask following Maya to wait. |
| 4.4 s | `(2.5, 6.2)` | Ask Ren for Intimate music. |
| 7.0 s | `(-6, 2)` | Ask Luca about Theo's fear of exposure; receive the authored clue. |
| 9.0 s | `(-2, -4)` | Agree on a private approach with Maya, then ask her to follow. |
| 12.5 s | First `(9.7, -1.7)`, then `(9, -1.7)` after Maya recognizes the affair | Ask Theo to keep his distance. Ask Maya to stop recording and wait while Luca joins. |
| 17.5 s | `(8, -1.7)`, after the public actor frame confirms the group has stopped in mediation range | Ask Luca to mediate. |
| By 30.3 s | `(0, -8)` | Leave with Maya; Theo independently reaches his VIP safe destination. Separation is confirmed. |
| 180.0 s | Remain safely separated | The set ends and the complete victory predicate succeeds. |

The times measure active simulation, not real conversation duration. The safe arrangement is complete with about 150 seconds remaining; the scenario intentionally declares victory only at the set's end. No relaxed speed, timer, obstacle, or interaction radius was needed.

There is a practical interaction detail to preserve in the UI: staying at `(9.7, -1.7)` can leave the player about 2.3 m from Theo after he approaches Maya, just outside the 2.2 m talk radius. Moving 0.7 m left to `(9, -1.7)` makes Theo reachable while Maya remains nearby. The talk prompt should follow the authoritative proximity gate, and a destination marker must not imply that a distant NPC is already available. Asking Maya to wait after lowering the phone also keeps the mediation group stable while the player steps toward Luca. These are ordinary movement and accepted dialogue actions, not hidden test permissions.

The public-only wait used by the route test requires Luca, Maya, and Theo to have `motion: idle`; their actions must be `approach`, `wait`, and `keep_distance`, respectively. Luca must be within 1.3 m of Maya, Theo between 2 and 3.5 m from Maya, recording must be off, and the mood must be Intimate. This becomes true at 17.2 s on this route: Luca is at `(6.19281, -1.73900)`, Maya at `(7.39545, -2.07984)`, and Theo at `(6.53576, 0.26770)`. The player then walks to `(8, -1.7)` and checks Luca's normal conversation gate. Waiting for Luca to be within 1.5 m of that player waypoint would never finish: his actual stopping distance from it is about 1.808 m. Require `idle` rather than merely “not moving,” because `blocked` also means not moving. This positional predicate is validated for this fixed route; it does not replace the server's authoritative obstruction and social-policy checks in arbitrary arrangements.

This demonstrates that the server-side route is reachable through the intended movement/conversation controls. Final Unity touch targeting, visual prompt clarity, provider interpretation, and voiced performance still need device playtesting; the test does not substitute for those checks.
