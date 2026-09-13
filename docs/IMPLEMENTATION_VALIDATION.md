# Encounter implementation validation

Recorded 2026-09-14. These results distinguish executable checks from device and presentation acceptance.

| Check | Evidence | Result |
| --- | --- | --- |
| Server suite | Linux Node 24 container, current request-freshness implementation | 100 passed, 4 opt-in live tests skipped; 104 total. Includes approach routing, late user-evidence reinterpretation, bounded clarification, lease correlation isolation and public WebSocket sequencing. Local Windows socket allocation previously returned intermittent EADDRINUSE; container checks passed. |
| Complete real-provider prevention | [Run 34777703855](https://github.com/jethac/lucid-loop/actions/runs/34777703855), commit `a9327fc` | All 3 Live tests passed, including witnessed catastrophe, rewind, nearby conversations, Responses action commits, mediation, separation and the full 180 active-second loop. |
| Spoken request through real provider | [Run 34779053805](https://github.com/jethac/lucid-loop/actions/runs/34779053805), commit `40e3a73` | Synthetic spoken PCM, no typed command: 1 native client delegation, 9 input fragments, confirmed Maya wait action, 132,480 output samples and final usage. All 3 enabled live tests passed; full prevention was skipped for this run. This does not validate a physical microphone or interruption behavior. |
| Pushed checkpoint CI | [Run 34776400688](https://github.com/jethac/lucid-loop/actions/runs/34776400688), commit `e425a79` | Gyms workflow passed; physical iOS checks remain separate |
| Real provider CI | [Run 34774108170](https://github.com/jethac/lucid-loop/actions/runs/34774108170), commit `72cc6d3` | Server/Docker job and both real Live smoke tests passed, including a typed Maya wait action committed through Responses |
| English speech stream | `dotnet test tools/live_speech/Tests.csproj` | 6 passed; upstream nullable-context compiler warnings |
| Character EditMode | Actual shared Unity Editor, `character-editmode-20260913-181620-873.xml` | 95 passed |
| Character face/speech PlayMode | Actual shared Unity Editor, `character-playmode-20260913-190936-360.xml` | 12 passed, including root ownership and final-pose hold; evidence under `docs/validation/character-runtime/` |
| Encounter EditMode | Actual shared Unity Editor, `gym-editmode-20260913-194714-271.xml` | 84 passed, including approach intent fencing, action feedback transport, touch cancellation, phone layout, LAN policy, emission reimport and primitive fall/reset |
| Approach PlayMode | Actual scene and local relay, `approach-playmode-20260913-194736-520.xml` | Passed movement into Ren's speaking range, overview until authoritative eligibility, exactly one conversation request and cancellation by manual walking. Provider disabled for this test; real-provider evidence is listed separately. |
| Phone preview PlayMode | Actual scene and local relay, `phone-encounter-playmode-20260913-193446-743.xml` | Passed opening, held catastrophe, rewind and expanded retained-clue checks. Ready and loop-two captures visually inspected; this is an Editor preview, not a physical phone test. |
| Mood PlayMode | Actual shared Unity Editor, `mood-playmode-20260913-185641-827.xml` | Passed exact post-update pulse scaling across 20 frames, pause, fallback and restoration |
| iOS activation | Shared Editor log `IOS_TARGET_ACTIVE: iOS` | Active target switched and player settings serialized |
| Development iOS export | Shared Editor log `IOS_XCODE_EXPORT_OK: Builds/iOS/Xcode` | Xcode project and IL2CPP output exported successfully; shader reports copied to `Unity/Builds/iOS/` |
| Encounter scene construction | Shared Editor log `ENCOUNTER_SCENE_CREATED` | `Assets/Gyms/Scenes/BeforeTheDrop.unity` created with server movement, voice, speech and mood bindings |
| Integrated encounter PlayMode | Actual scene and local relay, `encounter-playmode-20260913-185745-685.xml` | Passed movement, recording/approach/intervention/catastrophe, held fall without actor-root drift, HUD, and retained-clue/reset-pose restoration; three captured frames inspected |
| Lighting inventory | `python tools/audit_ios_rendering.py` | 8 encounter lights, one shadow-requesting light; 25 Lit materials, 2 material keyword sets, no emission flag inconsistencies |

Detailed Unity XML and local captures live under ignored `.local/validation/`. A malformed new test assembly metadata GUID was corrected after actual import revealed that Unity ignored the assembly definition; a subsequent duplicate TestRunner reference was also removed. Manual source compilation alone did not catch either import issue.

The initial catastrophe screenshot exposed a missing fall pose. An encounter-only primitive adapter now lowers Luca's visual, holds it until rewind and restores it on reset; the follow-up screenshot and test verify that behavior. Maya's phone/arm also follows the authoritative recording flag. These are explicit placeholders, not accepted retargeted death or recording clips.

Still pending: Xcode compile/sign/install, final action animation, on-device layout/keyboard and touch-target checks, physical iPhone networking/audio/thermal profiling, musical audition, final character animation and expressive face acceptance. English lip-sync is implemented; Japanese has no accepted producer. These results do not establish completed game or visual/audio quality.
