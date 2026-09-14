# Before the Drop — submission draft

Team: Lucid Loop. Project: Before the Drop (team to confirm final title). Track: AI-Native Game Prototype (Track 1 per PM brief). Representative/member details and video URL must be supplied by the team representative.

Based on the organizer email supplied by Arc: required video up to one minute; deadline September 15, 2026, 11:59 PM JST; links accessible through September 17. Working demo and code URLs optional. Target a 59-second export. No form has been submitted.

## Project description

Before the Drop is a narrative time-loop game set inside a nightclub. You arrive with Maya, witness a confrontation turn potentially fatal, and return to the entrance with a chance to change what happens.

Explore an isometric club, request a different musical mood, and speak to characters in your own words. OpenAI-powered characters interpret requests through their personalities, current knowledge and musical context. Their supported decisions change what characters actually do: wait, follow, keep a phone away or prepare to intervene.

The short playable experience follows two attempts at the same encounter: witnessing the catastrophe, then using conversation and preparation to prevent it. Illustrated conversations, distinct voices and contrasting music make social interaction the central gameplay.

The MVP is playable in Unity Editor, with mobile as the target platform. The next deployment milestone is validation on iPhone.

## Meaningful use of OpenAI — 30%

OpenAI connects natural conversation to playable character actions. Live voice conversations use a structured decision layer powered by GPT-4.1-mini. It receives the speaking character’s personality, limited knowledge, musical mood and conversation history, then returns an in-character reply and supported actions.

Players phrase requests themselves instead of selecting a dialogue-tree option: one utterance can combine “wait here” and “keep your phone away,” and the model selects both actions. Unity validates and applies them; Maya stays behind when the player walks away. The voice pipeline returns the approved response after the game acknowledges the decision.

The opening, rewind and outcome conditions are authored. AI interprets player intent within that structure rather than inventing unrestricted game mechanics. A demo observer exposes the request, selected actions and application status. Fixed story speech is cached separately from live dialogue. Codex assisted development; OpenAI image generation adapted the club concept into playable scenery.

## Originality — 25%

The player’s main instrument is language. Before the Drop combines a nightclub social drama with time-loop experimentation, letting players use what they witnessed to prepare a different encounter.

Music supplies emotional context: Intimate and Aggressive tracks influence how characters respond, without guaranteeing agreement or determining success alone. Characters retain their own personalities and limited knowledge; the player remembers the previous attempt, while NPC conversations begin afresh.

The same room becomes a different social situation when Maya agrees to put away her phone or Luca agrees to help early. Progress comes from coordinating people through conversation, then returning to see the consequences. The DJ’s interruption connects the nightclub setting directly to the rewind premise.

## Playability / Utility — 25%

The Unity Editor MVP supports the complete opening, catastrophe, rewind, preparation and successful prevention sequence, with a restart option. Arc playtested both loops; automated scene checks also exercised the progression and outcome conditions.

Click-to-move exploration connects conversation to visible world behavior. Players can ask Maya to wait or follow, choose music through explicit buttons, enlist Luca’s help, and accept Theo’s invitation into VIP.

Live speech includes captions, conversation history and a typed alternative. The second attempt presents a clear objective and a concrete success: the confrontation passes without Luca collapsing. Choosing music alone or avoiding the encounter does not complete the game.

Mobile is the target platform; iPhone deployment and performance validation are the next milestone. The submitted capture demonstrates the Editor build.

## Execution and craft — 20%

The MVP combines a human-reviewed two-loop experience with automated verification: 14 Unity EditMode tests and 33 backend tests passed, with one opt-in live test skipped. Scene checks cover navigation, rewind, prevention, conversation history, VIP movement and stale or duplicate voice-action rejection. A separate live API check exercised spoken input through to character action.

A painted isometric nightclub, following camera, illustrated portraits and distinct cast voices establish the setting. Contrasting tracks, a backspin transition and dialogue ducking make music an audible part of the experience. Fixed opening lines are bundled as audio to avoid generation delays during the introduction.

The demo observer makes model-selected actions visible while distinguishing authored story events. Simple world avatars remain the accepted MVP presentation; refined clothing and further environment work are planned polish.

## Pre-existing materials, tools and licenses — factual draft

All team-authored game code, story and art were created during the official challenge window beginning September 11, 2026, at 8 PM JST. Lucid Loop started fresh; no code or assets were migrated from the separate openai-hackathon-game exploratory repository.

Pre-existing dependencies include Unity 6.3 LTS and packages under applicable Unity/package terms; Node.js (MIT); ws (MIT); osu-framework-unity-di (MIT); R3 (MIT); and Newtonsoft.Json (MIT), whose Unity package wrapper uses the Unity Companion License. Bundled Microsoft/.NET support-library notices are retained.

Steph created the source character and club art with ChatGPT; the team confirms permission to use it. OpenAI image generation adapted that art into game presentation assets. OpenAI APIs provide speech and dialogue decisions; Codex assisted development. These services are used under their applicable service terms. Jetha supplied Suno-generated music; the normal transition cue is procedural. The rewind uses “Record Scratch #1” by musicvision31 (Freesound, CC0 1.0). Figma supported design; CapCut is the planned video editor.

The shipped MVP excludes the local Quaternius audition. No separate dataset was introduced by this MVP pass.

Editorial record (not form text): Arc confirmed build-period provenance, source-art authorship/permission and no exploratory-repo migration on September 14. See LICENSE-AND-PROVENANCE.md for evidence. Confirm the final recording still uses this asset set before submission. Each form section above is below 200 words.

## 59-second capture/edit plan

| Time | Picture | Sound / message |
|---|---|---|
| 0–5 | Club movement, title over gameplay | “A night out turns deadly. You get another chance.” |
| 5–13 | Maya recording, Theo confronts, Luca intervenes and falls | Actual dialogue fragments; “First attempt.” |
| 13–17 | Ren, interruption, rewind, entrance | Preserve “Not on my dancefloor” and rewind audio. |
| 17–22 | Go left; select Intimate | Hear the music change. “Try a different approach.” |
| 22–38 | Real Maya request, actual response, then walk away while she stays | “Wait here while I ask the bartender something—and keep your phone away.” Observer shows applied actions. Preserve one continuous request/response/action take if possible. |
| 38–45 | Real Luca request and agreement | Establish his preparation; no fabricated dialogue. |
| 45–54 | Return with Maya, quiet approach, calm intervention, safe outcome | “Same encounter. Different outcome.” |
| 54–59 | Club, title and Lucid Loop | “What would you say differently?” |

Record one complete successful run first, including game audio and microphone. Edit travel with clear cuts. Do not combine unrelated requests/responses or imply cached opening speech is live generation. If latency is edited, make the cut visible. Use readable captions and duck music beneath speech. Record before attempting optional evening art work; retain this baseline capture. Verify final URL without signing in. Aim to submit a valid entry several hours before the deadline.
