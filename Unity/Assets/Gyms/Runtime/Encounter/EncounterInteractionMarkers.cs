using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    public enum EncounterMarkerState { Hidden, OutOfRange, Available, Focused, PlayerBeacon }

    // Presentation only. This layer never selects actors, starts voice, or changes
    // eligibility. Its lifetime is the existing HUD canvas lifetime.
    [DefaultExecutionOrder(150)]
    public sealed class EncounterInteractionMarkers : MonoBehaviour
    {
        sealed class Marker
        {
            public Transform Actor;
            public RectTransform Rect;
            public EncounterInteractionMarkerGraphic Graphic;
            public EncounterMarkerState State;
        }
        readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
        EncounterHud hud;
        EncounterHudLayout layout;
        RectTransform layer;
        Canvas ownerCanvas;
        public int MarkerCount => markers.Count;
        public int VisibleNpcCount { get; private set; }
        public bool PlayerBeaconVisible { get; private set; }
        public bool TryGetMarkerState(string id, out EncounterMarkerState state)
        {
            if (id != null && markers.TryGetValue(id, out var marker)) { state = marker.State; return true; }
            state = EncounterMarkerState.Hidden; return false;
        }
        public bool TryGetMarkerRectTransform(string id, out RectTransform rect)
        {
            if (id != null && markers.TryGetValue(id, out var marker)) { rect = marker.Rect; return true; }
            rect = null; return false;
        }

        public void Initialize(EncounterHud owner, RectTransform safeRoot, EncounterHudLayout responsiveLayout)
        {
            if (markers.Count != 0) return;
            hud = owner; layout = responsiveLayout; layer = (RectTransform)transform;
            ownerCanvas = safeRoot.GetComponentInParent<Canvas>();
            foreach (var id in new[] { "player", "maya", "ren", "luca", "theo" })
            {
                Transform actorTransform = null;
                if (hud.Coordinator)
                    foreach (var actor in hud.Coordinator.Characters)
                        if (actor && actor.Id == id) { actorTransform = actor.transform; break; }
                if (!actorTransform && id == "player" && hud.Rig) actorTransform = hud.Rig.Player;
                var go = new GameObject(id + " interaction marker", typeof(RectTransform), typeof(CanvasRenderer));
                var rect = (RectTransform)go.transform; rect.SetParent(layer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = id == "player" ? new Vector2(42, 42) : new Vector2(54, 48);
                var graphic = go.AddComponent<EncounterInteractionMarkerGraphic>(); graphic.raycastTarget = false;
                markers.Add(id, new Marker { Actor = actorTransform, Rect = rect, Graphic = graphic, State = EncounterMarkerState.Hidden });
                go.SetActive(false);
            }
        }

        void LateUpdate()
        {
            VisibleNpcCount = 0; PlayerBeaconVisible = false;
            var camera = hud && hud.Rig ? hud.Rig.Camera : null;
            bool show = hud && hud.isActiveAndEnabled && hud.Coordinator && hud.Coordinator.IsReady && camera && camera.isActiveAndEnabled &&
                !hud.Rig.Target && !(layout && layout.ConversationExpanded) && !(hud.Voice && (hud.Voice.IsReady || hud.Voice.IsConnecting));
            foreach (var pair in markers)
            {
                var marker = pair.Value;
                if (!show || !marker.Actor || !marker.Actor.gameObject.activeInHierarchy) { Hide(marker); continue; }
                bool player = pair.Key == "player";
                float wave = Mathf.Sin(Time.unscaledTime * 2.6f);
                var world = marker.Actor.position + Vector3.up * (player ? 2.85f + wave * .09f : 2.95f);
                var screen = camera.WorldToScreenPoint(world);
                var uiCamera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownerCanvas.worldCamera;
                if (screen.z <= 0 || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y)) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, uiCamera, out var local)) { Hide(marker); continue; }
                // The existing uppercase actor label sits below this projection.
                // Lift the whole bubble, including its tail, clear of that label
                // in canvas units so CanvasScaler preserves the visual gap.
                if (!player) local.y += 32f;
                // Hide cropped symbols at safe-area edges; never pin an offscreen
                // actor marker onto another actor or a touch control.
                var bounds = layer.rect; var margin = marker.Rect.sizeDelta * .55f;
                if (local.x < bounds.xMin + margin.x || local.x > bounds.xMax - margin.x || local.y < bounds.yMin + margin.y || local.y > bounds.yMax - margin.y) { Hide(marker); continue; }
                bool eligible = false;
                if (!player) hud.Coordinator.ConversationEligibility(pair.Key, out eligible, out _);
                marker.State = player ? EncounterMarkerState.PlayerBeacon : !eligible ? EncounterMarkerState.OutOfRange :
                    hud.SelectedNpcId == pair.Key ? EncounterMarkerState.Focused : EncounterMarkerState.Available;
                marker.Rect.anchoredPosition = local;
                marker.Rect.localScale = Vector3.one * (player ? 1f + wave * .06f : 1f);
                marker.Graphic.SetPresentation(marker.State, player ? .82f + wave * .14f : 1f);
                marker.Rect.gameObject.SetActive(true);
                if (player) PlayerBeaconVisible = true; else VisibleNpcCount++;
            }
        }
        static void Hide(Marker marker) { marker.State = EncounterMarkerState.Hidden; if (marker.Rect) marker.Rect.gameObject.SetActive(false); }
        void OnDisable() { foreach (var marker in markers.Values) Hide(marker); VisibleNpcCount = 0; PlayerBeaconVisible = false; }
    }

    // All geometry uses the existing uGUI default material: no glyph, texture,
    // custom shader, keyword, per-marker material, or physics hit target.
    sealed class EncounterInteractionMarkerGraphic : MaskableGraphic
    {
        EncounterMarkerState state;
        static readonly Vector2[] Bubble = {
            new Vector2(-.31f,-.19f), new Vector2(.31f,-.19f), new Vector2(.43f,-.07f), new Vector2(.43f,.22f),
            new Vector2(.31f,.34f), new Vector2(-.31f,.34f), new Vector2(-.43f,.22f), new Vector2(-.43f,-.07f)
        };
        public void SetPresentation(EncounterMarkerState value, float alpha)
        {
            if (state != value) { state = value; SetVerticesDirty(); }
            var next = value == EncounterMarkerState.PlayerBeacon ? new Color(.24f, 1f, .53f, alpha) :
                value == EncounterMarkerState.OutOfRange ? new Color(.72f, .76f, .8f, .3f) : new Color(.96f, .83f, .56f, 1f);
            if (color != next) color = next;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (state == EncounterMarkerState.Hidden) return;
            if (state == EncounterMarkerState.PlayerBeacon)
            {
                const int count = 24;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                    Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .32f, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * .32f, .07f, color);
                }
                Triangle(mesh, new Vector2(-.1f, -.04f), new Vector2(.1f, -.04f), new Vector2(0, -.2f), color);
                return;
            }
            bool filled = state == EncounterMarkerState.Focused;
            for (int i = 0; i < Bubble.Length; i++)
            {
                var a = Bubble[i]; var b = Bubble[(i + 1) % Bubble.Length];
                if (filled) Triangle(mesh, new Vector2(0, .07f), a, b, color);
                else Line(mesh, a, b, .045f, color);
            }
            var tailA = new Vector2(-.22f, -.19f); var tailB = new Vector2(-.22f, -.4f); var tailC = new Vector2(.02f, -.19f);
            if (filled) Triangle(mesh, tailA, tailB, tailC, color);
            else { Line(mesh, tailA, tailB, .045f, color); Line(mesh, tailB, tailC, .045f, color); }
            var dots = filled ? new Color(.045f, .065f, .08f, 1) : color;
            for (int i = -1; i <= 1; i++) Line(mesh, new Vector2(i * .19f - .025f, .075f), new Vector2(i * .19f + .025f, .075f), .065f, dots);
        }
        Vector3 Position(Vector2 p) => new Vector3(p.x * rectTransform.rect.width, p.y * rectTransform.rect.height, 0);
        void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(Position(a), tint, Vector2.zero); mesh.AddVert(Position(b), tint, Vector2.zero); mesh.AddVert(Position(c), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
        void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var delta = b - a; var side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            Triangle(mesh, a - side, a + side, b + side, tint); Triangle(mesh, a - side, b + side, b - side, tint);
        }
    }
}
