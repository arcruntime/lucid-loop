using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LucidLoop.Gyms
{
    public sealed class EncounterHud : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public EncounterVoiceController Voice;
        public GymCamera Rig;
        public string DefaultGameAddress = "ws://127.0.0.1:8080/game";
        public string SelectedNpcId { get; private set; } = "maya";
        public EncounterInteractionMarkers InteractionMarkers { get; private set; }
        public EncounterPauseMenu PauseMenu { get; private set; }
        public event Action<string> TypedReplyRequested;
        public event Action<bool> MicrophoneRequested;
        public event Action LeaveRequested;

        RectTransform canvas, connectionPanel;
        EncounterHudLayout responsiveLayout;
        Text stateLabel, clueLabel, statusLabel, speakerLabel, transcript, guidance;
        RawImage speakerPortrait;
        InputField address, access, reply;
        Button send, mic, resume, pause, reset, openingRoute, talk;
        ScrollRect historyScroll;
        bool microphoneEnabled, wired;
        bool paused;
        readonly EncounterTapGesture floorTap = new EncounterTapGesture();
        bool suppressTouches;
        string historyText = "", previousRole;
        const int MaxDisplayedCharacters = 24000;
        static readonly Color Gold = new Color(.89f, .73f, .43f);

        void Start()
        {
            if (!Coordinator) Coordinator = GetComponent<EncounterCoordinator>();
            if (!Voice) Voice = GetComponent<EncounterVoiceController>();
            if (!Coordinator) { enabled = false; return; }
            Build(); Wire(); ShowState(Coordinator.State); ShowStatus(Coordinator.Status);
            UpdatePortrait();
            canvas.gameObject.AddComponent<EncounterDialogueStyle>().Initialize(this, responsiveLayout);
        }

        void Build()
        {
            canvas = GymUI.Canvas("Before the Drop HUD");
            var top = GymUI.Box(canvas, "Encounter state", 24, 24, 1275, 116); GymUI.Panel(top, GymUI.Ink);
            var headerTitle = GymUI.Label(top, "BEFORE THE DROP", 20, 10, 850, 40, 30, Gold);
            stateLabel = GymUI.Label(top, "Connect to begin", 20, 58, 1160, 36, 23);
            var connectionButton = GymUI.Button(GymUI.Box(top, "Connection settings", 1070, 12, 185, 43), "Connection", () => connectionPanel.gameObject.SetActive(!connectionPanel.gameObject.activeSelf));
            var clueBox = GymUI.Box(canvas, "Retained clues", 24, 156, 430, 158); GymUI.Panel(clueBox, GymUI.Ink);
            var clueToggle = GymUI.Button(GymUI.Box(clueBox, "Toggle clues", 12, 8, 406, 54), "Retained clues - expand", () => responsiveLayout.ToggleClues());
            var clueViewport = GymUI.Rect(clueBox, "Clue scroll", Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-18, -76));
            GymUI.Panel(clueViewport, GymUI.Ink); clueViewport.gameObject.AddComponent<RectMask2D>();
            var clueScroll = clueViewport.gameObject.AddComponent<ScrollRect>(); clueScroll.horizontal = false;
            var clueContent = GymUI.Rect(clueViewport, "Clue content", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            clueContent.pivot = new Vector2(.5f, 1);
            clueLabel = GymUI.Text(clueContent, "None yet", 32, Color.white);
            clueLabel.verticalOverflow = VerticalWrapMode.Overflow;
            clueContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            clueScroll.content = clueContent; clueScroll.viewport = clueViewport;
            pause = GymUI.Button(GymUI.Box(canvas, "Pause encounter", 24, 830, 200, 58), "Pause", () => Coordinator.Pause(!paused));
            reset = GymUI.Button(GymUI.Box(canvas, "Rewind encounter", 242, 830, 212, 58), "Rewind", () => Coordinator.Reset());
            var guide = GymUI.Box(canvas, "Encounter guidance", 24, 916, 1275, 140); GymUI.Panel(guide, GymUI.Ink);
            guidance = GymUI.Label(guide, "Start a night, then tap the floor to walk. Select someone nearby and tap Talk.", 20, 18, 900, 108, 24);
            openingRoute = GymUI.Button(GymUI.Box(guide, "Opening route", 950, 36, 305, 65), "Walk toward Theo", () =>
            { Leave(); Coordinator.SendDestination(new Vector3(10, 0, -1.7f)); });

            var panel = GymUI.Rect(canvas, "Conversation", new Vector2(1, 0), Vector2.one, new Vector2(-585, 24), new Vector2(-24, -24));
            GymUI.Panel(panel, GymUI.Ink);
            speakerLabel = GymUI.Label(panel, "Maya / Close friend", 80, 18, 455, 48, 30, Gold);
            string[] ids = { "maya", "ren", "luca", "theo" }, names = { "Maya", "Ren", "Luca", "Theo" };
            for (int i = 0; i < ids.Length; i++)
            { string id = ids[i]; GymUI.Button(GymUI.Box(panel, "Select " + id, 20 + i * 133, 80, 124, 52), names[i], () => SelectNpc(id)); }
            talk = GymUI.Button(GymUI.Box(panel, "Talk", 20, 150, 245, 56), "Talk", () =>
            {
                if (Coordinator.PendingConversationNpc != null) { Coordinator.CancelPendingConversation(); return; }
                responsiveLayout.ConversationExpanded = true;
                if (!Coordinator.RequestConversation(SelectedNpcId)) ShowConversationStatus(Coordinator.IsReady ? "Conversation unavailable right now." : "Connect before talking.");
            });
            GymUI.Button(GymUI.Box(panel, "History", 283, 150, 250, 56), "History", () => { responsiveLayout.ConversationExpanded = true; if (!Coordinator.RequestHistory(SelectedNpcId)) ShowConversationStatus("Connect to load history."); });
            statusLabel = GymUI.Label(panel, "Connect to begin", 20, 223, 513, 62, 21);
            var scrollArea = GymUI.Rect(panel, "History scroll", Vector2.zero, Vector2.one, new Vector2(20, 240), new Vector2(-20, -306));
            GymUI.Panel(scrollArea, new Color(.025f, .03f, .05f));
            scrollArea.gameObject.AddComponent<RectMask2D>();
            historyScroll = scrollArea.gameObject.AddComponent<ScrollRect>(); historyScroll.horizontal = false;
            var content = GymUI.Rect(scrollArea, "Transcript content", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            transcript = GymUI.Text(content, "Choose a character and tap Talk.", 24, Color.white);
            transcript.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScroll.content = content; historyScroll.viewport = scrollArea;
            var inputRoot = GymUI.Rect(panel, "Reply area", Vector2.zero, new Vector2(1, 0), new Vector2(20, 162), new Vector2(-20, 220));
            GymUI.Panel(inputRoot, new Color(.1f, .11f, .14f));
            var inputText = GymUI.Text(GymUI.Rect(inputRoot, "Reply text", Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8)), "", 23, Color.white, TextAnchor.MiddleLeft);
            reply = inputRoot.gameObject.AddComponent<InputField>(); reply.textComponent = inputText; reply.characterLimit = 4000;
            var placeholder = GymUI.Text(GymUI.Rect(inputRoot, "Reply placeholder", Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8)), "Type a reply…", 23, GymUI.Muted, TextAnchor.MiddleLeft);
            reply.placeholder = placeholder;
            send = GymUI.Button(GymUI.Rect(panel, "Send", Vector2.zero, new Vector2(.5f, 0), new Vector2(20, 91), new Vector2(-8, 146)), "Send", SendReply);
            mic = GymUI.Button(GymUI.Rect(panel, "Microphone", new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(8, 91), new Vector2(-20, 146)), "Mic off", ToggleMicrophone);
            GymUI.Button(GymUI.Rect(panel, "Leave", Vector2.zero, new Vector2(1, 0), new Vector2(20, 20), new Vector2(-20, 75)), "Leave", Leave);
            SetConversationInputEnabled(false, false);

            connectionPanel = GymUI.Box(canvas, "Connection setup", 24, 336, 550, 460); GymUI.Panel(connectionPanel, GymUI.Ink);
            GymUI.Label(connectionPanel, "CONNECT TO THE NIGHT", 26, 18, 498, 38, 26, Gold);
            address = GymUI.Input(connectionPanel, "Game server", 76, DefaultGameAddress);
            access = GymUI.Input(connectionPanel, "Access token (if required)", 185, "", true);
            foreach (var field in new[] { address, access })
            {
                var hint = GymUI.Text(GymUI.Rect(field.transform, "Hint", Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6)), field == address ? "Game server address" : "Access token (optional)", 30, GymUI.Muted, TextAnchor.MiddleLeft);
                field.placeholder = hint;
            }
            GymUI.Button(GymUI.Box(connectionPanel, "New game", 26, 305, 232, 56), "Start new night", () => Coordinator.ConnectNew(address.text.Trim(), access.text));
            resume = GymUI.Button(GymUI.Box(connectionPanel, "Resume game", 282, 305, 242, 56), "Resume", () => Coordinator.Resume(address.text.Trim(), access.text));
            GymUI.Button(GymUI.Box(connectionPanel, "Disconnect", 26, 381, 498, 54), "Disconnect", Coordinator.Disconnect);
            var layout = canvas.gameObject.AddComponent<EncounterHudLayout>();
            responsiveLayout = layout;
            layout.Conversation = panel; layout.Clues = clueBox; layout.ClueToggle = (RectTransform)clueToggle.transform; layout.ClueViewport = clueViewport;
            layout.SafeRoot = canvas; layout.Header = top; layout.HeaderTitle = headerTitle.rectTransform;
            layout.StateText = stateLabel.rectTransform; layout.ConnectionButton = (RectTransform)connectionButton.transform;
            layout.Guidance = guide; layout.GuidanceText = guidance.rectTransform; layout.OpeningRoute = (RectTransform)openingRoute.transform;
            layout.Pause = (RectTransform)pause.transform; layout.Reset = (RectTransform)reset.transform; layout.Connection = connectionPanel;
            var markers = GymUI.Rect(canvas, "Interaction markers", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            markers.SetAsFirstSibling();
            InteractionMarkers = markers.gameObject.AddComponent<EncounterInteractionMarkers>();
            InteractionMarkers.Initialize(this, canvas, responsiveLayout);
            var pauseRoot = GymUI.Rect(canvas, "Pause menu controller", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PauseMenu = pauseRoot.gameObject.AddComponent<EncounterPauseMenu>();
            PauseMenu.Initialize(this, canvas);
            canvas.gameObject.AddComponent<EncounterLoadingBrand>().Initialize(Coordinator, canvas);
        }

        void Wire()
        {
            Coordinator.StateChanged += ShowState; Coordinator.StatusChanged += ShowStatus;
            Coordinator.HistoryReceived += ShowHistory; Coordinator.ConversationRequested += OnConversation;
            Coordinator.ConversationInvalidated += OnInvalidated;
            Coordinator.ApproachStatusChanged += ShowApproachStatus;
            Coordinator.WorldChanged += ShowWorld; Coordinator.PauseChanged += ShowPause;
            if (Voice) { Voice.Ready += OnVoiceReady; Voice.StatusChanged += ShowConversationStatus; Voice.TranscriptFragment += AppendTranscriptFragment; }
            wired = true;
        }

        void Update()
        {
            if (resume) resume.interactable = Coordinator && Coordinator.CanResume && !Coordinator.IsConnecting;
            if (reply && reply.isFocused) { responsiveLayout.ConversationExpanded = true; if (Input.GetKeyDown(KeyCode.Return)) SendReply(); }
            if (reset) reset.interactable = Coordinator.IsReady && (Coordinator.State.Phase == "catastrophe" || Coordinator.State.Phase == "unresolved" || Coordinator.State.Phase == "victory");
            if (pause) pause.interactable = Coordinator.IsReady;
            if (openingRoute) openingRoute.interactable = Coordinator.IsReady && !Coordinator.IsPaused && Coordinator.State.LoopIndex == 1 &&
                Coordinator.State.Phase == "exploring";
            if (talk)
            {
                Coordinator.ConversationEligibility(SelectedNpcId, out var eligible, out var reason);
                talk.interactable = Coordinator.IsReady && !Coordinator.IsPaused;
                talk.GetComponentInChildren<Text>().text = Coordinator.PendingConversationNpc != null ? "Cancel" : eligible ? "Talk" : "Walk & talk";
            }
            if (Coordinator.PendingConversationNpc != null && guidance) guidance.text = "Walking to " + Coordinator.PendingConversationNpc + ". Talk opens when you arrive. Tap the floor or Cancel to change your mind.";
            else if (Voice && (Voice.IsReady || Voice.IsConnecting) && guidance) guidance.text = "The night is paused while you talk. Tap Leave to resume movement and let actions take effect.";
            else if (guidance && Coordinator.State.HasSnapshot) guidance.text = PhaseGuidance(Coordinator.State);
            ReadFloorTap();
            if (Voice && mic && microphoneEnabled != Voice.MicrophoneEnabled && !Voice.IsConnecting)
            { microphoneEnabled = Voice.MicrophoneEnabled; mic.GetComponentInChildren<Text>().text = microphoneEnabled ? "Mic on" : "Mic off"; }
        }

        void ReadFloorTap()
        {
            if (!Coordinator.IsReady || paused || !Rig || Rig.Target || (reply && reply.isFocused)) { floorTap.Cancel(); return; }
            Vector2 position;
            bool tap;
            if (Input.touchCount > 0)
            {
                // Reject the entire multi-touch sequence, including the last remaining finger.
                if (Input.touchCount != 1) { suppressTouches = true; floorTap.Cancel(); return; }
                if (suppressTouches) return;
                var touch = Input.GetTouch(0);
                position = touch.position;
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                if (touch.phase == TouchPhase.Canceled) { floorTap.Cancel(); return; }
                if (touch.phase == TouchPhase.Began) floorTap.Begin(touch.fingerId, position, 30f, overUi);
                floorTap.Move(touch.fingerId, position);
                tap = touch.phase == TouchPhase.Ended && floorTap.End(touch.fingerId, position, overUi);
            }
            else
            {
                suppressTouches = false;
                // iOS mouse emulation must not replay a touch release as a floor click.
                if (Application.isMobilePlatform) { floorTap.Cancel(); return; }
                position = Input.mousePosition;
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                if (Input.GetMouseButtonDown(0)) floorTap.Begin(-1, position, 20f, overUi);
                floorTap.Move(-1, position);
                tap = Input.GetMouseButtonUp(0) && floorTap.End(-1, position, overUi);
            }
            if (!tap || !Rig.Camera || !Physics.Raycast(Rig.Camera.ScreenPointToRay(position), out var hit, 150)) return;
            var actor = hit.collider.GetComponentInParent<CharacterActor>();
            if (actor)
            {
                if (actor.Id == "maya" || actor.Id == "ren" || actor.Id == "luca" || actor.Id == "theo") SelectNpc(actor.Id);
                return;
            }
            if (hit.normal.y > .7f && !Coordinator.SendDestination(hit.point)) ShowConversationStatus("Choose a reachable point on the floor.");
        }

        void SelectNpc(string id)
        {
            if (SelectedNpcId != id) Coordinator.CancelPendingConversation();
            if (SelectedNpcId != id) Leave();
            SelectedNpcId = id; historyText = ""; previousRole = null; RenderHistory();
            UpdatePortrait();
            foreach (var actor in Coordinator.Characters)
                if (actor && actor.Id == id) speakerLabel.text = actor.DisplayName + " / " + actor.Role;
        }

        void OnConversation(EncounterConversationRequest request)
        {
            responsiveLayout.ConversationExpanded = true;
            SelectedNpcId = request.CharacterId; historyText = ""; previousRole = null; RenderHistory();
            UpdatePortrait();
            ShowConversationStatus("Connecting…"); SetConversationInputEnabled(false, false);
            if (Rig) { Rig.Overview(); foreach (var actor in Coordinator.Characters) if (actor && actor.Id == SelectedNpcId) Rig.Target = actor; }
        }
        void OnInvalidated() { if (responsiveLayout) responsiveLayout.ConversationExpanded = false; microphoneEnabled = false; if (mic) mic.GetComponentInChildren<Text>().text = "Mic off"; SetConversationInputEnabled(false, false); if (Rig) Rig.Overview(); }
        void UpdatePortrait()
        {
            // A title child preserves panel indices used by the phone layout.
            // Imported atlas order: Maya, Luca, Theo, Ren, player.
            if (!speakerLabel) return;
            if (!speakerPortrait)
            {
                var rect = GymUI.Box(speakerLabel.transform, "Selected character portrait", -60, 0, 48, 48);
                speakerPortrait = rect.gameObject.AddComponent<RawImage>();
                speakerPortrait.texture = Resources.Load<Texture2D>("MvpArt/Portraits");
                speakerPortrait.raycastTarget = false;
            }
            int index = Array.IndexOf(new[] { "maya", "luca", "theo", "ren" }, SelectedNpcId);
            speakerPortrait.gameObject.SetActive(index >= 0 && speakerPortrait.texture);
            if (index >= 0) speakerPortrait.uvRect = new Rect(index * .2f, .55f, .2f, .3f);
        }
        void OnVoiceReady() { SetConversationInputEnabled(true, true); ShowConversationStatus("Ready · microphone off"); }
        public void SetConversationInputEnabled(bool typed, bool microphone)
        { if (reply) reply.interactable = typed; if (send) send.interactable = typed; if (mic) mic.interactable = microphone; }
        public void ShowConversationStatus(string value)
        {
            if (statusLabel) statusLabel.text = IdleConversationStatus(value, Coordinator && Coordinator.IsReady).Replace('_', ' ');
            if (Voice && !Voice.IsReady) SetConversationInputEnabled(false, false);
            if (Voice && !Voice.IsReady && !Voice.IsConnecting && Rig) Rig.Overview();
        }
        public static string IdleConversationStatus(string value, bool gameReady)
            => gameReady && (value == "disconnected" || value == "ready" || value == "closed") ? "Choose someone nearby" : value;
        void SendReply()
        {
            if (!send || !send.interactable || string.IsNullOrWhiteSpace(reply.text)) return;
            string text = reply.text; TypedReplyRequested?.Invoke(text);
            if (Voice && Voice.SendText(text)) reply.text = "";
        }
        void ToggleMicrophone()
        {
            microphoneEnabled = !microphoneEnabled; MicrophoneRequested?.Invoke(microphoneEnabled);
            if (Voice) Voice.EnableMicrophone(microphoneEnabled);
            mic.GetComponentInChildren<Text>().text = microphoneEnabled ? "Mic on" : "Mic off";
        }
        void Leave() { Coordinator.CancelPendingConversation(); if (reply) reply.DeactivateInputField(); if (Voice) Voice.Leave(); LeaveRequested?.Invoke(); OnInvalidated(); ShowConversationStatus("Choose a character and tap Talk."); }
        public void ShowConnectionSetup()
        {
            Leave();
            if (connectionPanel) connectionPanel.gameObject.SetActive(true);
            if (responsiveLayout) responsiveLayout.ConversationExpanded = false;
            CancelPointerGesture();
        }

        void ShowApproachStatus(string value)
        {
            if (value == "approaching")
            { if (responsiveLayout) responsiveLayout.ConversationExpanded = false; if (Rig) Rig.Overview(); }
            if (value == "conversation_starting") return;
            string text = value == "approaching" ? "Walking into conversation range..." :
                value == "approach_target_moved" ? "They moved or the path was blocked. Tap Talk to approach again." :
                value == "approach_timed_out" ? "Approach timed out. Tap Talk to try again." :
                value == "encounter_ended" ? "The encounter has ended. Rewind to try again." :
                value == "approach_unavailable" ? "No conversation point is reachable right now." : "Approach cancelled. Tap the floor to walk.";
            ShowConversationStatus(text);
            if (guidance) guidance.text = text;
        }
        void CancelPointerGesture() { floorTap.Cancel(); suppressTouches = Input.touchCount > 0; }
        void OnDisable() { CancelPointerGesture(); if (Coordinator) Coordinator.CancelPendingConversation(); }
        void OnApplicationFocus(bool focused) { if (!focused) { CancelPointerGesture(); if (Coordinator) Coordinator.CancelPendingConversation(); } }

        void ShowStatus(string value)
        {
            if (Coordinator.IsReady && connectionPanel) connectionPanel.gameObject.SetActive(false);
            if (!Voice || !Voice.IsReady) ShowConversationStatus(value.Replace('_', ' '));
        }
        void ShowState(EncounterClientState state)
        {
            if (!stateLabel) return;
            stateLabel.text = state.HasSnapshot ? "LOOP " + state.LoopIndex + "   ·   " + state.Mood : "Connect to begin";
            if (state.HasSnapshot) ShowWorldTime((float)state.ElapsedSeconds);
            if (guidance && state.HasSnapshot) guidance.text = PhaseGuidance(state);
            var clues = new StringBuilder();
            foreach (var clue in state.PlayerDiscoveries)
            {
                string text = clue.Type == JTokenType.String ? (string)clue : clue is JObject ? (string)clue["text"] ?? (string)clue["factId"] ?? "Discovered clue" : "Discovered clue";
                if (clues.Length > 0) clues.Append('\n'); clues.Append(text);
            }
            clueLabel.text = clues.Length == 0 ? "None yet" : clues.ToString();
        }
        static string PhaseGuidance(EncounterClientState state)
        {
            switch (state.Phase)
            {
                case "catastrophe": return "Luca has fallen. Rewind to try another approach; your discovered clues remain.";
                case "victory": return "You resolved the confrontation and reached the end of the set.";
                case "unresolved": return "The set ended without resolving the danger. Rewind and try another approach.";
                case "separating": return "Maya is coming with you. Walk away from Theo so they can separate; leave conversation to let them move.";
                case "resolved": return "They have separated. Let the set finish; talking pauses the clock.";
                case "recording": return "Maya is recording. Select someone and Talk to respond; conversation pauses the night.";
                case "theo_approaching":
                case "phone_dispute":
                case "luca_intervening":
                    return state.Recording ? "The confrontation is escalating. Talk to someone nearby; conversation pauses the night." :
                        "The recording has stopped. Talk to Luca about helping them settle this calmly.";
                default: return state.LoopIndex == 1 ?
                    "Walk with Maya toward Theo in the VIP area on the right. Tap the floor or Walk toward Theo." :
                    "Try the left route: talk to Luca, ask Maya to wait, or request music from Ren. Review your clues.";
            }
        }
        // Parent's authoritative-world adapter supplies elapsed time; this HUD does not run a game clock.
        public void ShowWorldTime(float elapsedSeconds)
        {
            if (!stateLabel || !Coordinator.State.HasSnapshot || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)) return;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            int duration = Mathf.Max(0, (int)Coordinator.State.DurationSeconds);
            string end = duration > 0 ? " / " + duration / 60 + ":" + (duration % 60).ToString("00") : "";
            stateLabel.text = "LOOP " + Coordinator.State.LoopIndex + "   ·   " + Coordinator.State.Mood + "   ·   SET " + seconds / 60 + ":" + (seconds % 60).ToString("00") + end;
        }
        void ShowPause(bool value) { paused = value; if (pause) pause.GetComponentInChildren<Text>().text = paused ? "Resume" : "Pause"; }
        void ShowWorld(JObject world)
        { if (world["elapsedSeconds"]?.Type == JTokenType.Integer || world["elapsedSeconds"]?.Type == JTokenType.Float) ShowWorldTime(world["elapsedSeconds"].Value<float>()); }
        void ShowHistory(JObject history)
        {
            if (!(history["fragments"] is JArray fragments)) return;
            historyText = ""; previousRole = null;
            foreach (var fragment in fragments)
            {
                if ((string)fragment["npcId"] != SelectedNpcId) continue;
                AddFragment((string)fragment["role"], (string)fragment["text"] ?? "");
            }
            RenderHistory();
        }
        public void AppendTranscriptFragment(JObject fragment)
        {
            string type = (string)fragment["type"];
            string role = (string)fragment["role"] ?? (type == "session.input_transcript.delta" ? "user" : "assistant");
            AddFragment(role, (string)fragment["text"] ?? (string)fragment["delta"] ?? ""); RenderHistory();
        }
        void AddFragment(string role, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Adjacent same-speaker grouping is only presentation; it does not assert turn completion.
            if (role != previousRole) historyText += (historyText.Length > 0 ? "\n\n" : "") + (role == "user" ? "YOU" : SelectedNpcId.ToUpperInvariant()) + "\n";
            historyText += text; previousRole = role;
            if (historyText.Length > MaxDisplayedCharacters) historyText = historyText.Substring(historyText.Length - MaxDisplayedCharacters);
        }
        void RenderHistory() { if (transcript) transcript.text = historyText; if (historyScroll) historyScroll.verticalNormalizedPosition = 0; }
        void OnDestroy()
        {
            if (wired && Coordinator)
            {
                Coordinator.StateChanged -= ShowState; Coordinator.StatusChanged -= ShowStatus;
                Coordinator.HistoryReceived -= ShowHistory; Coordinator.ConversationRequested -= OnConversation;
                Coordinator.ConversationInvalidated -= OnInvalidated;
                Coordinator.ApproachStatusChanged -= ShowApproachStatus;
                Coordinator.WorldChanged -= ShowWorld; Coordinator.PauseChanged -= ShowPause;
            }
            if (wired && Voice) { Voice.Ready -= OnVoiceReady; Voice.StatusChanged -= ShowConversationStatus; Voice.TranscriptFragment -= AppendTranscriptFragment; }
            if (canvas) Destroy(canvas.GetComponentInParent<Canvas>().gameObject);
        }
    }
}
