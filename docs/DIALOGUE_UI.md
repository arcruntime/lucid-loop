# Art deco conversation panel

Implemented on `feature/art-deco-dialogue`, based on main, independently of the player-marker branch. This changes the main encounter HUD; it does not modify the separate checkpoint demo.

The expanded conversation uses a centered dark panel (78% of safe width, capped at 1440 canvas units), procedural gold borders and a character nameplate. The expanded panel omits the portrait and keyboard hints, matching the revised mobile reference. Selecting a character updates the name and role. History remains scrollable; reply, microphone, send and leave keep their existing callbacks and eligibility. The reply bar contains tappable microphone and send icons. Microphone input remains a toggle; the pink ring is driven by actual microphone-enabled state, not simulated audio. An audio-level waveform is not implemented. No new image assets or shaders are required for the interface.

Open `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity` and press Play. Select a character and choose History to inspect the expanded panel without an API key. Conversations still require the normal relay setup. Leave restores the compact exploration dock.

No dedicated per-character conversation backgrounds were found in the current art assets. The current club/camera presentation remains behind the panel.

## Validation

Unity 6000.3.24f1 compiled successfully. `Lucid Loop > Validate art deco dialogue` passed the actual-scene PlayMode test for portrait/hint removal, icon presence, non-interactive ornament, input text retention, phone-preview safe bounds and return to exploration. Desktop and phone-preview screenshots were visually inspected. The screenshot dialogue is a test fixture copied from the supplied design reference, not a new authored story event or live AI response.

The panel accounts for keyboard inset and retains Leave inside the panel; physical iPhone keyboard, touch-target acceptance and live voice testing remain unverified for this change.

![Phone-layout preview](images/encounter/art-deco-dialogue-phone.png)

## Separate checkpoint preview

The following capture shows the local checkpoint adaptation, included for visual comparison only. Its runtime code and preview shortcut are not included in this main-branch change. No live provider session was started for the capture.

![Checkpoint demo conversation preview](images/encounter/art-deco-dialogue-demo.png)
