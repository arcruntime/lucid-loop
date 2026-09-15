# Victory ending overlay

On `feature/victory-screen`, based on main. No dependency on the vinyl rewind, marker, or dialogue branches. The real nightclub remains behind the UI; no background image is used.

## Preview and customize

Open `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity`, then choose **Lucid Loop → Victory → Preview ending**. This offline preview needs no relay or API key. It captures a review PNG in `.local/victory` after the reveal. The preview does not declare a real game victory or reset server knowledge.

The main encounter contains `VictoryScreenController` and `EncounterVictoryPresentation`. Use **Lucid Loop → Victory → Set up encounter overlay** to install/reconnect them in another encounter scene; repeating setup reuses the components. Inspector fields expose text, TMP font overrides, heading size/line spacing, small-text tracking, colors, circle size, ornament/subtitle spacing, dim/vignette/glow strength, reveal timing and readability delay. Runtime UI children remain separate editable TMP and procedural graphic objects, under a shared canvas and composition parent.

`ShowVictoryScreen()` opens the overlay once; `HideVictoryScreen()` closes it. `IsOpen`, `CanContinue`, and static `InputBlocked` expose its state. Inspector UnityEvents are `Shown`, `Continued`, and `Closed`. For another game, connect these events to its pause/input lifecycle. `SubmitInput(held, freshPress)` supports custom input adapters; the default poll uses this project's legacy keyboard, mouse, joystick-button and touch input.

The main adapter observes the existing server-reported `victory` phase and deduplicates by loop ID. Catastrophe, unresolved nights, and normal restarts do not show this screen. While open, it hides/restores ordinary canvases and character labels, closes voice, cancels queued approaches, clears NavMesh paths and suspends registered actors/action presentation. The coordinator rejects movement, conversations and reset commands during the overlay. No global time scale or audio-listener pause is changed. Additional project-specific behavior can be registered in `AdditionalPausedActivity`.

Reveal timing is unscaled: background first, arcs/glints and extended lines, title, subtitle, then the prompt. After the reveal plus readability delay, input must be released before a fresh press can continue. The default adapter returns to the existing connection screen without resetting progress. Disable `ReturnToConnectionOnContinue` and assign `Continued` to route elsewhere. The blocker remains for the input frame to avoid clicking through into gameplay.

## Artwork and dependencies

The heading uses [Cormorant Garamond](https://github.com/google/fonts/tree/main/ofl/cormorantgaramond), bundled with its SIL Open Font License in `Assets/Gyms/Resources/Victory`. Small text uses the bundled TextMeshPro Liberation Sans asset. TMP Essentials were imported unchanged from the installed Unity UI package; packages were not upgraded. Optional TMP font overrides allow replacing either typeface.

`VictoryDecoration.cs` draws separate fading hairlines, concave four-point glints, a few uneven partial arcs and a soft vignette. Text is rendered sharply by TMP outside any scene distortion. Blur is omitted; dimming and vignette provide readability. No shader/render feature, screenshots, logos, or video are used by the runtime overlay. `VictoryReviewCapture` is Editor-only evidence capture.

## Validation

Unity 6000.3.24f1, URP 17.3.0, Metal. Inspected the live main encounter at 2560×1440 and 2520×1080: centered two-line title, circular UI traces, crisp ornaments, and separated prompt. The visible bright rings on the dance floor belong to the existing nightclub, separate from the faint overlay arcs.

Screenshots: [16:9](images/victory/victory-16x9.png) · [21:9](images/victory/victory-21x9.png).

All five final Play Mode checks passed. They cover victory phase filtering, repeated-show deduplication, input delay/release and one continuation, unscaled reveal, disable/reopen cleanup, HUD restoration, and the procedural graphics' renderer requirement. Test results are recorded locally in `.local/victory-tests-final.xml`.

A full paid/live gameplay run through the winning conversation path, physical gamepad input, iPhone touch input/performance, and device font appearance have not been tested. The source integration uses the existing authoritative victory event; no new success condition is introduced.
