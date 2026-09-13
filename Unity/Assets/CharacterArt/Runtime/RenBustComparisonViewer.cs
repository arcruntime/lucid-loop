using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    [Serializable]
    public sealed class RenBustCandidate
    {
        public string Id, Label, Provider, ModelLabel, Pose, TextureSummary, Notes;
        public int TriangleCount;
        public GameObject Prefab;
        public Material[] BaseColorMaterials = Array.Empty<Material>();
        public Material[] LitMaterials = Array.Empty<Material>();
        public Material[] SourceNormalLitMaterials = Array.Empty<Material>();
    }

    [Serializable]
    public sealed class RenBustReference
    {
        public string Label;
        public Texture2D Texture;
    }

    /// <summary>Internal source-quality review; lighting modes are diagnostics, not the final character shader.</summary>
    public sealed class RenBustComparisonViewer : MonoBehaviour
    {
        public const float PresentationWidth = 1600f, PresentationHeight = 900f;
        public string Title = "Ren / bust comparison";
        public RenBustCandidate[] Candidates = Array.Empty<RenBustCandidate>();
        public RenBustReference[] References = Array.Empty<RenBustReference>();
        public Camera LeftCamera, RightCamera;
        public Transform LeftStage, RightStage;
        public Light[] NeutralLights = Array.Empty<Light>();
        public Light[] NightclubLights = Array.Empty<Light>();
        public GameObject LeftInstance, RightInstance;
        public int LeftIndex, RightIndex = 1;
        public int LeftLayer = 30, RightLayer = 31;
        public float Yaw, Pitch, Zoom = 1f;
        public bool ShowReference;
        public int ReferenceIndex;
        public int LightingMode;
        public bool SourceNormalMaps;

        GUIStyle titleStyle, headingStyle, bodyStyle, smallStyle, buttonStyle;
        Vector2 referenceScroll;
        float lastPinchDistance;
        bool dragging;
        int previousWidth, previousHeight;

        public string LightingLabel => LightingMode == 0 ? "Base color / unlit" : LightingMode == 1 ? "Neutral lighting" : "Nightclub lighting";

        void Awake()
        {
            ApplyLighting();
            ApplyView();
            UpdateLayout();
        }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            var capturePath = ReadArgument(args, "-renBustCapture");
            if (string.IsNullOrWhiteSpace(capturePath)) yield break;
            Application.runInBackground = true;
            Screen.SetResolution(1600, 900, false);
            SelectById(ReadArgument(args, "-renBustLeft"), true);
            SelectById(ReadArgument(args, "-renBustRight"), false);
            var view = ReadArgument(args, "-renBustView");
            SetView(view == "profile" ? 90f : view == "three-quarter" ? 45f : 0f);
            var mode = ReadArgument(args, "-renBustLighting");
            SetLighting(mode == "club" ? 2 : mode == "neutral" ? 1 : 0);
            SetSourceNormalMaps(Array.IndexOf(args, "-renBustSourceNormals") >= 0);
            ShowReference = Array.IndexOf(args, "-renBustShowReference") >= 0;
            if (Array.IndexOf(args, "-renBustReferenceIndex") >= 0)
            {
                if (!int.TryParse(ReadArgument(args, "-renBustReferenceIndex"), out var referenceIndex) ||
                    referenceIndex < 0 || referenceIndex >= References.Length)
                {
                    Debug.LogError("REN_BUST_VIEWER_CAPTURE_FAILED: -renBustReferenceIndex must be a valid zero-based reference index.");
                    Application.Quit(2);
                    yield break;
                }
                ReferenceIndex = referenceIndex;
            }
            UpdateLayout();
            // A player can start this coroutine while Unity's splash screen still owns the
            // backbuffer. Eight rendered frames alone can capture that black transition.
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            for (var i = 0; i < 8; i++) yield return new WaitForEndOfFrame();
            capturePath = Path.GetFullPath(capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(capturePath));
            ScreenCapture.CaptureScreenshot(capturePath);
            for (var i = 0; i < 300; i++)
            {
                yield return new WaitForEndOfFrame();
                if (!File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) continue;
                Debug.Log("REN_BUST_VIEWER_CAPTURE_OK: " + capturePath);
                Application.Quit(0);
                yield break;
            }
            Debug.LogError("REN_BUST_VIEWER_CAPTURE_FAILED: " + capturePath);
            Application.Quit(2);
        }

        void Update()
        {
            if (Screen.width != previousWidth || Screen.height != previousHeight) UpdateLayout();
            // IMGUI also handles mouse and single-touch drags. Two touches provide synchronized zoom.
            if (Input.touchCount == 2)
            {
                var distance = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
                if (lastPinchDistance > 0f) SetZoom(Zoom * lastPinchDistance / Mathf.Max(1f, distance));
                lastPinchDistance = distance;
            }
            else lastPinchDistance = 0f;
        }

        public void SelectById(string id, bool left)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var index = Array.FindIndex(Candidates, candidate => candidate.Id == id);
            if (index < 0) throw new ArgumentException("Unknown Ren bust candidate: " + id);
            SelectCandidate(index, left);
        }

        public void SelectCandidate(int index, bool left)
        {
            if (Candidates.Length == 0) return;
            index = (index % Candidates.Length + Candidates.Length) % Candidates.Length;
            var previous = left ? LeftInstance : RightInstance;
            if (previous)
            {
                previous.SetActive(false);
                if (Application.isPlaying) Destroy(previous); else DestroyImmediate(previous);
            }
            var candidate = Candidates[index];
            if (!candidate.Prefab) throw new InvalidOperationException("Missing prefab for " + candidate.Id);
            var instance = Instantiate(candidate.Prefab, left ? LeftStage : RightStage, false);
            instance.name = candidate.Label;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = left ? LeftLayer : RightLayer;
            if (left) { LeftIndex = index; LeftInstance = instance; }
            else { RightIndex = index; RightInstance = instance; }
            ApplyMaterials(instance, candidate);
            ApplyView();
        }

        public void SetView(float yaw)
        {
            Yaw = yaw; Pitch = 0f; Zoom = 1f;
            ApplyView();
        }

        public void SetZoom(float zoom)
        {
            Zoom = Mathf.Clamp(zoom, .35f, 1.6f);
            ApplyView();
        }

        public void SetLighting(int mode)
        {
            LightingMode = Mathf.Clamp(mode, 0, 2);
            ApplyLighting();
        }

        public void SetSourceNormalMaps(bool enabled)
        {
            SourceNormalMaps = enabled;
            ApplyLighting();
        }

        public void ApplyLighting()
        {
            foreach (var light in NeutralLights) if (light) light.enabled = LightingMode == 1;
            foreach (var light in NightclubLights) if (light) light.enabled = LightingMode == 2;
            RenderSettings.ambientLight = LightingMode == 2 ? new Color(.30f, .28f, .34f) : new Color(.58f, .58f, .58f);
            if (LeftInstance && LeftIndex < Candidates.Length) ApplyMaterials(LeftInstance, Candidates[LeftIndex]);
            if (RightInstance && RightIndex < Candidates.Length) ApplyMaterials(RightInstance, Candidates[RightIndex]);
            foreach (var camera in new[] { LeftCamera, RightCamera })
                if (camera) camera.backgroundColor = LightingMode == 2 ? new Color(.036f, .025f, .063f) : new Color(.105f, .115f, .14f);
        }

        void ApplyMaterials(GameObject instance, RenBustCandidate candidate)
        {
            var hasNormalVariant = candidate.SourceNormalLitMaterials != null &&
                                   candidate.SourceNormalLitMaterials.Length == candidate.LitMaterials.Length;
            var target = LightingMode == 0 ? candidate.BaseColorMaterials :
                SourceNormalMaps && hasNormalVariant ? candidate.SourceNormalLitMaterials : candidate.LitMaterials;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var current = renderer.sharedMaterials;
                for (var slot = 0; slot < current.Length; slot++)
                    for (var i = 0; i < candidate.BaseColorMaterials.Length; i++)
                        if (current[slot] == candidate.BaseColorMaterials[i] || current[slot] == candidate.LitMaterials[i] ||
                            hasNormalVariant && current[slot] == candidate.SourceNormalLitMaterials[i])
                        { current[slot] = target[i]; break; }
                renderer.sharedMaterials = current;
            }
        }

        public void ApplyView()
        {
            PositionCamera(LeftCamera, LeftStage);
            PositionCamera(RightCamera, RightStage);
        }

        void PositionCamera(Camera camera, Transform stage)
        {
            if (!camera || !stage) return;
            var target = stage.position + new Vector3(0f, .825f, 0f);
            var offset = Quaternion.Euler(-Pitch, Yaw, 0f) * new Vector3(0f, 0f, 4f);
            camera.transform.position = target + offset;
            camera.transform.LookAt(target);
            camera.orthographic = true;
            camera.orthographicSize = .98f * Zoom;
        }

        public void UpdateLayout()
        {
            previousWidth = Screen.width; previousHeight = Screen.height;
            var scale = Mathf.Min(Screen.width / PresentationWidth, Screen.height / PresentationHeight);
            if (scale <= 0f) return;
            var origin = new Vector2((Screen.width - PresentationWidth * scale) * .5f, (Screen.height - PresentationHeight * scale) * .5f);
            SetViewport(LeftCamera, PaneRect(true), scale, origin);
            SetViewport(RightCamera, PaneRect(false), scale, origin);
        }

        static void SetViewport(Camera camera, Rect rect, float scale, Vector2 origin)
        {
            if (!camera) return;
            camera.rect = new Rect((origin.x + rect.x * scale) / Screen.width,
                1f - (origin.y + rect.yMax * scale) / Screen.height,
                rect.width * scale / Screen.width, rect.height * scale / Screen.height);
        }

        Rect PaneRect(bool left) => ShowReference && References.Length > 0
            ? new Rect(left ? 32f : 1072f, 218f, 496f, 468f)
            : new Rect(left ? 32f : 812f, 218f, 756f, 468f);

        void OnGUI()
        {
            CreateStyles();
            var scale = Mathf.Min(Screen.width / PresentationWidth, Screen.height / PresentationHeight);
            if (scale <= 0f) return;
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - PresentationWidth * scale) * .5f,
                (Screen.height - PresentationHeight * scale) * .5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                Box(new Rect(0, 0, 1600, 214), new Color(.055f, .064f, .082f));
                Box(new Rect(0, 690, 1600, 210), new Color(.055f, .064f, .082f));
                GUI.Label(new Rect(32, 22, 1000, 40), Title, titleStyle);
                GUI.Label(new Rect(32, 62, 1300, 23), "Generated busts / shared scale and camera / inspect likeness before facial rigging", bodyStyle);
                GUI.enabled = LightingMode != 0;
                if (GUI.Button(new Rect(1328, 28, 240, 34), SourceNormalMaps ? "Source normal maps: ON" : "Source normal maps: OFF", buttonStyle))
                    SetSourceNormalMaps(!SourceNormalMaps);
                GUI.enabled = true;
                var selected = GUI.SelectionGrid(new Rect(32, 99, 598, 34), LightingMode,
                    new[] { "Base color / unlit", "Neutral lighting", "Nightclub lighting" }, 3, buttonStyle);
                if (selected != LightingMode) SetLighting(selected);
                if (GUI.Button(new Rect(670, 99, 108, 34), "Front", buttonStyle)) SetView(0f);
                if (GUI.Button(new Rect(784, 99, 128, 34), "Three-quarter", buttonStyle)) SetView(45f);
                if (GUI.Button(new Rect(918, 99, 108, 34), "Profile", buttonStyle)) SetView(90f);
                if (GUI.Button(new Rect(1032, 99, 108, 34), "Other side", buttonStyle)) SetView(-90f);
                GUI.Label(new Rect(1170, 105, 45, 24), "Zoom", smallStyle);
                var nextZoom = GUI.HorizontalSlider(new Rect(1225, 113, 130, 18), Zoom, 1.6f, .35f);
                if (!Mathf.Approximately(nextZoom, Zoom)) SetZoom(nextZoom);
                GUI.enabled = References.Length > 0;
                if (GUI.Button(new Rect(1390, 99, 178, 34), ShowReference ? "Hide reference" : "Show reference", buttonStyle))
                { ShowReference = !ShowReference; UpdateLayout(); }
                GUI.enabled = true;
                DrawCandidate(true);
                DrawCandidate(false);
                if (ShowReference && References.Length > 0) DrawReference();
                GUI.Label(new Rect(32, 814, 1500, 24), "Drag either model to orbit both. Scroll or pinch to zoom. Front / Three-quarter / Profile reset the framing.", bodyStyle);
                GUI.Label(new Rect(32, 847, 1500, 28), "Source review only. Lit modes use the same simple material response; this viewer does not establish mobile performance or working facial controls.", smallStyle);
                HandlePointer(Event.current);
            }
            finally { GUI.matrix = previousMatrix; }
        }

        void DrawCandidate(bool left)
        {
            var rect = PaneRect(left);
            var index = left ? LeftIndex : RightIndex;
            if (Candidates.Length == 0 || index < 0 || index >= Candidates.Length)
            { GUI.Label(new Rect(rect.x, 155, rect.width, 50), "No generated candidate available", bodyStyle); return; }
            var candidate = Candidates[index];
            if (GUI.Button(new Rect(rect.x, 157, 34, 34), "<", buttonStyle)) SelectCandidate(index - 1, left);
            if (GUI.Button(new Rect(rect.xMax - 34, 157, 34, 34), ">", buttonStyle)) SelectCandidate(index + 1, left);
            GUI.Label(new Rect(rect.x + 46, 153, rect.width - 92, 30), candidate.Label, headingStyle);
            GUI.Label(new Rect(rect.x + 46, 186, rect.width - 92, 23),
                candidate.Provider + " / " + candidate.ModelLabel + " / " + (index + 1) + " of " + Candidates.Length, smallStyle);
            GUI.Label(new Rect(rect.x, 706, rect.width, 23),
                candidate.Pose + "  |  " + candidate.TriangleCount.ToString("N0") + " triangles  |  " + candidate.TextureSummary, bodyStyle);
            GUI.Label(new Rect(rect.x, 738, rect.width, 66), candidate.Notes, smallStyle);
        }

        void DrawReference()
        {
            var width = 496f;
            Box(new Rect(552, 150, width, 648), new Color(.075f, .084f, .105f));
            var reference = References[Mathf.Clamp(ReferenceIndex, 0, References.Length - 1)];
            if (GUI.Button(new Rect(566, 158, 34, 34), "<", buttonStyle))
                ReferenceIndex = (ReferenceIndex + References.Length - 1) % References.Length;
            if (GUI.Button(new Rect(1000, 158, 34, 34), ">", buttonStyle))
                ReferenceIndex = (ReferenceIndex + 1) % References.Length;
            GUI.Label(new Rect(612, 162, 376, 52), reference.Label, bodyStyle);
            if (reference.Texture)
            {
                var displayWidth = width - 28f;
                var height = displayWidth * reference.Texture.height / Mathf.Max(1f, reference.Texture.width);
                var area = new Rect(566, 224, displayWidth, 528);
                referenceScroll = GUI.BeginScrollView(area, referenceScroll, new Rect(0, 0, displayWidth - 18, Mathf.Max(area.height, height)));
                GUI.DrawTexture(new Rect(0, 0, displayWidth - 18, height), reference.Texture, ScaleMode.ScaleToFit);
                GUI.EndScrollView();
            }
            GUI.Label(new Rect(566, 765, width - 28, 25), (ReferenceIndex + 1) + " of " + References.Length + " / source image", smallStyle);
        }

        void HandlePointer(Event current)
        {
            var overModel = PaneRect(true).Contains(current.mousePosition) || PaneRect(false).Contains(current.mousePosition);
            if (current.type == EventType.MouseDown && current.button == 0 && overModel) { dragging = true; current.Use(); }
            if (current.type == EventType.MouseUp) dragging = false;
            if (current.type == EventType.MouseDrag && dragging && Input.touchCount < 2)
            {
                Yaw -= current.delta.x * .3f;
                Pitch = Mathf.Clamp(Pitch + current.delta.y * .25f, -45f, 45f);
                ApplyView(); current.Use();
            }
            if (current.type == EventType.ScrollWheel && overModel)
            { SetZoom(Zoom * Mathf.Exp(current.delta.y * .07f)); current.Use(); }
        }

        void CreateStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 29, fontStyle = FontStyle.Bold };
            headingStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            foreach (var style in new[] { titleStyle, headingStyle, bodyStyle, smallStyle })
                style.normal.textColor = new Color(.91f, .93f, .97f);
            smallStyle.normal.textColor = new Color(.67f, .73f, .81f);
        }

        static void Box(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }

        static string ReadArgument(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
