using UnityEngine;

namespace LucidLoop.Gyms
{
    // Encounter-only layout: SafeAreaPanel first supplies the usable rectangle.
    // Keep touch controls at their authored size instead of shrinking the entire HUD.
    public sealed class EncounterHudLayout : MonoBehaviour
    {
        public RectTransform SafeRoot;
        public RectTransform Header, HeaderTitle, StateText, ConnectionButton;
        public RectTransform Guidance, GuidanceText, OpeningRoute, Pause, Reset, Connection;
        Vector2 lastSize = new Vector2(-1, -1);

        void LateUpdate()
        {
            if (!SafeRoot || !Header || !Guidance || !Connection) return;
            var size = SafeRoot.rect.size;
            if (size == lastSize || size.x < 700 || size.y < 500) return;
            lastSize = size;
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
