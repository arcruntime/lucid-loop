using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.CharacterArt
{
    [Serializable] public sealed class RenTokonReviewSlot
    {
        public Renderer renderer;
        public int slot;
        public Material tokon;
        public bool closedEndpoint;
        public Texture2D revisedControl;
        public float revisedFaceMode = -1;
    }
    [DefaultExecutionOrder(200)]
    public sealed class RenTokonReviewController : MonoBehaviour
    {
        public RenHReferenceAnimationController Controller;
        public RenTokonReviewSlot[] Slots = Array.Empty<RenTokonReviewSlot>();
        public string ManifestSha256, ShaderSha256, IncludeSha256, InkSha256, RuntimeSha256, BuilderSha256;
        public bool Tokon = true, ClosedPaint, RefinedControls;
        sealed class Bound { public RenTokonReviewSlot Definition; public Material Original, Baseline, Instance; public Texture OriginalControl; public float OriginalFaceMode; }
        readonly List<Bound> bound = new List<Bound>();
        Bounds faceBounds;
        string output;
        public float CurrentClosedWeight { get; private set; }
        void Awake()
        {
            faceBounds = RenNprReviewController.CalculateFaceLocalBounds(Controller.AnimatedHeadFrame, Controller.HeadRenderer);
            foreach (var item in Slots)
            {
                if (!item.renderer || item.slot < 0 || item.slot >= item.renderer.sharedMaterials.Length || !item.tokon)
                    throw new InvalidOperationException("Invalid Tokon comparison slot.");
                var source = item.renderer.sharedMaterials[item.slot];
                bound.Add(new Bound { Definition = item, Original = source,
                    Baseline = new Material(source) { name = source.name + " / comparison runtime", hideFlags = HideFlags.DontSave },
                    Instance = new Material(item.tokon) { name = item.tokon.name + " / runtime", hideFlags = HideFlags.DontSave },
                    OriginalControl = item.tokon.GetTexture("_ControlMap"), OriginalFaceMode = item.tokon.GetFloat("_FaceMode") });
            }
            SetTokon(Tokon);
        }
        public void SetTokon(bool enabled)
        {
            Tokon = enabled;
            foreach (var item in bound)
            {
                var materials = item.Definition.renderer.sharedMaterials;
                materials[item.Definition.slot] = enabled ? item.Instance : item.Baseline;
                item.Definition.renderer.sharedMaterials = materials;
            }
        }
        void LateUpdate()
        {
            var controls = Controller.CurrentControls;
            float a = controls.TryGetValue("jawOpen_A", out var jaw) ? jaw : 0;
            var t = Mathf.Clamp01(a / .75f);
            var unsupportedPose = new[] { "mouthSmileL", "mouthSmileR", "upperLipRaiseL", "upperLipRaiseR", "mouthPucker", "mouthFunnel", "mouthLeft", "mouthRight", "lipPress" }
                .Any(name => controls.TryGetValue(name, out var weight) && weight > .001f);
            CurrentClosedWeight = ClosedPaint && !unsupportedPose ? 1 - t * t * (3 - 2 * t) : 0;
            foreach (var item in bound)
            {
                RenNprReviewController.SetFaceFrame(item.Baseline, Controller.AnimatedHeadFrame, faceBounds);
                item.Baseline.SetFloat("_Unlit", Controller.LightingMode == 0 ? 1 : 0);
                RenNprReviewController.SetFaceFrame(item.Instance, Controller.AnimatedHeadFrame, faceBounds);
                item.Instance.SetFloat("_Unlit", Controller.LightingMode == 0 ? 1 : 0);
                item.Instance.SetFloat("_ClosedWeight", item.Definition.closedEndpoint ? CurrentClosedWeight : 0);
                item.Instance.SetTexture("_ControlMap", RefinedControls && item.Definition.revisedControl ? item.Definition.revisedControl : item.OriginalControl);
                item.Instance.SetFloat("_FaceMode", RefinedControls && item.Definition.revisedFaceMode >= 0 ? item.Definition.revisedFaceMode : item.OriginalFaceMode);
            }
        }
        void OnDestroy()
        {
            foreach (var item in bound)
            {
                if (item.Definition.renderer)
                {
                    var materials = item.Definition.renderer.sharedMaterials;
                    if (item.Definition.slot < materials.Length && (materials[item.Definition.slot] == item.Instance || materials[item.Definition.slot] == item.Baseline))
                    { materials[item.Definition.slot] = item.Original; item.Definition.renderer.sharedMaterials = materials; }
                }
                if (item.Baseline) Destroy(item.Baseline);
                if (item.Instance) Destroy(item.Instance);
            }
            bound.Clear();
        }
        [Serializable] sealed class Record
        {
            public string file, sha256, mode, poseSha256;
            public float yaw, phase, closedWeight; public int lighting, frame;
        }
        [Serializable] sealed class Evidence
        {
            public string status = "SHADER_DIAGNOSTIC_REJECTED_EYE_GEOMETRY";
            public string unityVersion, graphicsApi, sourceSha256, manifestSha256, shaderSha256, includeSha256, inkSha256, runtimeSha256, builderSha256;
            public bool sameGeometryAcrossModes, perPixelAdditionalLights;
            public bool materialModeAndUnlitAsserted, animatedFaceFrameAsserted, controlMapBindingsAsserted, unsupportedClosedPaintGuardPassed;
            public Record[] captures;
            public string limits = "Actual Windows GPU diagnostic only; rejected eye silhouette is unchanged. No final likeness, phone performance, additional-shadow or outline-shell result. Closed-paint captures are separate from primary same-base shader comparison. Reference audio disabled.";
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-renTokonCapture");
            if (index < 0 || index + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            Application.runInBackground = true;
            var routine = Capture();
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Debug.LogException(error); Application.Quit(2); yield break; }
                yield return next;
            }
        }
        IEnumerator Capture()
        {
            var deadline = Time.realtimeSinceStartup + 40;
            while (!Controller.VideoReady)
            { if (Controller.VideoError != null || Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("Reference preparation failed."); yield return null; }
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline || pipeline.additionalLightsRenderingMode != LightRenderingMode.PerPixel)
                throw new InvalidOperationException("Tokon study requires PerPixel additional lighting; vertex-light path is unsupported.");
            Controller.SetAudio(false); Controller.AnimateClubLights = false; ClosedPaint = false;
            var records = new List<Record>();
            foreach (var label in new[] { "rest-front", "rest-quarter", "rest-profile", "unlit", "partial-blink-gaze", "full-blink", "open-A", "head-turn", "club-a", "club-b" })
            {
                var values = new List<RenHReferenceValue>();
                if (label == "partial-blink-gaze") { Add(values, "eyeBlinkL", .5f); Add(values, "eyeBlinkR", .35f); Add(values, "gazeX", .5f); Add(values, "gazeY", -.5f); Add(values, "jawOpen_A", .35f); }
                if (label == "full-blink") { Add(values, "eyeBlinkL", 1); Add(values, "eyeBlinkR", 1); }
                if (label == "open-A") Add(values, "jawOpen_A", 1);
                if (label == "head-turn") { Add(values, "headYaw", .65f); Add(values, "headPitch", -.25f); }
                Controller.SetManualPose(values.ToArray());
                Controller.SetView(label == "rest-quarter" ? 45 : label == "rest-profile" ? 90 : 0);
                Controller.SetLighting(label.StartsWith("club", StringComparison.Ordinal) ? 2 : label == "unlit" ? 0 : 1);
                Controller.LightPhase = label == "club-b" ? Mathf.PI : 0;
                string state = null;
                foreach (var mode in new[] { 0, 1, 2 })
                {
                    RefinedControls = mode == 2; SetTokon(mode != 0);
                    for (var frame = 0; frame < 12; frame++) yield return null;
                    var pose = RenHReferenceCaptureProbe.PoseSignature(Controller, false);
                    if (state != null && state != pose) throw new InvalidOperationException("Shader switch changed geometry/control state.");
                    state = pose;
                    foreach (var binding in Controller.MorphBindings)
                    {
                        var skin = Controller.ModelRoot.Find(binding.rendererPath).GetComponent<SkinnedMeshRenderer>();
                        if (Mathf.Abs(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(binding.shape)) - RenHReferenceAnimationController.EvaluateMorph(binding, Controller.CurrentControls)) > .001f)
                            throw new InvalidOperationException("Live morph mismatch: " + binding.shape);
                    }
                    records.Add(Save(label, mode == 0 ? "previous-npr" : mode == 1 ? "tokon-original-controls" : "tokon-refined-controls", pose));
                }
            }
            SetTokon(true); RefinedControls = true; ClosedPaint = true; Controller.SetView(0); Controller.SetLighting(1);
            foreach (var jaw in new[] { 0f, .375f, .75f, 1f })
            {
                Controller.SetManualPose(new[] { new RenHReferenceValue { name = "jawOpen_A", value = jaw } });
                for (var frame = 0; frame < 12; frame++) yield return null;
                var t = Mathf.Clamp01(jaw / .75f); var expected = 1 - t * t * (3 - 2 * t);
                if (Mathf.Abs(CurrentClosedWeight - expected) > .0001f) throw new InvalidOperationException("Closed paint blend contract mismatch.");
                records.Add(Save("closed-paint-jaw-" + jaw.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), "tokon-refined-paint-diagnostic", RenHReferenceCaptureProbe.PoseSignature(Controller, false)));
            }
            Controller.SetManualPose(new[] { new RenHReferenceValue { name = "mouthSmileL", value = .5f } });
            for (var frame = 0; frame < 4; frame++) yield return null;
            if (CurrentClosedWeight != 0) throw new InvalidOperationException("Unsupported expression must disable the closed-paint endpoint.");
            File.WriteAllText(Path.Combine(output, "RenTokonLiveEvidence.json"), JsonUtility.ToJson(new Evidence {
                unityVersion = Application.unityVersion, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), sourceSha256 = Controller.SourceSha256,
                manifestSha256 = ManifestSha256, shaderSha256 = ShaderSha256, includeSha256 = IncludeSha256, inkSha256 = InkSha256,
                runtimeSha256 = RuntimeSha256, builderSha256 = BuilderSha256,
                sameGeometryAcrossModes = true, perPixelAdditionalLights = true, materialModeAndUnlitAsserted = true,
                animatedFaceFrameAsserted = true, controlMapBindingsAsserted = true, unsupportedClosedPaintGuardPassed = true,
                captures = records.ToArray() }, true));
            Debug.Log("REN_TOKON_CAPTURE_OK: " + output); Application.Quit(0);
        }
        Record Save(string label, string mode, string pose)
        {
            foreach (var item in bound)
            {
                var expected = Tokon ? item.Instance : item.Baseline;
                if (item.Definition.renderer.sharedMaterials[item.Definition.slot] != expected || expected.GetFloat("_Unlit") != (Controller.LightingMode == 0 ? 1 : 0))
                    throw new InvalidOperationException("Actual selected renderer material has stale mode/unlit state.");
                if (item.Instance.GetTexture("_BaseMap") != item.Baseline.GetTexture("_BaseMap"))
                    throw new InvalidOperationException("Primary comparison base textures differ.");
                if (Vector3.Distance(expected.GetVector("_FaceForwardWS"), Controller.AnimatedHeadFrame.forward) > .0001f)
                    throw new InvalidOperationException("Actual selected material has a stale animated face frame.");
                if (Tokon)
                {
                    var control = RefinedControls && item.Definition.revisedControl ? item.Definition.revisedControl : item.OriginalControl;
                    var strength = RefinedControls && item.Definition.revisedFaceMode >= 0 ? item.Definition.revisedFaceMode : item.OriginalFaceMode;
                    if (expected.GetTexture("_ControlMap") != control || Mathf.Abs(expected.GetFloat("_FaceMode") - strength) > .0001f)
                        throw new InvalidOperationException("Actual selected material has a stale face-control mapping/strength.");
                }
            }
            var file = mode + "--" + label + ".png"; var path = Path.Combine(output, file);
            CaptureCamera(Controller.ModelCamera, path);
            return new Record { file = file, sha256 = Hash(path), mode = mode, poseSha256 = pose,
                yaw = Controller.ModelCamera.transform.eulerAngles.y, phase = Controller.LightPhase, lighting = Controller.LightingMode,
                closedWeight = CurrentClosedWeight, frame = Time.frameCount };
        }
        static void CaptureCamera(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1536, 1536, 24, RenderTextureFormat.ARGB32);
            var prior = camera.targetTexture; var priorRect = camera.rect; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; var image = new Texture2D(1536, 1536, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1536, 1536), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
            }
            finally { camera.targetTexture = prior; camera.rect = priorRect; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); }
        }
        static void Add(List<RenHReferenceValue> values, string name, float value) => values.Add(new RenHReferenceValue { name = name, value = value });
        static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    }
}
