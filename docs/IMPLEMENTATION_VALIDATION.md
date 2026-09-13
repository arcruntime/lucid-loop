# Encounter implementation validation

Recorded 2026-09-14. These results distinguish executable checks from device and presentation acceptance.

| Check | Evidence | Result |
| --- | --- | --- |
| Local server suite | `cd server; npm test`, current working tree | 84 passed, 2 opt-in live tests skipped; 86 total |
| Real provider CI | [Run 34774108170](https://github.com/jethac/lucid-loop/actions/runs/34774108170), commit `72cc6d3` | Server/Docker job and both real Live smoke tests passed, including a typed Maya wait action committed through Responses |
| English speech stream | `dotnet test tools/live_speech/Tests.csproj` | 6 passed; upstream nullable-context compiler warnings |
| Character EditMode | Actual shared Unity Editor, `character-editmode-20260913-181620-873.xml` | 95 passed |
| Character face/speech PlayMode | Actual shared Unity Editor, `character-playmode-20260913-185450-014.xml` | 9 passed after the character owner fixed root-motion ownership; original failing assertion retained |
| Encounter EditMode | Actual shared Unity Editor, `gym-editmode-20260913-185712-674.xml` | 50 passed, including LAN policy, emission reimport and primitive fall/reset |
| Mood PlayMode | Actual shared Unity Editor, `mood-playmode-20260913-185641-827.xml` | Passed exact post-update pulse scaling across 20 frames, pause, fallback and restoration |
| iOS activation | Shared Editor log `IOS_TARGET_ACTIVE: iOS` | Active target switched and player settings serialized |
| Encounter scene construction | Shared Editor log `ENCOUNTER_SCENE_CREATED` | `Assets/Gyms/Scenes/BeforeTheDrop.unity` created with server movement, voice, speech and mood bindings |
| Integrated encounter PlayMode | Actual scene and local relay, `encounter-playmode-20260913-185745-685.xml` | Passed movement, recording/approach/intervention/catastrophe, held fall without actor-root drift, HUD, and retained-clue/reset-pose restoration; three captured frames inspected |
| Lighting inventory | `python tools/audit_ios_rendering.py` | 8 encounter lights, one shadow-requesting light; 25 Lit materials, 2 material keyword sets, no emission flag inconsistencies |

Detailed Unity XML and local captures live under ignored `.local/validation/`. A malformed new test assembly metadata GUID was corrected after actual import revealed that Unity ignored the assembly definition; a subsequent duplicate TestRunner reference was also removed. Manual source compilation alone did not catch either import issue.

The initial catastrophe screenshot exposed a missing fall pose. An encounter-only primitive adapter now lowers Luca's visual, holds it until rewind and restores it on reset; the follow-up screenshot and test verify that behavior. Maya's phone/arm also follows the authoritative recording flag. These are explicit placeholders, not accepted retargeted death or recording clips.

Still pending: final action animation, on-device layout/keyboard and touch-target checks, iOS export and retained shader counts, physical iPhone networking/audio/thermal profiling, musical audition, final character animation and expressive face acceptance. English lip-sync is implemented; Japanese has no accepted producer. These results do not establish completed game or visual/audio quality.
