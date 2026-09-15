# NPC dialogue and speech waveform

Main branch: `feature/npc-dialogue-waveform` (based on main).
The same reusable UI is also installed in the separate local
`feature/demo-integration` checkout at `.local/demo-integration/Unity`.

## Run

Open `Assets/Gyms/Scenes/BeforeTheDrop.unity` and enter Play mode.
Main automatically attaches the dialogue adapter to the existing encounter HUD.
Connect using the existing game/voice service and select a character to talk.
The new panel replaces the old conversation feed while a conversation is open.

In the checkpoint demo, Loop 2 conversations with Maya, Luca, Theo and Ren use
this panel. Authored opening story lines retain their existing presentation.
Theo's accepted VIP invitation remains available as a contextual panel action.
Ren accepts music requests through the existing AI actions.

**Live configuration:** main keeps its existing encounter relay configuration.
The demo needs `tools/start-dialogue.command` running in its integration checkout,
with a valid server-side OpenAI credential. It uses ports 8082 and `/mvp-live`.
No credentials are stored in Unity assets. The demo's existing voice endpoint now
accepts `mvp.text`, uses the same adjudicator and waits for a successful game
commit before approving generated speech. If initial speech connection fails,
typed requests fall back to the existing HTTP text-only dialogue path.

## Offline test

In main Play mode, choose **Lucid Loop → Dialogue → Preview Theo interface**.
This shows the requested blackout dialogue with an explicit offline-test label.
Sending any reply plays the assigned prerecorded Theo sample, displaying its
actual line: “Keep your voice down. This is none of your business.”
The microphone is hidden in this mode; no API calls or microphone access occur.

**Capture and validate assigned speech** plays the same sample, checks measurable
AudioSource output, checks waveform decay after playback and immediate reset on
close, and writes idle/speaking screenshots into `.local/dialogue-waveform-captures`.
The demo has an equivalent **Dialogue → Capture assigned speech sample** command.
The sample is existing AI-generated checkpoint story audio, reused from
`MvpAudio/Story/quiet.wav`, not a newly generated or third-party voice recording.
Cormorant Garamond uses its bundled OFL license; TMP Essentials supplies the sans
font and standard UI shaders. No URP/package upgrades were needed.

## Scripts and hierarchy

All reusable code is in `Assets/Gyms/Runtime/Dialogue/`:

- `DialogueBoxController.cs`: generated Canvas/TMP interface, current response,
  typed submission, optional player mic, unscaled presentation, conversation epoch,
  context action, close/reset and assigned-clip test mode.
- `DialogueOrnament.cs`: replaceable procedural gold frame/nameplate and mic icon.
- `NpcVoiceWaveform.cs`: 35 fine strokes, audio-driven heights, tapered silhouette,
  attack/release smoothing and layered UI glow without scene bloom.
- `NpcSpeechMeter.cs`: bounded 12-second PCM history, generation guard and RMS
  amplitudes indexed by consumed playback samples, plus dedicated AudioSource mode.
- `EncounterDialogueBoxBridge.cs`: main encounter/voice integration.
- `DialogueSpeechReview.cs`: Editor-only recorded-speech validation/capture.

The controller reuses a parent Canvas when supplied; otherwise it creates one
owned overlay Canvas. It reuses the existing EventSystem. Under the safe-area
root are the panel background, decorative frame, nameplate with separate TMP
name/role, bounded response ScrollRect, status, reply row, clipped TMP input
viewport, NPC waveform, player mic button, submit button, desktop hint, close
button and optional contextual action. The UI displays the current NPC response,
not a scrolling conversation history.

Fonts, color, dimensions, margins/padding, border sprite, waveform sensitivity,
attack/release, glow and fade duration are editable on DialogueBoxController.
The microphone is optional. Desktop V starts push-to-talk when the reply field
is not being edited; while typing, V remains a letter. Enter submits. Mobile
hides keyboard hints and offsets the panel above the reported on-screen keyboard.
The mic toggles capture independently from NPC speech; releasing push-to-talk in
the demo keeps the voice output connection alive to deliver the response.

## Playback integration

Main subscribes to the existing `StreamStarted`, `Pcm16Output`,
`PlaybackProgress`, `StreamStopped` events. PCM receipt stores data only;
**consumed samples**, reported by the existing DSP/iOS playback implementation,
advance the waveform. No waveform is driven by transcription or request timing.
The UI never samples the AudioListener, music, environment or microphone.

The checkpoint adapter is `Runtime/Mvp/FirstLoop.SpeechDialogue.cs`. It samples
`GetOutputData` only on the dedicated NPC voice AudioSource while that source is
playing. The stream may keep a silent clip running: silent output makes the
waveform settle. Closing invalidates the existing request epochs, stops voice,
and immediately resets the waveform.

For another compatible speech player:

```csharp
box.Open("Theo", "The Socialite", microphoneAvailable: false);
int conversation = box.Epoch;
box.Submitted += text => existingDialogueService.Send(text);
// Only deliver results still belonging to this conversation:
box.SetResponse(replyText, conversation);
box.Meter.Begin(streamGeneration);
box.Meter.Push(pcm16Bytes, streamGeneration); // queue only
box.Meter.Advance(streamGeneration, actuallyConsumedSamples, ended: false);
// On interruption/close:
box.InterruptSpeech();
box.Close();
```

The existing providers retain authority over conversation/turn identity and
late-event rejection. There is no claim of word-by-word text synchronization.
No new speech recognition system or synthetic/random waveform was introduced.
Music behavior remains with each game's existing audio owner; this UI adds no
independent music-volume writer.

## Validation

- Five dialogue PlayMode tests passed after the player-transcript update: queued
  versus played PCM, stale generation/end reset, late response/close cleanup,
  reply/waveform bounds and submission, and player transcript persistence,
  layout separation and conversation-epoch rejection. The existing dedicated
  voice/music DSP isolation check passed during the earlier waveform validation.
- Five demo voice protocol tests passed, including typed request adjudication,
  speech only after game commit, cancelled decisions and retained history.
- Actual assigned NPC speech drove the waveform and settled correctly in the
  visible Editor (`DIALOGUE_SPEECH_OK`). Main 16:9 and demo ultrawide layouts
  were inspected.
- Unity compilation succeeded for main and the combined demo.

Live microphone transcription was not retested after the player-transcript update.
The checked-in screenshots show the earlier waveform UI, before the new transcript
area was added. The demo-specific voice transport and integration changes live
in the separate local demo checkout; this main-branch change supplies the shared
UI and main encounter adapter.
The iPhone build, device microphone permissions and on-device keyboard/native
speech waveform still require device validation. Screenshots are labeled offline
where they use prerecorded sample audio.

## Player transcript

Both main and the combined demo show the latest recognized player speech in a
separate **You said:** area above the NPC response. It updates as the existing
service delivers transcript fragments and stays visible while the NPC answers.
Typed submissions appear there too. Long text can be scrolled without covering
the reply controls. Opening another conversation clears it; old conversation
epochs cannot replace the current transcript. Inspector controls on
`DialogueBoxController`: `PlayerTranscriptHeight`, `PlayerTranscriptFontSize`,
and `PlayerTranscriptLabel`. No new speech service or credentials are required.
