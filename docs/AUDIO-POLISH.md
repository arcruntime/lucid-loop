# Local audio polish — human listening checkpoint

Branch: `codex/btd-audio-polish`, forked from published MVP `95aae5d`. These changes have not been pushed or merged into Jetha’s work. The published MVP stays available as the recording fallback.

## Changes

- Same Maya voice (`gleam`). Recognition now says “Wait. That's Theo. He's famous… AND married… and that is not his wife.” to establish public recognition rather than a personal connection; captions match. Added cached-line-specific performance direction to arrival, recognition and recording. Other 13 story clips are unchanged. Only these three cache signatures change. Final selected files match the scripted transcripts according to the existing generation guard.
- Arrival: 3.4 → 4.1 seconds. Recognition: 2.9 → 9.0 seconds. Recording: 3.3 → 6.7 seconds. The final recording take intentionally gives each thought room. The user approved the audition, with the recognition wording change above. These timings establish slower delivery, not proof of acting quality. No pitch/time stretching was applied. The first recording candidate was too fast; one revised completed take was made. A server reload interrupted one preparation attempt before the final successful run.
- musicvision31’s CC0 Record Scratch #1 converted to 48 kHz stereo PCM, -3 dBFS peak ceiling and 5 ms edge fades. Added as a dedicated rewind source; normal music requests keep their existing backspin/crossfade. Background tracks are held silent while the rewind scratch completes. Manual Restart cancels it. Audio toggle mutes it.
- Submission disclosure and source/license record now include the CC0 sample. Suno tracks remain pending access and are not represented as implemented.

## Evidence

- Four live structured-dialogue checks: both natural phrasings of Maya wait/phone-away returned both actions; unsupported teleport request returned none; Luca’s generic help request returned calm intervention without naming Theo. These checks exercised the live backend over HTTP, not a new microphone recording.
- Measured request times: 9.02, 1.12, 1.36 and 1.27 seconds. Do not promise fixed low latency; make any cuts in captured waiting time visible.
- Updated Unity smoke: scratch asset load/play, scratch carrying across reset while music is suppressed, manual-restart cleanup, full two loops and voice UI/action checks passed. No test asserts subjective sound quality.
- Backend: 33 passed, one opt-in live test skipped.
- Three final speech WAVs contain no full-scale clipped samples. Existing generation validation requires normalized exact transcript match. The other cast voices and live conversation prompt are unchanged.

## Listen / accept

Open `Unity` → `Assets/Gyms/Scenes/BeforeTheDrop.unity`, use headphones and play the opening. Check Maya’s surprise/pacing and whether the scratch supports the rewind without feeling comical or abrupt. Try Restart during the scratch; it should stop. Normal Ren music changes should not use the new scratch.

Local audition: `.local/audio-polish/maya-opening-audition.wav` contains arrival, recognition and recording in that order, separated by half-second gaps. Original takes are under `.local/audio-polish/original/`; metrics and scripted live-check results are nearby. These ignored test artifacts do not go into Git.

Listening approved; the requested recognition wording has been regenerated with the exact-transcript guard passing. Do not broaden to map/avatar or recognition work while Jetha’s integration review is pending. Suno provider/key request remains outstanding.
