# Retained information UI

Branch: `feature/learned-information`, based on main. This implements only the top-right indicator → compact banner → left information panel flow. No loop title screen, automatic toast, pause menu, or rewind animation is added.

## Try it

Open `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity`, then **Lucid Loop → Information → Preview three-step flow**. The offline Editor preview supplies the four example clues without modifying a server game. Click the gold diamond, then the banner. Close the panel using its top-right ×. The now-muted outlined diamond reopens the list. **Capture three-step flow** records the stages to `.local/information/<resolution>` during Play mode.

The scene has a configured manager, UI controller, and encounter bridge on the existing encounter object. **Lucid Loop → Information → Set up encounter UI** creates/reconnects these components in another encounter scene. Repeating it does not duplicate them. The previous retained-clues box is hidden to avoid duplicate knowledge interfaces; the rest of the HUD is preserved.

## Data and public API

`Assets/Gyms/Runtime/Information/LearnedInformationManager.cs` stores serializable entries with a unique ID, content, category, monotonic order, unread flag, and retained/published flag. IDs deduplicate repeated authoritative updates; read state survives loop changes and reconnects within this client session. Knowledge remains authoritative on the server. This UI does not introduce a new save-file format or persist read receipts across application restarts.

```csharp
// Publish local, already-confirmed information immediately; the unread icon appears.
informationManager.AddLearnedInformation("Theo is having an affair.");

// Prefer stable IDs for data received repeatedly from gameplay/server events.
informationManager.AddLearnedInformation("theo_affair", "Theo is having an affair.");

// Hold a discovery until the next successful loop restart.
informationManager.AddLearnedInformation(
    "vip_confrontation", "Maya will confront Theo if she sees him in VIP.",
    category: "behavior", deferUntilLoopStart: true);
informationManager.NotifyLoopStarted();
```

`MarkRead(IEnumerable<string> ids)` marks selected retained entries read. `ClearForNewGame()` starts a separate knowledge collection. `Changed`, `HasUnread`, `MostRecentUnread`, `HasRetained`, and read-only `Entries` drive presentation. Empty input is rejected; duplicate IDs do not create notifications or replace read flags.

`EncounterInformationBridge.cs` consumes the existing `PlayerDiscoveries` records (`factId`, `text`, `learnedInLoop`) and actual loop index/ID changes. New current-loop discoveries are deferred. A successful higher loop index publishes them; an initial resumed later-loop snapshot can also populate retained clues. Repeated snapshots do not re-alert read entries. Starting a different game clears this client's collection. The panel closes before publication at a loop boundary, preventing a previously open panel from silently reading newly retained clues.

The repo's main encounter uses server-authoritative movement and treats Maya as a separate NPC. This UI does not change any character hierarchy, movement system, or scene geometry.

## UI hierarchy and wiring

`InformationNotificationUI.cs` creates one Canvas-based hierarchy and initializes `LearnedInformationPanelUI.cs`:

```text
Learned information canvas (Canvas, CanvasScaler, GraphicRaycaster)
└── Safe area
    ├── Unread information indicator (Button, CanvasGroup)
    │   └── Gem (assignable sprite or procedural diamond)
    ├── New information banner (Button, CanvasGroup)
    │   ├── Notification title (TMP)
    │   ├── Banner diamond + hairline
    │   └── Newest information (TMP)
    └── Learned information panel (CanvasGroup)
        ├── Panel title (TMP) + hairline
        ├── Close learned information (Button)
        └── Information scroll (ScrollRect, RectMask2D)
            └── Entries (data-driven rows with diamond, TMP content, NEW tag)
```

Buttons are wired automatically: icon → `ClickIcon()`, banner → `ClickBanner()`, close → `Panel.Close()`. The banner selects the newest unread retained entry; the list is newest first and scrolls for longer collections. Opening the panel marks listed entries read, retaining NEW emphasis for that viewing session. New published entries arriving while the panel is visible are immediately listed/read. Deferred current-loop discoveries wait for the next loop.

The icon stays gold while unread items exist. After reading it becomes a quiet outline (or `ReadIconSprite`) and remains available to reopen the list. No icon is shown for an empty retained collection. The banner is opened only by clicking the icon and is dismissed by opening the list or clicking the icon again.

Fonts, icon sprites, colors, heading/body sizes, margins, row spacing, panel/banner widths, pulse strength/period and animation duration are editable on `InformationNotificationUI`. Styles are applied when the UI is built; size adapts during viewport changes. `InformationGemGraphic.cs` and `InformationUIStyle.cs` provide separate procedural graphics and TMP/UI factories. The heading uses bundled Cormorant Garamond with its SIL Open Font License; text uses the installed TMP Essentials Liberation Sans asset. No package upgrade is needed.

Animations use unscaled time but do not change time scale or pause gameplay/audio. Only UI hit areas intercept pointer input; the game remains interactive outside the panels. Main's existing UI-aware floor-tap handling also checks a brief consumed-click guard so closing a panel cannot issue a movement command. The safe area, adaptive widths and scroll viewport support desktop, ultrawide and portrait layouts. Device touch targets use the project's existing 44-point sizing convention.

For another game, use the manager/UI directly and call the public methods from its authoritative knowledge/reset events. If its movement code does not respect EventSystem UI hit testing, add the same check plus `InformationNotificationUI.SuppressFloorTap` before issuing a floor destination. No dedicated camera, input package, or paid plugin is required.

## Validation

Unity 6000.3.24f1 / URP 17.3.0 / Metal: compilation and repeatable setup passed. Eight distinct checks passed across the final runs: seven information data/UI tests (`.local/information-pointer-tests.xml`) and the real encounter's opening → catastrophe → authoritative reset → retained-information flow (`.local/information-verified-tests.xml`). Coverage includes duplicate IDs/snapshots, ordered newest-unread content, panel read/NEW state, reopening, unchanged time scale, UI disable cleanup, real UI raycasts, and keeping new notifications unread when a loop restarts with the panel open.

The pre-existing localhost:8789 relay timed out waiting for world updates. The same encounter test passed against a temporary relay built from this source at localhost:8792, with no API key or OpenAI calls. That temporary relay was stopped; the original relay was left running.

Live Editor captures were inspected at 2560×1440 and 2520×1080. The offline preview hides the connection form, as connected gameplay does; the other existing main HUD elements remain. Editor captures wait for first-use shader compilation before saving text. Pointer hit testing and button callbacks were tested automatically; native automation clicks in this Editor session did not reliably activate either the new or existing HUD buttons, so a human mouse/touch check remains useful. Physical iPhone touch/performance, gamepad navigation, and persisted read receipts across app restarts have not been tested.

16:9 screenshots: [unread icon](images/information/01-unread-16x9.png) · [notification banner](images/information/02-banner-16x9.png) · [learned-information panel](images/information/03-panel-16x9.png) · [read indicator](images/information/04-read-16x9.png).

Ultrawide: [banner](images/information/02-banner-21x9.png) · [panel](images/information/03-panel-21x9.png).
