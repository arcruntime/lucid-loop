using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

namespace LucidLoop.CharacterArt
{
    [Serializable] public sealed class RenHReferenceValue { public string name; public float value; }
    [Serializable] public sealed class RenHReferenceChannel { public string name; public float min, max = 1, rest; }
    [Serializable] public sealed class RenHReferenceKeyframe { public float time; public RenHReferenceValue[] values = Array.Empty<RenHReferenceValue>(); }
    [Serializable] public sealed class RenHReferenceTimeline
    {
        public float duration, videoFrameSpan, lastVideoFrameTime;
        public string sourceVideoSha256, provenance;
        public RenHReferenceChannel[] channels = Array.Empty<RenHReferenceChannel>();
        public RenHReferenceKeyframe[] keyframes = Array.Empty<RenHReferenceKeyframe>();
    }
    [Serializable] public sealed class RenHReferenceMorphBinding
    {
        public string control, rendererPath, shape;
        public float multiplier = 100, offset;
    }
    [Serializable] public sealed class RenHReferenceRotationAxis
    {
        public string control;
        public Vector3 localAxis;
        public float degreesPerUnit;
    }
    [Serializable] public sealed class RenHReferenceRotationBinding
    {
        public string transformPath;
        public RenHReferenceRotationAxis[] axes = Array.Empty<RenHReferenceRotationAxis>();
    }
    [Serializable] public sealed class RenHReferenceTranslationAxis
    {
        public string control;
        public Vector3 localAxis;
        public float distancePerUnit;
    }
    [Serializable] public sealed class RenHReferenceTranslationBinding
    {
        public string transformPath;
        public RenHReferenceTranslationAxis[] axes = Array.Empty<RenHReferenceTranslationAxis>();
    }

    /// <summary>Manually keyed reference performance. Video presentation time is the clock; this is not a live speech solver.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class RenHReferenceAnimationController : MonoBehaviour
    {
        public TextAsset TimelineAsset;
        public VideoPlayer ReferenceVideo;
        public Texture2D OriginalArtist;
        public Camera ModelCamera;
        public Transform ModelRoot, AnimatedHeadFrame;
        public Renderer HeadRenderer;
        public UniversalRenderPipelineAsset ReviewPipeline;
        public RenHReferenceMorphBinding[] MorphBindings = Array.Empty<RenHReferenceMorphBinding>();
        public RenHReferenceRotationBinding[] RotationBindings = Array.Empty<RenHReferenceRotationBinding>();
        public RenHReferenceTranslationBinding[] TranslationBindings = Array.Empty<RenHReferenceTranslationBinding>();
        public string SourceLabel, SourceSha256, MouthConvention;
        public bool ReferenceAudio;
        public bool Playing;
        public float PlaybackRate = 1, DisplayedTime;
        public Vector3 CameraTarget = new Vector3(0, .825f, 0);
        public float CameraDistance = 4, CameraSize = .98f;
        public float CameraFieldOfView = 28;
        public bool DiagnosticOrthographic;

        sealed class BoundMorph { public RenHReferenceMorphBinding Config; public SkinnedMeshRenderer Renderer; public int Index; }
        sealed class BoundRotation { public RenHReferenceRotationBinding Config; public Transform Node; public Quaternion Rest; }
        sealed class BoundTranslation { public RenHReferenceTranslationBinding Config; public Transform Node; public Vector3 Rest; }
        sealed class FrameMaterial { public Material Instance, Source; public Renderer Renderer; public int Slot; }
        RenHReferenceTimeline timeline;
        readonly List<BoundMorph> morphs = new List<BoundMorph>();
        readonly List<BoundRotation> rotations = new List<BoundRotation>();
        readonly List<BoundTranslation> translations = new List<BoundTranslation>();
        readonly List<FrameMaterial> frameMaterials = new List<FrameMaterial>();
        readonly Dictionary<string, float> current = new Dictionary<string, float>(StringComparer.Ordinal);
        readonly List<string> unsupported = new List<string>();
        readonly List<string> bindingErrors = new List<string>();
        Dictionary<string, float>[] samples;
        RenderPipelineAsset priorGraphics, priorQuality;
        Bounds localFaceBounds;
        float yaw;
        bool initialized, pipelineBound, showUnsupported = true, dragging;
        RenHReferenceVideoTransport transport;
        GUIStyle heading, body, small, button;
        Vector2 unsupportedScroll;

        public float Duration => timeline == null ? 0 : timeline.duration;
        public IReadOnlyList<string> UnsupportedControls => unsupported;
        public IReadOnlyList<string> BindingErrors => bindingErrors;

        void Awake()
        {
            if (!TimelineAsset || !ReferenceVideo || !ModelRoot || !ModelCamera || !AnimatedHeadFrame || !HeadRenderer)
                throw new InvalidOperationException("The H reference study requires the actual source asset, timeline, video, head frame and renderer.");
            timeline = JsonUtility.FromJson<RenHReferenceTimeline>(TimelineAsset.text);
            ValidateTimeline(timeline);
            samples = timeline.keyframes.Select(frame => frame.values.ToDictionary(item => item.name, item => item.value, StringComparer.Ordinal)).ToArray();
            BindControls();
            PrepareFaceMaterials();
            BindPipeline();
            transport = new RenHReferenceVideoTransport(ReferenceVideo, timeline.duration, timeline.videoFrameSpan, timeline.lastVideoFrameTime);
            transport.FramePresented += VideoFramePresented; transport.SetAudio(ReferenceAudio); transport.SetPlaying(Playing);
            initialized = true; ApplyAt(0); UpdateCamera();
        }
        public static void ValidateTimeline(RenHReferenceTimeline data)
        {
            if (data == null || data.duration <= 0 || data.videoFrameSpan <= 0 || data.videoFrameSpan > data.duration ||
                data.lastVideoFrameTime <= 0 || data.lastVideoFrameTime >= data.videoFrameSpan ||
                data.channels.Length == 0 || data.keyframes.Length < 2 || string.IsNullOrWhiteSpace(data.provenance))
                throw new InvalidOperationException("A reviewed, timestamped reference timeline with separate file duration and video frame span is required.");
            if (data.channels.Any(item => string.IsNullOrWhiteSpace(item.name)) || data.channels.Select(item => item.name).Distinct().Count() != data.channels.Length)
                throw new InvalidOperationException("Timeline control names must be unique.");
            foreach (var channel in data.channels)
                if (!Finite(channel.min) || !Finite(channel.max) || !Finite(channel.rest) || channel.min > channel.max || channel.rest < channel.min || channel.rest > channel.max)
                    throw new InvalidOperationException("Invalid timeline channel range: " + channel.name);
            for (var f = 0; f < data.keyframes.Length; f++)
            {
                var frame = data.keyframes[f];
                if (!Finite(frame.time) || frame.time < 0 || frame.time > data.duration || (f > 0 && frame.time <= data.keyframes[f - 1].time))
                    throw new InvalidOperationException("Timeline timestamps must increase within the source duration.");
                if (frame.values.Length != data.channels.Length || frame.values.Select(item => item.name).Distinct().Count() != data.channels.Length)
                    throw new InvalidOperationException("Every timeline frame must contain every control exactly once.");
                foreach (var item in frame.values)
                {
                    var channel = data.channels.SingleOrDefault(value => value.name == item.name);
                    if (channel == null || float.IsNaN(item.value) || float.IsInfinity(item.value) || item.value < channel.min || item.value > channel.max)
                        throw new InvalidOperationException("Invalid timeline value: " + item.name);
                }
            }
        }
        void BindControls()
        {
            ValidateBindingDefinitions(timeline, MorphBindings, RotationBindings, TranslationBindings);
            var supported = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in MorphBindings)
            {
                var node = ModelRoot.Find(binding.rendererPath);
                var renderer = node ? node.GetComponent<SkinnedMeshRenderer>() : null;
                var index = renderer && renderer.sharedMesh ? renderer.sharedMesh.GetBlendShapeIndex(binding.shape) : -1;
                if (!renderer || index < 0)
                { bindingErrors.Add(binding.control + ": missing " + binding.rendererPath + "/" + binding.shape); continue; }
                if (!targets.Add(renderer.GetInstanceID() + ":" + index))
                    throw new InvalidOperationException("Duplicate target morph binding: " + binding.rendererPath + "/" + binding.shape);
                if ((binding.control == "mouthSeal" || binding.control == "jawOpen_A") && string.IsNullOrWhiteSpace(MouthConvention))
                { bindingErrors.Add(binding.control + ": source mouth convention has not been reviewed"); continue; }
                morphs.Add(new BoundMorph { Config = binding, Renderer = renderer, Index = index }); supported.Add(binding.control);
            }
            foreach (var binding in RotationBindings)
            {
                var node = string.IsNullOrEmpty(binding.transformPath) ? ModelRoot : ModelRoot.Find(binding.transformPath);
                if (!node || binding.axes.Any(axis => axis.localAxis.sqrMagnitude < .99f))
                { bindingErrors.Add("Invalid rotation binding: " + binding.transformPath); continue; }
                rotations.Add(new BoundRotation { Config = binding, Node = node, Rest = node.localRotation });
                foreach (var axis in binding.axes) supported.Add(axis.control);
            }
            foreach (var binding in TranslationBindings)
            {
                var node = string.IsNullOrEmpty(binding.transformPath) ? ModelRoot : ModelRoot.Find(binding.transformPath);
                if (!node) { bindingErrors.Add("Invalid translation binding: " + binding.transformPath); continue; }
                translations.Add(new BoundTranslation { Config = binding, Node = node, Rest = node.localPosition });
                foreach (var axis in binding.axes) supported.Add(axis.control);
            }
            unsupported.AddRange(timeline.channels.Select(item => item.name).Where(name => !supported.Contains(name)));
        }
        public static void ValidateBindingDefinitions(RenHReferenceTimeline data, RenHReferenceMorphBinding[] morphBindings, RenHReferenceRotationBinding[] rotationBindings,
            RenHReferenceTranslationBinding[] translationBindings = null)
        {
            var controls = new HashSet<string>(data.channels.Select(item => item.name), StringComparer.Ordinal);
            var morphTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in morphBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.control) || !controls.Contains(binding.control))
                    throw new InvalidOperationException("Unknown morph control: " + binding?.control);
                if (string.IsNullOrWhiteSpace(binding.rendererPath) || string.IsNullOrWhiteSpace(binding.shape) || !Finite(binding.multiplier) || !Finite(binding.offset))
                    throw new InvalidOperationException("Invalid/nonfinite morph binding: " + binding.control);
                if (!morphTargets.Add(binding.rendererPath + "\n" + binding.shape))
                    throw new InvalidOperationException("Duplicate target morph binding: " + binding.rendererPath + "/" + binding.shape);
            }
            var rotationTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in rotationBindings)
            {
                if (binding == null || binding.axes == null || binding.axes.Length == 0 || !rotationTargets.Add(binding.transformPath ?? ""))
                    throw new InvalidOperationException("Empty/duplicate rotation target: " + binding?.transformPath);
                foreach (var axis in binding.axes)
                {
                    if (axis == null || string.IsNullOrWhiteSpace(axis.control) || !controls.Contains(axis.control))
                        throw new InvalidOperationException("Unknown rotation control: " + axis?.control);
                    if (!Finite(axis.localAxis.x) || !Finite(axis.localAxis.y) || !Finite(axis.localAxis.z) || !Finite(axis.degreesPerUnit) || Mathf.Abs(axis.localAxis.magnitude - 1) > .001f)
                        throw new InvalidOperationException("Invalid/nonfinite rotation basis: " + axis.control);
                }
            }
            var translationTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in translationBindings ?? Array.Empty<RenHReferenceTranslationBinding>())
            {
                if (binding == null || binding.axes == null || binding.axes.Length == 0 || !translationTargets.Add(binding.transformPath ?? ""))
                    throw new InvalidOperationException("Empty/duplicate translation target: " + binding?.transformPath);
                foreach (var axis in binding.axes)
                {
                    if (axis == null || string.IsNullOrWhiteSpace(axis.control) || !controls.Contains(axis.control))
                        throw new InvalidOperationException("Unknown translation control: " + axis?.control);
                    if (!Finite(axis.localAxis.x) || !Finite(axis.localAxis.y) || !Finite(axis.localAxis.z) || !Finite(axis.distancePerUnit) || Mathf.Abs(axis.localAxis.magnitude - 1) > .001f)
                        throw new InvalidOperationException("Invalid/nonfinite translation basis: " + axis.control);
                }
            }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void PrepareFaceMaterials()
        {
            localFaceBounds = RenNprReviewController.CalculateFaceLocalBounds(AnimatedHeadFrame, HeadRenderer);
            foreach (var renderer in ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var source = materials[slot];
                    if (!source || source.shader.name != "LucidLoop/Characters/PaintedAnimeNPR") continue;
                    var clone = new Material(source) { name = source.name + " / H animated frame", hideFlags = HideFlags.DontSave };
                    materials[slot] = clone;
                    frameMaterials.Add(new FrameMaterial { Instance = clone, Source = source, Renderer = renderer, Slot = slot });
                }
                renderer.sharedMaterials = materials;
            }
            UpdateFaceFrame();
        }
        void UpdateFaceFrame()
        {
            foreach (var item in frameMaterials)
                RenNprReviewController.SetFaceFrame(item.Instance, AnimatedHeadFrame, localFaceBounds);
        }
        void VideoFramePresented(double time, long frame) { if (isActiveAndEnabled) ApplyAt((float)time); }
        void Update()
        {
            if (transport == null) return;
            transport.PlaybackRate = PlaybackRate; transport.Tick(); Playing = transport.DesiredPlaying;
        }
        void LateUpdate() { UpdateFaceFrame(); UpdateCamera(); }
        public void Seek(float time) { transport?.Seek(time); }
        public void SetPlaying(bool play) { Playing = play; transport?.SetPlaying(play); }
        public void Replay() { Playing = true; transport?.Replay(); }
        public void SetAudio(bool enabled) { ReferenceAudio = enabled; transport?.SetAudio(enabled); }
        public void ApplyAt(float time)
        {
            if (timeline == null) return;
            DisplayedTime = Mathf.Clamp(time, 0, Duration);
            var upper = Array.FindIndex(timeline.keyframes, frame => frame.time >= DisplayedTime);
            if (upper < 0) upper = timeline.keyframes.Length - 1;
            var lower = Mathf.Max(0, upper - 1);
            var span = timeline.keyframes[upper].time - timeline.keyframes[lower].time;
            var t = span <= 0 ? 0 : Mathf.Clamp01((DisplayedTime - timeline.keyframes[lower].time) / span);
            foreach (var channel in timeline.channels) current[channel.name] = Mathf.Lerp(samples[lower][channel.name], samples[upper][channel.name], t);
            // Bindings are applied directly. There is intentionally no inherited seal/open-A normalization.
            foreach (var binding in morphs)
                binding.Renderer.SetBlendShapeWeight(binding.Index, binding.Config.offset + binding.Config.multiplier * current[binding.Config.control]);
            foreach (var binding in rotations)
            {
                var rotation = binding.Rest;
                foreach (var axis in binding.Config.axes)
                    rotation *= Quaternion.AngleAxis(current[axis.control] * axis.degreesPerUnit, axis.localAxis.normalized);
                binding.Node.localRotation = rotation;
            }
            foreach (var binding in translations)
            {
                var position = binding.Rest;
                foreach (var axis in binding.Config.axes) position += axis.localAxis * (current[axis.control] * axis.distancePerUnit);
                binding.Node.localPosition = position;
            }
            UpdateFaceFrame();
        }
        public void SetView(float angle) { yaw = angle; UpdateCamera(); }
        void UpdateCamera()
        {
            if (!ModelCamera) return;
            ModelCamera.transform.position = CameraTarget + Quaternion.Euler(0, yaw, 0) * (Vector3.forward * CameraDistance);
            ModelCamera.transform.LookAt(CameraTarget);
            ModelCamera.orthographic = DiagnosticOrthographic; ModelCamera.orthographicSize = CameraSize; ModelCamera.fieldOfView = CameraFieldOfView;
            var scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            var offset = new Vector2((Screen.width - 1600 * scale) * .5f, (Screen.height - 900 * scale) * .5f);
            ModelCamera.pixelRect = new Rect(offset.x + 30 * scale, offset.y + (900 - 754) * scale, 700 * scale, 622 * scale);
        }
        void BindPipeline()
        {
            if (ReviewPipeline && !pipelineBound)
            {
                priorGraphics = GraphicsSettings.defaultRenderPipeline; priorQuality = QualitySettings.renderPipeline;
                GraphicsSettings.defaultRenderPipeline = ReviewPipeline; QualitySettings.renderPipeline = ReviewPipeline; pipelineBound = true;
            }
        }
        void ReleaseOwnedState()
        {
            foreach (var item in frameMaterials)
            {
                if (item.Renderer)
                {
                    var slots = item.Renderer.sharedMaterials;
                    if (item.Slot < slots.Length && slots[item.Slot] == item.Instance)
                    { slots[item.Slot] = item.Source; item.Renderer.sharedMaterials = slots; }
                }
                if (item.Instance) Destroy(item.Instance);
            }
            frameMaterials.Clear();
            if (pipelineBound)
            {
                if (GraphicsSettings.defaultRenderPipeline == ReviewPipeline) GraphicsSettings.defaultRenderPipeline = priorGraphics;
                if (QualitySettings.renderPipeline == ReviewPipeline) QualitySettings.renderPipeline = priorQuality;
                pipelineBound = false;
            }
        }
        void OnEnable() { if (initialized) { if (frameMaterials.Count == 0) PrepareFaceMaterials(); BindPipeline(); } }
        void OnDisable() { SetPlaying(false); ReleaseOwnedState(); }
        void OnDestroy() { transport?.Dispose(); ReleaseOwnedState(); }
        void OnGUI()
        {
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
                body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                small = new GUIStyle(body) { fontSize = 14 }; button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            }
            var scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f); if (scale <= 0) return;
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) * .5f, (Screen.height - 900 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
            try
            {
                GUI.Label(new Rect(30, 20, 1540, 32), "Ren / H reference animation study", heading);
                GUI.Label(new Rect(30, 58, 1540, 38), SourceLabel + " / manually keyed study, not motion capture or realtime lip-sync", body);
                GUI.Label(new Rect(30, 100, 700, 30), "Actual H-derived candidate", body);
                GUI.Label(new Rect(750, 100, 350, 30), "Video reference", body);
                GUI.Label(new Rect(1120, 100, 450, 30), "Original artist reference", body);
                if (ReferenceVideo && ReferenceVideo.texture) GUI.DrawTexture(new Rect(750, 132, 350, 622), ReferenceVideo.texture, ScaleMode.ScaleToFit);
                else GUI.Label(new Rect(750, 160, 350, 100), transport?.Error ?? "Preparing reference video…", body);
                if (OriginalArtist) GUI.DrawTexture(new Rect(1120, 132, 450, 280), OriginalArtist, ScaleMode.ScaleToFit);
                showUnsupported = GUI.Toggle(new Rect(1120, 426, 450, 32), showUnsupported, "Unsupported controls (" + unsupported.Count + ")", button);
                if (showUnsupported)
                {
                    unsupportedScroll = GUI.BeginScrollView(new Rect(1120, 464, 450, 250), unsupportedScroll, new Rect(0, 0, 424, 36 + 25 * (unsupported.Count + bindingErrors.Count)));
                    var line = 0;
                    foreach (var item in unsupported.Concat(bindingErrors)) { GUI.Label(new Rect(0, line++ * 25, 420, 28), item, small); }
                    if (line == 0) GUI.Label(new Rect(0, 0, 420, 28), "Every timeline control has a source binding.", small);
                    GUI.EndScrollView();
                }
                if (GUI.Button(new Rect(30, 764, 95, 34), "Front", button)) SetView(0);
                if (GUI.Button(new Rect(135, 764, 95, 34), "Quarter", button)) SetView(45);
                if (GUI.Button(new Rect(240, 764, 95, 34), "Profile", button)) SetView(90);
                GUI.enabled = transport != null && transport.HasPresentedFrame && transport.Error == null;
                if (GUI.Button(new Rect(355, 764, 110, 34), Playing ? "Pause" : "Play", button)) SetPlaying(!Playing);
                if (GUI.Button(new Rect(475, 764, 110, 34), "Replay", button)) Replay();
                var audio = GUI.Toggle(new Rect(610, 764, 220, 34), ReferenceAudio, "Reference audio", button);
                if (audio != ReferenceAudio) SetAudio(audio);
                DiagnosticOrthographic = GUI.Toggle(new Rect(870, 764, 300, 34), DiagnosticOrthographic, "Orthographic diagnostic", button);
                var time = transport != null && transport.RequestedTime >= 0 ? (float)transport.RequestedTime : DisplayedTime;
                var scrub = GUI.HorizontalSlider(new Rect(30, 822, 1270, 28), time, 0, Duration);
                if (Mathf.Abs(scrub - time) > .0001f) Seek(scrub);
                GUI.Label(new Rect(1320, 812, 250, 35), time.ToString("F3") + " / " + Duration.ToString("F3") + " s", body);
                GUI.enabled = true;
                GUI.Label(new Rect(30, 858, 1540, 30), "Video presentation time drives the pose. Unsupported controls are omitted explicitly. Final art and phone performance remain unverified.", small);
                var area = new Rect(30, 132, 700, 622); var evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && area.Contains(evt.mousePosition)) { dragging = true; evt.Use(); }
                if (evt.type == EventType.MouseUp) dragging = false;
                if (evt.type == EventType.MouseDrag && dragging) { yaw += evt.delta.x * .35f; evt.Use(); }
            }
            finally { GUI.enabled = true; GUI.matrix = matrix; }
        }
    }
}
