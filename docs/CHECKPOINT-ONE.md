# Before the Drop checkpoint one

Local human-review build, September 13. Branch: `codex/btd-checkpoint-one`. Nothing has been pushed. This is the playable story skeleton using the existing gym environment and primitive characters, not final art or the AI milestone.

## Open

Use Unity 6000.3.24f1. Open the Unity project in this working copy (not the original `repo/Unity` project), then open `Assets/Gyms/Scenes/BeforeTheDrop.unity`. Select Game view at landscape 16:9 or Free Aspect and press Play. No server, API key, microphone, or internet connection is required.

Click floor to walk. Click a named character to approach and open authored choices. In the first loop, introductory clicks offer a route toward the encounter instead of prevention choices. Continue or Space advances story dialogue. Escape leaves a choice panel. Restart demo resets everything.

## First review route

1. Advance Maya's arrival line. Click the open floor on the right side of the central dance circle (toward Theo). Maya follows. The first entrance path is deliberately constrained.
2. Maya spots Theo and records. Continue through Theo approaching, Luca stepping in, the fall, and Ren's line. The cyan rewind returns the cast to the entrance and updates the loop/memory display.
3. On loop two, click Maya and ask her to wait while you talk to Luca. Walk left; Maya should stay at the entrance.
4. Click Luca and ask him to help you step aside early if Theo gets upset. He should agree. The player walks to him first.
5. Optionally click Ren and request Intimate. This changes the state label and some wording only at checkpoint one; the actual music/audio treatment is the next milestone.
6. Return to Maya. Ask her not to film and to speak privately. Then talk to her again and ask her to come with you.
7. Walk toward Theo with Maya. The encounter should resolve safely: no recording, Luca intervenes early, Theo agrees to take space, and the objective changes to THE NIGHT CONTINUES.
8. Restart if desired. Entering the encounter on loop two without both preparations leads to another catastrophe and rewind. Standing at the bar or choosing Intimate alone never wins.

## Feedback requested

- Is it obvious how to move, interact, and continue the story? Where do you hesitate?
- Does the first sequence make spatial sense: recording → Theo approaches → Luca intervenes → collapse → Ren interrupts?
- Is the rewind clear, and does the second attempt feel different quickly enough?
- Does asking Maya to wait feel meaningful and readable?
- Is returning to Maya too much walking? Is there too much Continue-clicking?
- Does the safe ending feel like a consequence of your preparation?

Send the step, expected versus actual result, and screenshot/Console error if something fails. Evaluate story structure and control feel, not the temporary visual quality.

## Explicit limits

- Authored buttons, no freeform AI, voice, API usage, or actual music yet. Existing LiveGym and relay are unchanged.
- Success currently requires two fixed preparations: Maya agrees to a private/no-recording approach, and Luca agrees to intervene early. This is a testable provisional rule, not the final AI decision space or a locked narrative conclusion.
- The recognition region assumes the authored clear view of Theo. It is not a general occlusion-aware sight system. The background affair partner is a non-interactable stand-in.
- Close-up portraits, sound, rewind polish, animation, and final mobile UI/performance are later work. This scene has not been tested on an iPhone.
- Memory records what happened; no invented murder-mystery clue is claimed. A larger mystery hook remains later narrative work.

## Implementation and verification

`LoopState.cs` holds per-loop state and guarded resolution rules. `FirstLoop.cs` controls navigation, authored interactions and the sequence. `FirstLoopBuilder.cs` creates the separate scene from CharacterGym without altering the source gym.

Menu: Lucid Loop → MVP → Create or rebuild first loop. Rebuilding replaces the generated BeforeTheDrop scene; make durable changes in the builder/runtime.

Automated verification: 12/12 EditMode tests passed (8 existing + 4 new). A Unity play-mode smoke run exercised navigation, recognition, catastrophe, rewind, waiting, rejection of music-only success, and safe resolution. Tests and logs are in the ignored `.local` folder. No live API or device claim is made.

Editor note: this local Unity installation also emits an `ArgumentOutOfRangeException` from `UnityEditor.Search.SearchDatabase` during startup indexing. It did not prevent the verified playthrough; it is separate from the MVP scripts. Report any gameplay exception separately. This editor indexing issue remains unresolved at handoff.
