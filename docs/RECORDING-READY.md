# Recording handoff — September 14

Use the local `codex/btd-audio-polish` branch, Unity project `Unity`, scene `Assets/Gyms/Scenes/BeforeTheDrop.unity`. The tested polish is published on `codex/btd-checkpoint-one`; the original baseline remains at commit `95aae5d`. Local development uses `codex/btd-audio-polish`. Do not record from the nested `Unity/My project` folder.

## Accepted / changed

Arc confirmed Ren's line is no longer cut off; dialogue punctuation, Luca's run, music and transitions are good. Keep the larger map and simple avatars. Theo's apparent extra legs were rigid ankle-length coat panels: the fix shortens them to the upper thigh, adds subtle movement, and distinguishes dark trousers from the green coat. Check Theo once at gameplay camera distance before the final take.

## Capture in this order

1. Start the local dialogue server using `tools/start-dialogue.command`. Confirm live mode is ready. Use headphones. Record a short sample that includes both game sound and your microphone, then play it back before a full take.
2. Record one complete two-loop run at landscape 1920×1080 if practical. Keep subtitles legible. Run loop one along the intended right-hand route: Maya sees Theo, records, Theo approaches, Luca runs in, catastrophe, Ren's line, rewind. Leave Ren's line and rewind intact in the edit.
3. In loop two head left. Request Intimate from Ren. Ask Maya: “Wait here while I ask the bartender something, and keep your phone away.” Show the AI observer's actual accepted actions and Maya remaining in place as you move. Keep this request, response and visible result together.
4. Ask Luca to help calmly if things get awkward. Return to Maya and ask her to follow. Approach the same encounter and capture the safe outcome. Follow the actual responses: if an action was declined, resolve it in-game rather than implying it happened.
5. Capture a few seconds of clean club movement and the success state without the observer. Keep the full unedited run as backup. This pass does not require new art, extra mechanics or an iPhone export.

Use [the 59-second beat sheet](SUBMISSION-DRAFT.md#59-second-captureedit-plan). The greatest share of screen time should show a real player request, the character's response, the accepted action and its visible consequence. The observer must display actual results; the rewind remains an authored game rule, not an AI-generated danger score. Label edits that shorten waiting/travel; do not splice a response from a different request.

## CapCut artwork

The original PNGs are saved without modification under [media/branding](media/branding/README.md). Use the clean `Before-the-Drop-title-card.png` for the closing card; use the game logo over footage only after checking its transparency/edges in the preview. The team logo belongs on the final card, with short readable text if needed. Keep all branding within the existing five-second closing slot rather than adding a lengthy intro. The supplied artwork is not installed in the Unity scene, so it adds no startup or gameplay interruption.

## Remaining submission tasks

- Arc: brief Theo visual check; capture, edit and export a video under 60 seconds; upload and check that judges can open it without signing in.
- Steph: final approval of submission copy and branding attribution/authorship details. Review remains pending in our notes until the team confirms it.
- Jetha: demo/integration feedback and confirmation of the music generation account/usage terms. Received files are integrated; no API key is needed for playback.
- Team representative: enter team roster/email, track, final title, video URL and the approved form answers. Deadline from the supplied instructions: September 15, 11:59 PM JST. Keep links accessible through September 17. Working demo and code URLs are optional; do not claim an iPhone build has been validated.

Known limitations: Maya's VIP recognition coverage remains a reported issue pending coordination; use the tested opening/capture route. Production avatars and a new environment are deferred. Theo's visual fix has automated geometry coverage but needs the brief human appearance check above. The final uploaded video, team approvals and submission form are not completed by this handoff.

Validation for this recording candidate: Unity compile and full two-loop smoke passed, including Theo’s two articulated legs/short coat geometry, Luca’s urgent run and speed restoration, Ren playback protection, scratch/reset handling, and voice UI fixture checks. The final gameplay capture was visually inspected. This does not replace checking the actual captured microphone/game audio or the uploaded video.

Visibility/pathing pass: memories collapse, portraits stay lower-left, and the observer shifts left for authored story beats. Theo/AP remain visible at recognition. AP wears burgundy. Six planter exclusions and a narrowed VIP connection keep characters on the intended aisles/stair approach. Before recording, walk the entrance/plant detour and watch Theo descend the VIP route once.

Final pass validation: both loops, all six planter footprint exclusions, complete VIP path through the stairs, Luca running and restoration, Theo geometry, Ren/scratch timing and voice UI fixture checks passed. Recognition screenshots with observer on/off were checked for scene visibility.

Conversation/stage follow-up: portraits now sit beside live chat controls, labels have dark backplates, and PC/Maya request music from the dancefloor. Ren is rendered behind the decks; the camera pans to her before her line and rewind. Full two-loop tests, portrait non-overlap and DJ approach checks passed; conversation, DJ request and rewind-focus captures were visually checked. The scene is still a painted 2.5D MVP, not a rebuilt elevated stage.
