using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.CharacterArt
{
    /// <summary>Same frozen V2 geometry and poses on both sides; only the shading changes.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class RenNprReviewController : MonoBehaviour
    {
        public RenBustComparisonViewer Viewer;
        public RenCompleteHeadController FaceControls;
        public UniversalRenderPipelineAsset ReviewPipeline;
        public string SourceSha256, ShaderSha256, ShaderIncludeSha256, ContractSha256, ControllerSha256;
        public bool MovingClubLights = true;
        public float LightPhase;
        bool captureMode;
        RenderPipelineAsset previousPipeline, previousQualityPipeline;
        bool pipelineBound;
        readonly Dictionary<Light, Vector3> lightPositions = new Dictionary<Light, Vector3>();
        FrameMaterials leftMaterials, rightMaterials;
        GUIStyle label, small, button;
        sealed class FrameMaterials
        {
            public GameObject Root;
            public RenBustCandidate Candidate;
            public Renderer[] Renderers;
            public Renderer Head;
            public Bounds FaceLocalBounds;
            public int[][] Slots;
            public readonly Dictionary<Material, Material> Clones = new Dictionary<Material, Material>();
        }

        void Awake()
        {
            if (ReviewPipeline)
            {
                previousPipeline = GraphicsSettings.defaultRenderPipeline; previousQualityPipeline = QualitySettings.renderPipeline;
                GraphicsSettings.defaultRenderPipeline = ReviewPipeline; QualitySettings.renderPipeline = ReviewPipeline; pipelineBound = true;
            }
            foreach (var light in Viewer.NightclubLights)
                if (light && light.type == LightType.Point) lightPositions.Add(light, light.transform.localPosition);
        }
        void LateUpdate()
        {
            if (!captureMode && MovingClubLights) LightPhase = Time.unscaledTime * .7f;
            ApplyNow();
        }
        public void ApplyNow()
        {
            FaceControls.ApplyNow();
            foreach (var instance in new[] { Viewer.LeftInstance, Viewer.RightInstance })
            {
                if (!instance) continue;
                var rig = instance.GetComponent<RenCompleteHeadRig>(); if (!rig) continue;
                rig.SetCapVisible(FaceControls.ShowCap); rig.SetHairVisible(FaceControls.ShowHair);
                rig.Apply(FaceControls.MouthSeal, FaceControls.OpenA, FaceControls.EffectiveBlinkLeft,
                    FaceControls.EffectiveBlinkRight, FaceControls.Gaze);
            }
            foreach (var pair in lightPositions)
            {
                if (!pair.Key) continue;
                var cyan = pair.Key.name.IndexOf("cyan", StringComparison.OrdinalIgnoreCase) >= 0;
                var phase = LightPhase + (cyan ? Mathf.PI : 0);
                pair.Key.transform.localPosition = pair.Value + new Vector3(.65f * Mathf.Sin(phase), .15f * Mathf.Cos(phase), .65f * Mathf.Cos(phase));
            }
            if (Application.isPlaying)
            {
                ApplyFaceMaterials(ref leftMaterials, Viewer.LeftInstance, Viewer.Candidates[Viewer.LeftIndex]);
                ApplyFaceMaterials(ref rightMaterials, Viewer.RightInstance, Viewer.Candidates[Viewer.RightIndex]);
            }
        }
        void ApplyFaceMaterials(ref FrameMaterials state, GameObject instance, RenBustCandidate candidate)
        {
            if (!instance) return;
            if (state == null || state.Root != instance)
            {
                Release(state);
                state = new FrameMaterials { Root = instance, Candidate = candidate, Renderers = instance.GetComponentsInChildren<Renderer>(true) };
                state.Slots = new int[state.Renderers.Length][];
                for (var r = 0; r < state.Renderers.Length; r++)
                {
                    var renderer = state.Renderers[r]; if (renderer.name == "Ren_Head") state.Head = renderer;
                    var materials = renderer.sharedMaterials; state.Slots[r] = new int[materials.Length];
                    for (var s = 0; s < materials.Length; s++)
                    {
                        var index = Array.FindIndex(candidate.LitMaterials, material => material == materials[s]);
                        if (index < 0) index = Array.FindIndex(candidate.BaseColorMaterials, material => material == materials[s]);
                        if (index < 0) throw new InvalidOperationException("NPR viewer material slot is not mapped: " + renderer.name);
                        state.Slots[r][s] = index;
                    }
                }
                if (!state.Head) throw new InvalidOperationException("NPR face frame requires the frozen Ren_Head renderer.");
                state.FaceLocalBounds = CalculateFaceLocalBounds(instance.transform, state.Head);
            }
            var targets = Viewer.LightingMode == 0 ? candidate.BaseColorMaterials : candidate.LitMaterials;
            for (var r = 0; r < state.Renderers.Length; r++)
            {
                var current = state.Renderers[r].sharedMaterials; var changed = false;
                for (var s = 0; s < current.Length; s++)
                {
                    var source = targets[state.Slots[r][s]]; var target = source;
                    if (source.shader.name == "LucidLoop/Characters/PaintedAnimeNPR")
                    {
                        if (!state.Clones.TryGetValue(source, out target))
                        { target = new Material(source) { name = source.name + " / live frame", hideFlags = HideFlags.DontSave }; state.Clones.Add(source, target); }
                        SetFaceFrame(target, instance.transform, state.FaceLocalBounds);
                    }
                    if (current[s] != target) { current[s] = target; changed = true; }
                }
                if (changed) state.Renderers[r].sharedMaterials = current;
            }
        }
        public static Bounds CalculateFaceLocalBounds(Transform frame, Renderer head)
        {
            var vertices = MeshOf(head).vertices;
            if (vertices.Length == 0) throw new InvalidOperationException("Frozen head has no face-frame reference geometry.");
            var first = frame.InverseTransformPoint(head.transform.TransformPoint(vertices[0]));
            var bounds = new Bounds(first, Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(frame.InverseTransformPoint(head.transform.TransformPoint(vertex)));
            return bounds;
        }
        public static void SetFaceFrame(Material material, Transform frame, Bounds localHeadBounds)
        {
            material.SetVector("_FaceForwardWS", frame.forward);
            material.SetVector("_FaceRightWS", frame.right);
            material.SetVector("_FaceUpWS", frame.up);
            material.SetVector("_FaceCenterWS", frame.TransformPoint(localHeadBounds.center));
            material.SetFloat("_FaceWidth", Mathf.Max(.001f, frame.TransformVector(Vector3.right * localHeadBounds.extents.x).magnitude));
        }
        static void Release(FrameMaterials state)
        {
            if (state == null) return;
            foreach (var material in state.Clones.Values) if (material) Destroy(material);
        }
        void OnDestroy()
        {
            Release(leftMaterials); Release(rightMaterials);
            if (pipelineBound)
            {
                if (GraphicsSettings.defaultRenderPipeline == ReviewPipeline) GraphicsSettings.defaultRenderPipeline = previousPipeline;
                if (QualitySettings.renderPipeline == ReviewPipeline) QualitySettings.renderPipeline = previousQualityPipeline;
            }
        }

        [Serializable] sealed class CaptureRecord
        {
            public string file, sha256, shader, pose, lighting;
            public int firstFrame, captureFrame;
            public float yaw, lightPhase, seal, openA, blinkL, blinkR, gazeX, gazeY;
            public RenCompleteHeadRig.CapWeight[] capWeights;
        }
        [Serializable] sealed class CaptureEvidence
        {
            public string unityVersion, sourceSha256, shaderSha256, shaderIncludeSha256, contractSha256, controllerSha256;
            public string shadows = "NPR renderers cast and receive; old diffuse baseline retains no casting/receiving. Both directional keys request Soft shadows, bias0.025, normalBias0.025, nearPlane0.05. Matched camera far planes6. Study-owned pipeline; actual supported features and resolution recorded below. Additional-shadow and Forward+ paths are not tested.";
            public string activePipeline;
            public bool mainShadowsSupported, softShadowsSupported, additionalShadowsSupported;
            public int mainShadowResolution;
            public float shadowDistance;
            public string method = "Running Unity player, identical frozen shared meshes and poses, at least 12 player updates before explicit URP GPU capture. Source geometry and textures unchanged. Moving colored lights sampled at deterministic phases; this is not device-performance evidence.";
            public CaptureRecord[] captures;
        }
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-renNprCapture");
            if (index < 0 || index + 1 >= args.Length) yield break;
            captureMode = true; Application.runInBackground = true;
            var output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            Viewer.SelectCandidate(0, true); Viewer.SelectCandidate(1, false);
            FaceControls.ResetFace(); FaceControls.ShowCap = FaceControls.ShowHair = true; ApplyNow();
            var quick = Array.IndexOf(args, "-renNprQuick") >= 0;
            var revision = Array.IndexOf(args, "-renNprRevision") >= 0;
            var rows = quick ? new[] { "rest", "unlit" } : revision ? new[] { "rest", "club-a", "club-b" } : new[] { "rest", "unlit", "club-a", "club-b", "partial", "full-blink", "open-A" };
            var records = new List<CaptureRecord>();
            foreach (var pose in rows)
            {
                var club = pose.StartsWith("club", StringComparison.Ordinal);
                LightPhase = pose == "club-b" ? Mathf.PI : 0;
                Viewer.SetLighting(club ? 2 : pose == "unlit" ? 0 : 1);
                var blink = pose == "partial" ? .5f : pose == "full-blink" ? 1 : 0;
                FaceControls.SetFace(pose == "open-A" ? 0 : 1, pose == "open-A" ? 1 : 0, blink, blink,
                    pose == "partial" || pose == "open-A" ? new Vector2(.5f, .5f) : Vector2.zero);
                var views = quick ? new[] { 0f } : revision ? new[] { 0f, 45f } : pose == "rest" || club ? new[] { 0f, 45f, 90f } : pose == "partial" ? new[] { 0f, 45f } : new[] { 0f };
                foreach (var yaw in views)
                {
                    Viewer.SetView(yaw); var firstFrame = Time.frameCount;
                    for (var frame = 0; frame < 12; frame++) yield return null;
                    ApplyNow(); VerifySharedMeshes();
                    foreach (var left in new[] { true, false })
                    {
                        var instance = left ? Viewer.LeftInstance : Viewer.RightInstance;
                        var rig = instance.GetComponent<RenCompleteHeadRig>(); var weights = rig.CaptureCapWeights();
                        if (weights.Length != 6 || Array.Exists(weights, item => Mathf.Abs(item.weight - 100) > .001f))
                            throw new InvalidOperationException("NPR comparison lost the frozen V2 cap fitting state.");
                        var shader = left ? "old-diffuse" : "npr";
                        var view = yaw == 0 ? "front" : yaw == 45 ? "quarter" : "profile";
                        var file = "RenNprReview--" + shader + "--" + pose + "--" + view + ".png";
                        var path = Path.Combine(output, file); CaptureCamera(left ? Viewer.LeftCamera : Viewer.RightCamera, path);
                        records.Add(new CaptureRecord { file = file, sha256 = Hash(path), shader = shader, pose = pose,
                            lighting = club ? "Moving colored light sample" : pose == "unlit" ? "Source color / unlit" : "Neutral", firstFrame = firstFrame, captureFrame = Time.frameCount,
                            yaw = yaw, lightPhase = LightPhase, seal = FaceControls.MouthSeal, openA = FaceControls.OpenA,
                            blinkL = FaceControls.EffectiveBlinkLeft, blinkR = FaceControls.EffectiveBlinkRight,
                            gazeX = FaceControls.Gaze.x, gazeY = FaceControls.Gaze.y, capWeights = weights });
                    }
                }
            }
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline || !pipeline.supportsMainLightShadows || !pipeline.supportsSoftShadows)
                throw new InvalidOperationException("NPR shadow proof requires the actual study pipeline with soft main shadows enabled.");
            File.WriteAllText(Path.Combine(output, "RenNprReviewLiveEvidence.json"), JsonUtility.ToJson(new CaptureEvidence {
                unityVersion = Application.unityVersion, sourceSha256 = SourceSha256,
                activePipeline = pipeline.name, mainShadowsSupported = pipeline.supportsMainLightShadows, softShadowsSupported = pipeline.supportsSoftShadows,
                additionalShadowsSupported = pipeline.supportsAdditionalLightShadows, mainShadowResolution = pipeline.mainLightShadowmapResolution, shadowDistance = pipeline.shadowDistance,
                shaderSha256 = ShaderSha256, shaderIncludeSha256 = ShaderIncludeSha256, contractSha256 = ContractSha256, controllerSha256 = ControllerSha256, captures = records.ToArray() }, true));
            Debug.Log("REN_NPR_REVIEW_CAPTURE_OK: " + output); Application.Quit(0);
        }
        void VerifySharedMeshes()
        {
            var left = Viewer.LeftInstance.GetComponentsInChildren<Renderer>(true);
            var right = Viewer.RightInstance.GetComponentsInChildren<Renderer>(true);
            if (left.Length != right.Length) throw new InvalidOperationException("Shader comparison renderer counts diverged.");
            for (var i = 0; i < left.Length; i++)
                if (left[i].name != right[i].name || MeshOf(left[i]) != MeshOf(right[i]))
                    throw new InvalidOperationException("Shader comparison does not use identical shared meshes.");
        }
        static Mesh MeshOf(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static void CaptureCamera(Camera camera, string path)
        {
            var target = new RenderTexture(1536, 1536, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var previous = RenderTexture.active; var rect = camera.rect; var aspect = camera.aspect;
            var image = new Texture2D(1536, 1536, TextureFormat.RGB24, false, false);
            try
            {
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1; target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1536, 1536), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { camera.rect = rect; camera.aspect = aspect; RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(image); }
        }

        void OnGUI()
        {
            if (!Viewer || !FaceControls) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
                label.normal.textColor = new Color(.91f, .93f, .97f);
                small = new GUIStyle(label) { fontSize = 14 }; button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            }
            var scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f); if (scale <= 0) return;
            var matrix = GUI.matrix; var depth = GUI.depth; GUI.depth = -20;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) * .5f, (Screen.height - 900 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
            try
            {
                Panel(new Rect(30, 61, 1280, 28)); GUI.Label(new Rect(32, 62, 1260, 26), "Current prototype / identical V2 geometry and textures / shading study only / both faces share the controls", label);
                var normalNotice = new Rect(1328, 28, 240, 34); Panel(normalNotice);
                GUI.Label(new Rect(1332, 33, 232, 26), "Source normals: not used", small);
                if (normalNotice.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)) Event.current.Use();
                var rx = Viewer.ShowReference && Viewer.References.Length > 0 ? 1072f : 812f;
                Panel(new Rect(30, 697, 498, 30)); GUI.Label(new Rect(32, 701, 496, 24), "Both faces: independent eyelids", label);
                Panel(new Rect(rx, 697, 1600 - rx, 30)); GUI.Label(new Rect(rx, 701, 490, 24), "Both faces: mouth and gaze", label);
                Panel(new Rect(0, 864, 1600, 36));
                MovingClubLights = GUI.Toggle(new Rect(32, 867, 238, 28), MovingClubLights, "Moving nightclub lights", button);
                GUI.Label(new Rect(290, 869, 1260, 28), "Shader study only. Jaw and hair geometry are unchanged. Final style and phone performance remain under review.", small);
            }
            finally { GUI.matrix = matrix; GUI.depth = depth; }
        }
        static void Panel(Rect rect)
        {
            var color = GUI.color; GUI.color = new Color(.055f, .064f, .082f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = color;
        }
    }
}
