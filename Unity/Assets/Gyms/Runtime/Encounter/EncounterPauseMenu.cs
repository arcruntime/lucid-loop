using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    public enum EncounterPausePage { Main, Settings, Controls }

    // The server acknowledgement owns visibility. This modal never reopens voice
    // or resets the encounter; Main Menu keeps the coordinator's resume identity.
    [DefaultExecutionOrder(250)]
    public sealed class EncounterPauseMenu : MonoBehaviour
    {
        public const string MusicVolumePreferenceKey = "lucid-loop.music-volume";
        public bool IsVisible => overlay && overlay.gameObject.activeSelf;
        public EncounterPausePage CurrentPage { get; private set; }
        public RectTransform PanelRoot => overlay;
        public float MusicVolume => music ? music.MusicVolume : musicVolume;
        public bool CanQuit => !(responsiveLayout && responsiveLayout.PreviewPhoneLayout) && !Application.isMobilePlatform &&
            (Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer);

        EncounterHud hud;
        EncounterCoordinator coordinator;
        EncounterMoodPresentation music;
        EncounterHudLayout responsiveLayout;
        RectTransform safeRoot, overlay, card, mainPage, settingsPage, controlsPage, controlsViewport;
        RectTransform sliderRoot, sliderHandle, sliderHandleArea, quitButton;
        RectTransform[] mainButtons;
        readonly System.Collections.Generic.List<RectTransform> backButtons = new System.Collections.Generic.List<RectTransform>();
        Text title, volumeLabel, settingsExplanation;
        Slider volumeSlider;
        float musicVolume = .32f;
        bool preferencesDirty;
        CanvasGroup hudInteraction;
        bool interactionLocked, previousInteractable;

        public void Initialize(EncounterHud owner, RectTransform safeArea)
        {
            if (hud) return;
            hud = owner; coordinator = owner.Coordinator; safeRoot = safeArea;
            hudInteraction = safeRoot.GetComponent<CanvasGroup>();
            if (!hudInteraction) hudInteraction = safeRoot.gameObject.AddComponent<CanvasGroup>();
            responsiveLayout = safeArea.GetComponent<EncounterHudLayout>();
            music = owner.GetComponent<EncounterMoodPresentation>();
            float fallback = music ? music.MusicVolume : .32f;
            musicVolume = PlayerPrefs.GetFloat(MusicVolumePreferenceKey, fallback);
            if (float.IsNaN(musicVolume) || float.IsInfinity(musicVolume)) musicVolume = .32f;
            musicVolume = Mathf.Clamp01(musicVolume);
            if (music) music.MusicVolume = musicVolume;
            Build();
            coordinator.PauseChanged += OnPause; coordinator.StatusChanged += OnStatus;
            OnPause(coordinator.IsPaused);
        }

        void Build()
        {
            overlay = GymUI.Rect(transform, "Pause modal", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Independent sorting survives clue/conversation SetAsLastSibling.
            var modalCanvas = overlay.gameObject.AddComponent<Canvas>(); modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 1000;
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            // Exclude the underlying HUD from keyboard/controller navigation too.
            overlay.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            GymUI.Panel(overlay, new Color(.015f, .02f, .035f, .88f)).raycastTarget = true;
            card = GymUI.Rect(overlay, "Pause card", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            card.pivot = new Vector2(.5f, .5f); GymUI.Panel(card, new Color(.035f, .04f, .075f, 1));
            title = GymUI.Label(card, "PAUSED", 28, 22, 580, 50, 34, new Color(.89f, .73f, .43f));
            mainPage = Page("Pause main"); settingsPage = Page("Pause settings"); controlsPage = Page("Pause controls");
            var buttons = new System.Collections.Generic.List<RectTransform>();
            buttons.Add(MainButton("Resume", () => Resume()));
            buttons.Add(MainButton("Settings", OpenSettings));
            buttons.Add(MainButton("Controls", OpenControls));
            buttons.Add(MainButton("Main Menu", ReturnToMainMenu));
            if (CanQuit) { quitButton = MainButton("Quit", Quit); buttons.Add(quitButton); }
            mainButtons = buttons.ToArray();

            volumeLabel = GymUI.Label(settingsPage, "Music volume", 12, 12, 600, 60, 28);
            var sliderRect = GymUI.Rect(settingsPage, "Music volume slider", new Vector2(0, 1), Vector2.one, new Vector2(18, -155), new Vector2(-18, -75));
            GymUI.Panel(sliderRect, Color.clear).raycastTarget = true;
            sliderRoot = sliderRect;
            var track = GymUI.Rect(sliderRect, "Track", new Vector2(0, .4f), new Vector2(1, .6f), Vector2.zero, Vector2.zero);
            GymUI.Panel(track, new Color(.18f, .22f, .28f));
            var fillArea = GymUI.Rect(sliderRect, "Fill area", new Vector2(0, .4f), new Vector2(1, .6f), Vector2.zero, Vector2.zero);
            var fill = GymUI.Rect(fillArea, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            GymUI.Panel(fill, GymUI.Cyan);
            var handleArea = GymUI.Rect(sliderRect, "Handle area", Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-20, 0));
            var handle = GymUI.Rect(handleArea, "Handle", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            sliderHandle = handle; sliderHandleArea = handleArea;
            handle.sizeDelta = new Vector2(40, 0); GymUI.Panel(handle, Color.clear);
            var thumb = GymUI.Rect(handle, "Visible thumb", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-28, -28), new Vector2(28, 28));
            var handleImage = GymUI.Panel(thumb, Color.white);
            volumeSlider = sliderRect.gameObject.AddComponent<Slider>(); volumeSlider.minValue = 0; volumeSlider.maxValue = 1;
            volumeSlider.fillRect = fill; volumeSlider.handleRect = handle; volumeSlider.targetGraphic = handleImage;
            volumeSlider.SetValueWithoutNotify(musicVolume); volumeSlider.interactable = music;
            volumeSlider.onValueChanged.AddListener(value => SetMusicVolume(value));
            settingsExplanation = GymUI.Label(settingsPage, "Adjusts club music. Voice volume is unchanged.", 12, 178, 600, 100, 24, GymUI.Muted);
            BackButton(settingsPage);

            controlsViewport = GymUI.Rect(controlsPage, "Controls scroll", Vector2.zero, Vector2.one, new Vector2(10, 90), new Vector2(-10, -4));
            GymUI.Panel(controlsViewport, GymUI.Ink); controlsViewport.gameObject.AddComponent<RectMask2D>();
            var scroll = controlsViewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var content = GymUI.Rect(controlsViewport, "Controls text", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            var text = GymUI.Text(content,
                "WALK\nTap or click the club floor.\n\nTALK\nSelect a person or their name in the dock, then tap Talk / Walk & talk. You walk into range before voice opens.\n\nREPLY\nType a reply and tap Send. Enter sends while the reply field is focused. Mic on / Mic off toggles capture after voice is ready.\n\nLEAVE\nTap Leave to end voice and return to the club. Talking pauses the night.\n\nCLUES & HISTORY\nExpand Clues to review discoveries. History shows the selected person's conversation. Scroll long content.\n\nPAUSE\nTap Pause to open this menu. On desktop, Escape opens it, goes back from a submenu, or resumes. Escape does not interrupt typing.",
                25, Color.white);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = controlsViewport;
            BackButton(controlsPage); UpdateVolumeLabel(); ShowPage(EncounterPausePage.Main); overlay.gameObject.SetActive(false);
        }
        RectTransform Page(string name) => GymUI.Rect(card, name, Vector2.zero, Vector2.one, new Vector2(28, 24), new Vector2(-28, -92));
        RectTransform MainButton(string name, UnityEngine.Events.UnityAction action)
        {
            var rect = GymUI.Rect(mainPage, name, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            GymUI.Button(rect, name, action); return rect;
        }
        void BackButton(RectTransform page)
        {
            var rect = GymUI.Rect(page, "Back", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 68));
            GymUI.Button(rect, "Back", Back); backButtons.Add(rect);
        }

        public bool RequestOpen() => coordinator && coordinator.IsReady && coordinator.Pause(true);
        public bool Resume() => IsVisible && coordinator && coordinator.IsReady && coordinator.Pause(false);
        public void OpenSettings() { if (IsVisible) ShowPage(EncounterPausePage.Settings); }
        public void OpenControls() { if (IsVisible) ShowPage(EncounterPausePage.Controls); }
        public void Back() { if (IsVisible) ShowPage(EncounterPausePage.Main); }
        public void ReturnToMainMenu()
        {
            if (!IsVisible || !coordinator) return;
            SavePreferences(); coordinator.Disconnect();
            if (hud) hud.ShowConnectionSetup();
        }
        public bool SetMusicVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            musicVolume = Mathf.Clamp01(value); if (music) music.MusicVolume = musicVolume;
            PlayerPrefs.SetFloat(MusicVolumePreferenceKey, musicVolume); preferencesDirty = true;
            if (volumeSlider) volumeSlider.SetValueWithoutNotify(musicVolume);
            UpdateVolumeLabel(); return true;
        }
        public void HandleEscape()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            var input = selected ? selected.GetComponentInParent<InputField>() : null;
            if (input && input.isFocused) return;
            if (!IsVisible) RequestOpen();
            else if (CurrentPage != EncounterPausePage.Main) Back();
            else Resume();
        }
        void Quit() { if (!CanQuit || !IsVisible) return; SavePreferences(); coordinator.Disconnect(); Application.Quit(); }
        void Update() { if (hud && hud.isActiveAndEnabled && !(hud.RewindTransition && hud.RewindTransition.IsPlaying) && Input.GetKeyDown(KeyCode.Escape)) HandleEscape(); }
        void LateUpdate()
        {
            if (!hud || !hud.isActiveAndEnabled || !coordinator || !coordinator.IsReady) { Hide(); return; }
            if (!IsVisible) return;
            bool phone = Application.isMobilePlatform || (responsiveLayout && responsiveLayout.PreviewPhoneLayout);
            float touch = phone ? EncounterHudLayout.TouchTargetUnits(safeRoot.GetComponentInParent<Canvas>().scaleFactor) : 68;
            float target = Mathf.Max(82, touch), availableHeight = Mathf.Max(1, safeRoot.rect.height - 32);
            if (quitButton) quitButton.gameObject.SetActive(CanQuit);
            int count = mainButtons.Length - (quitButton && !CanQuit ? 1 : 0);
            int columns = 88 + count * target + (count - 1) * 14 > availableHeight ? 2 : 1;
            int rows = Mathf.CeilToInt((float)count / columns);
            float width = Mathf.Min(Mathf.Max(720, columns * target + 62), Mathf.Max(1, safeRoot.rect.width - 32));
            float height = Mathf.Min(availableHeight, Mathf.Max(620, 88 + rows * target + (rows - 1) * 14));
            card.sizeDelta = new Vector2(width, height);
            title.rectTransform.offsetMin = new Vector2(28, -64); title.rectTransform.offsetMax = new Vector2(width - 28, -22);
            foreach (var page in new[] { mainPage, settingsPage, controlsPage })
            { page.offsetMin = new Vector2(24, 16); page.offsetMax = new Vector2(-24, -72); }
            float buttonHeight = Mathf.Min(target, (height - 88 - (rows - 1) * 14) / rows);
            float cellWidth = (width - 48 - (columns - 1) * 14) / columns;
            int visible = 0;
            foreach (var button in mainButtons)
            {
                if (!button.gameObject.activeSelf) continue;
                int row = visible / columns, col = visible++ % columns;
                button.anchorMin = button.anchorMax = new Vector2(0, 1);
                button.offsetMin = new Vector2(col * (cellWidth + 14), -row * (buttonHeight + 14) - buttonHeight);
                button.offsetMax = new Vector2(col * (cellWidth + 14) + cellWidth, -row * (buttonHeight + 14));
            }
            foreach (var back in backButtons) back.offsetMax = new Vector2(0, touch);
            controlsViewport.offsetMin = new Vector2(10, touch + 20);
            volumeLabel.rectTransform.offsetMin = new Vector2(12, -60); volumeLabel.rectTransform.offsetMax = new Vector2(width - 60, -12);
            sliderRoot.offsetMin = new Vector2(18, -60 - touch); sliderRoot.offsetMax = new Vector2(-18, -60);
            sliderHandle.sizeDelta = new Vector2(touch, 0);
            sliderHandleArea.offsetMin = new Vector2(touch * .5f, 0); sliderHandleArea.offsetMax = new Vector2(-touch * .5f, 0);
            float explanationTop = 76 + touch, explanationHeight = Mathf.Max(0, height - 88 - touch * 2 - 96);
            settingsExplanation.rectTransform.offsetMin = new Vector2(12, -explanationTop - explanationHeight);
            settingsExplanation.rectTransform.offsetMax = new Vector2(width - 60, -explanationTop);
        }
        void ShowPage(EncounterPausePage page)
        {
            CurrentPage = page; mainPage.gameObject.SetActive(page == EncounterPausePage.Main);
            settingsPage.gameObject.SetActive(page == EncounterPausePage.Settings); controlsPage.gameObject.SetActive(page == EncounterPausePage.Controls);
            title.text = page == EncounterPausePage.Main ? "PAUSED" : page == EncounterPausePage.Settings ? "SETTINGS" : "CONTROLS · SCROLL";
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
        void OnPause(bool paused)
        {
            if (!paused || !coordinator || !coordinator.IsReady) { Hide(); return; }
            if (!interactionLocked)
            { previousInteractable = hudInteraction.interactable; hudInteraction.interactable = false; interactionLocked = true; }
            ShowPage(EncounterPausePage.Main); overlay.gameObject.SetActive(true);
        }
        void OnStatus(string status) { if (!coordinator || !coordinator.IsReady) Hide(); }
        void Hide()
        {
            if (overlay && overlay.gameObject.activeSelf) { overlay.gameObject.SetActive(false); SavePreferences(); }
            if (interactionLocked) { if (hudInteraction) hudInteraction.interactable = previousInteractable; interactionLocked = false; }
        }
        void UpdateVolumeLabel() { if (volumeLabel) volumeLabel.text = music ? "Music volume  " + Mathf.RoundToInt(MusicVolume * 100) + "%" : "Music is unavailable in this scene"; }
        void SavePreferences() { if (!preferencesDirty) return; PlayerPrefs.Save(); preferencesDirty = false; }
        void OnApplicationPause(bool paused) { if (paused) SavePreferences(); }
        void OnDisable() { Hide(); SavePreferences(); }
        void OnDestroy()
        {
            SavePreferences();
            if (coordinator) { coordinator.PauseChanged -= OnPause; coordinator.StatusChanged -= OnStatus; }
        }
    }
}
