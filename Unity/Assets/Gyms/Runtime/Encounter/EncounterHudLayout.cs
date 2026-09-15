using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    // Encounter-only layout: SafeAreaPanel first supplies the usable rectangle.
    // Keep touch controls at their authored size instead of shrinking the entire HUD.
    public sealed class EncounterHudLayout : MonoBehaviour
    {
        public RectTransform SafeRoot;
        public RectTransform Header, HeaderTitle, StateText, ConnectionButton;
        public RectTransform Guidance, GuidanceText, OpeningRoute, Pause, Reset, Connection;
        public RectTransform Conversation, Clues, ClueToggle, ClueViewport;
        public bool PreviewPhoneLayout;
        public bool ConversationExpanded;
        float lastKeyboardInset, keyboardHoldUntil;
        public bool UseInformationOverlay;
        public bool CluesExpanded { get; private set; }
        public void ToggleClues() { CluesExpanded = !CluesExpanded; }
        // 44 points at native iPhone 15/15 Plus scale (3 pixels per point).
        public static float TouchTargetUnits(float scale) => 132f / Mathf.Max(.01f, scale);
        public static float KeyboardInset(Rect keyboard, Rect safe, float scale, bool visible)
            => visible ? Mathf.Max(0, (keyboard.height > 0 ? keyboard.yMax : safe.yMin + safe.height * .55f) - safe.yMin) / Mathf.Max(.01f, scale) : 0;
        static RectTransform Child(RectTransform parent, string name) => parent ? parent.Find(name) as RectTransform : null;

        void LateUpdate()
        {
            if (!SafeRoot || !Header || !Guidance || !Connection) return;
            var size = SafeRoot.rect.size;
            if (size.x < 700 || size.y < 500) return;
            if (Application.isMobilePlatform || PreviewPhoneLayout)
            {
                float scale = SafeRoot.GetComponentInParent<Canvas>().scaleFactor;
                float keyboard = KeyboardInset(TouchScreenKeyboard.area, Screen.safeArea, scale, TouchScreenKeyboard.visible);
                // InputField deselects on pointer-down; hold geometry through pointer-up
                // so Send/Leave cannot move away between the two halves of a tap.
                if (keyboard > 0) { lastKeyboardInset = keyboard; keyboardHoldUntil = Time.unscaledTime + .35f; }
                else if (Time.unscaledTime < keyboardHoldUntil) keyboard = lastKeyboardInset;
                ApplyPhoneLayout(size, TouchTargetUnits(scale), keyboard);
                return;
            }
            LayoutClues(430, CluesExpanded ? Mathf.Max(200, size.y - 430) : 80, 156, 54);
            // Conversation occupies [safeWidth-585, safeWidth-24]. Reserve another
            // 24-unit gap before it, plus the left panel's 24-unit outer margin.
            float leftWidth = Mathf.Min(1275, Mathf.Max(0, size.x - 633));
            SetTopLeft(Header, 24, 24, leftWidth, 116);
            SetTopLeft(HeaderTitle, 20, 10, Mathf.Max(0, Mathf.Min(850, leftWidth - 245)), 40);
            SetTopLeft(StateText, 20, 58, Mathf.Max(0, leftWidth - 40), 36);
            SetTopRight(ConnectionButton, 20, 12, 185, 43);

            SetBottomLeft(Guidance, 24, 24, leftWidth, 140);
            SetTopLeft(GuidanceText, 20, 18, Mathf.Max(0, leftWidth - 375), 108);
            SetTopRight(OpeningRoute, 20, 36, 305, 65);
            SetBottomLeft(Pause, 24, 192, 200, 58);
            SetBottomLeft(Reset, 242, 192, 212, 58);

            // Connection is a temporary foreground panel; fit it inside the safe
            // rectangle even on short landscape screens. It closes after ready.
            SetTopLeft(Connection, 24, Mathf.Clamp(336, 24, Mathf.Max(24, size.y - 484)), 550, 460);
        }

        void LayoutClues(float width, float height, float top, float target, float x = 24)
        {
            if(UseInformationOverlay){if(Clues)Clues.gameObject.SetActive(false);return;}
            SetTopLeft(Clues, x, top, width, height);
            SetTopLeft(ClueToggle, 12, 8, width - 24, target);
            if (ClueViewport) { ClueViewport.offsetMax = new Vector2(-18, -target - 20); ClueViewport.gameObject.SetActive(CluesExpanded); }
            if (ClueToggle) ClueToggle.GetComponentInChildren<Text>().text = CluesExpanded ? "Retained clues - close" : "Retained clues - expand";
            if (CluesExpanded) Clues.SetAsLastSibling();
        }

        // Deterministic geometry seam for layout tests; all sizes are safe-root units.
        public void ApplyPhoneLayout(Vector2 size, float target, float keyboard)
        {
            if (!Conversation) return;
            float inset = Mathf.Max(0, keyboard);
            bool typing = inset > 0;
            foreach (var name in new[] { "Reply area", "Send", "Microphone", "Leave" }) Child(Conversation, name).gameObject.SetActive(true);
            float width = typing ? size.x - 48 : Mathf.Min(900, size.x * .49f);
            float left = size.x - width - 72;
            SetBottomLeft(Conversation, size.x - width - 24, 24 + inset, width, size.y - 48 - inset);
            float h = size.y - 48 - inset;
            SetTopLeft(Header, 24, 24, left, 156);
            SetTopLeft(HeaderTitle, 16, 8, left - 250, 48);
            SetTopLeft(StateText, 16, 104, left - 32, 48);
            SetTopRight(ConnectionButton, 12, 8, 225, target);
            SetBottomLeft(Pause, 24, 220, 210, target);
            SetBottomLeft(Reset, 250, 220, 220, target);
            SetBottomLeft(Guidance, 24, 24, left, 180);
            SetTopLeft(GuidanceText, 16, 12, left - 360, 154);
            SetTopRight(OpeningRoute, 16, 32, 320, target);
            LayoutClues(left, CluesExpanded ? size.y - 240 : target + 16, 196, target);
            Header.gameObject.SetActive(!typing); Guidance.gameObject.SetActive(!typing);
            Pause.gameObject.SetActive(!typing); Reset.gameObject.SetActive(!typing); Clues.gameObject.SetActive(!typing && !UseInformationOverlay);
            foreach (var name in new[] { "Select maya", "Select ren", "Select luca", "Select theo", "Talk", "History" })
                Child(Conversation, name).gameObject.SetActive(!typing);
            SetTopLeft(Conversation.GetChild(0) as RectTransform, 80, 10, width - 100, 48);
            string[] ids = { "maya", "ren", "luca", "theo" };
            for (int i = 0; i < 4; i++) SetTopLeft(Child(Conversation, "Select " + ids[i]), 20 + i * (width - 40) / 4, 66, (width - 40) / 4 - 8, target);
            SetTopLeft(Child(Conversation, "Talk"), 20, 82 + target, (width - 56) / 2, target);
            SetTopRight(Child(Conversation, "History"), 20, 82 + target, (width - 56) / 2, target);
            var status = Conversation.GetChild(7) as RectTransform;
            status.gameObject.SetActive(!typing);
            SetTopLeft(status, 20, 94 + 2 * target, width - 40, 60);
            float row = (width - 72) / 3;
            SetBottomLeft(Child(Conversation, "Send"), 20, 16, row, target);
            SetBottomLeft(Child(Conversation, "Microphone"), 36 + row, 16, row, target);
            SetBottomLeft(Child(Conversation, "Leave"), 52 + 2 * row, 16, row, target);
            SetBottomLeft(Child(Conversation, "Reply area"), 20, 32 + target, width - 40, target);
            var history = Child(Conversation, "History scroll");
            float top = typing ? 62 : 158 + 2 * target;
            float bottom = 48 + 2 * target;
            history.anchorMin = Vector2.zero; history.anchorMax = Vector2.one;
            history.offsetMin = new Vector2(20, bottom); history.offsetMax = new Vector2(-20, -top);
            history.gameObject.SetActive(h > top + bottom + 45);
            foreach (var text in Conversation.GetComponentsInChildren<Text>(true)) text.fontSize = Mathf.Max(text.fontSize, 32);
            if (ClueToggle) ClueToggle.GetComponentInChildren<Text>().fontSize = 32;
            foreach (var control in new[] { Pause, Reset, OpeningRoute, ConnectionButton }) control.GetComponentInChildren<Text>().fontSize = 32;
            if (!ConversationExpanded && !typing) ApplyExplorationDock(size, target);
            if (Connection.gameObject.activeSelf)
            {
                Connection.SetAsLastSibling();
                float cw = Mathf.Min(size.x - 48, typing ? size.x - 48 : 1040);
                SetBottomLeft(Connection, 24, inset + 24, cw, 2 * target + 100);
                var fields = Connection.GetComponentsInChildren<InputField>();
                for (int i = 0; i < fields.Length; i++) SetTopLeft((RectTransform)fields[i].transform, 24 + i * (cw - 48) / 2, 64, (cw - 64) / 2, target);
                foreach (Transform child in Connection) if (child.GetComponent<Text>() && child.GetSiblingIndex() != 0) child.gameObject.SetActive(false);
                SetTopLeft(Connection.GetChild(0) as RectTransform, 24, 12, cw - 48, 44);
                float bw = (cw - 80) / 3;
                SetTopLeft(Child(Connection, "New game"), 24, 80 + target, bw, target);
                SetTopLeft(Child(Connection, "Resume game"), 40 + bw, 80 + target, bw, target);
                SetTopLeft(Child(Connection, "Disconnect"), 56 + 2 * bw, 80 + target, bw, target);
                foreach (var text in Connection.GetComponentsInChildren<Text>(true)) text.fontSize = Mathf.Max(text.fontSize, 30);
            }
        }

        void ApplyExplorationDock(Vector2 size, float target)
        {
            // One top toolbar and a shallow bottom dock leave the club centre tappable.
            float dockWidth = Mathf.Min(1050, size.x * .56f);
            float left = size.x - dockWidth - 72;
            SetBottomLeft(Conversation, size.x - dockWidth - 24, 24, dockWidth, target + 88);
            SetTopLeft(Conversation.GetChild(0) as RectTransform, 80, 10, dockWidth - 100, 48);
            string[] controls = { "Select maya", "Select ren", "Select luca", "Select theo", "Talk", "History" };
            float cell = (dockWidth - 80) / 6;
            for (int i = 0; i < controls.Length; i++) SetTopLeft(Child(Conversation, controls[i]), 20 + i * (cell + 8), 64, cell, target);
            foreach (var name in new[] { "Reply area", "Send", "Microphone", "Leave", "History scroll" }) Child(Conversation, name).gameObject.SetActive(false);
            Conversation.GetChild(7).gameObject.SetActive(false);
            SetTopLeft(Header, 24, 24, size.x - 48, target + 16);
            SetTopLeft(HeaderTitle, 16, 8, size.x - 1000, 40);
            SetTopLeft(StateText, 16, 62, size.x - 1000, 40);
            SetTopRight(ConnectionButton, 16, 8, 200, target);
            SetTopLeft(Pause, size.x - 632, 32, 180, target);
            SetTopLeft(Reset, size.x - 436, 32, 180, target);
            LayoutClues(CluesExpanded ? Mathf.Min(900, size.x - 48) : 280,
                CluesExpanded ? size.y - 208 : target + 16,
                CluesExpanded ? target + 56 : 24, target,
                CluesExpanded ? 24 : size.x - 928);
            if (ClueToggle) ClueToggle.GetComponentInChildren<Text>().text = CluesExpanded ? "Retained clues - close" : "Clues";
            SetBottomLeft(Guidance, 24, 24, left, target + 112);
            SetTopLeft(GuidanceText, 16, 8, left - 32, 84);
            SetTopLeft(OpeningRoute, 16, 96, left - 32, target);
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            if (!rect) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.offsetMin = new Vector2(x, -y - height);
            rect.offsetMax = new Vector2(x + width, -y);
        }
        static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
        {
            if (!rect) return;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-right - width, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
        static void SetBottomLeft(RectTransform rect, float x, float bottom, float width, float height)
        {
            if (!rect) return;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = new Vector2(x, bottom);
            rect.offsetMax = new Vector2(x + width, bottom + height);
        }
    }
}
