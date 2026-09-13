using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenNprReviewManifest
    {
        public string sourceFbxSha256, shaderAsset, shaderSha256, shaderIncludeAsset, shaderIncludeSha256, presetAsset, presetSha256, reviewNote;
    }
    [Serializable] public sealed class RenNprFloat { public string name; public float value; }
    [Serializable] public sealed class RenNprColor { public string name; public float[] linearRgba; }
    [Serializable] public sealed class RenNprPreset
    {
        public string sourceName;
        public RenNprFloat[] floats = Array.Empty<RenNprFloat>();
        public RenNprColor[] colors = Array.Empty<RenNprColor>();
    }
    [Serializable] public sealed class RenNprPresets
    {
        public string shaderName;
        public RenNprPreset[] materials = Array.Empty<RenNprPreset>();
    }
    public static class RenNprReviewBuilder
    {
        public const string Root = "Assets/CharacterArt/Generated/RenNprReview";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenNprReview.unity";
        public const string ManifestPath = Root + "/RenNprReviewManifest.json";
        [Serializable] sealed class Evidence
        {
            public string sourceSha256, shaderSha256, shaderIncludeSha256, presetSha256, unityVersion;
            public string method = "Two new prefabs reference the same frozen V2 shared mesh and texture assets. Existing V2 materials, scene and geometry are not overwritten. Per-instance runtime material clones supply the face lighting frame without MaterialPropertyBlocks.";
            public int triangles, sharedMeshes, mappedMaterials;
        }

        [MenuItem("Lucid Loop/Character Art/Build Ren NPR Review")]
        public static void Build()
        {
            var config = JsonUtility.FromJson<RenNprReviewManifest>(File.ReadAllText(ManifestPath));
            if (config == null || string.IsNullOrWhiteSpace(config.reviewNote)) throw new InvalidDataException("A real NPR review manifest is required.");
            var source = JsonUtility.FromJson<RenCompleteHeadManifest>(File.ReadAllText(RenCompleteHeadBuilder.ManifestPath));
            Verify(source.sourceFbxAsset, config.sourceFbxSha256);
            if (source.sourceSha256 != config.sourceFbxSha256) throw new InvalidDataException("NPR review must use the frozen selected V2 source.");
            Verify(config.shaderAsset, config.shaderSha256); Verify(config.presetAsset, config.presetSha256);
            Verify(config.shaderIncludeAsset, config.shaderIncludeSha256);
            foreach (var map in source.materials) if (!string.IsNullOrWhiteSpace(map.baseColorAsset)) Verify(map.baseColorAsset, map.baseColorSha256);
            var presets = JsonUtility.FromJson<RenNprPresets>(File.ReadAllText(config.presetAsset));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(config.shaderAsset);
            if (!shader || shader.name != presets.shaderName || !shader.isSupported) throw new InvalidDataException("NPR shader is unavailable or unsupported.");
            Folder(Root + "/Viewer");
            var previous = SceneManager.GetActiveScene();
            var original = SceneManager.GetSceneByPath(RenCompleteHeadBuilder.ScenePath);
            var opened = !original.IsValid() || !original.isLoaded;
            if (Application.isBatchMode) { original = EditorSceneManager.OpenScene(RenCompleteHeadBuilder.ScenePath, OpenSceneMode.Single); opened = true; }
            else if (opened) original = EditorSceneManager.OpenScene(RenCompleteHeadBuilder.ScenePath, OpenSceneMode.Additive);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var template = original.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RenBustComparisonViewer>(true)).Single();
                var sourceCandidate = template.Candidates.Single(candidate => candidate.Id == "ren-complete-painted");
                var evidence = new Evidence { sourceSha256 = source.sourceSha256, shaderSha256 = config.shaderSha256,
                    shaderIncludeSha256 = config.shaderIncludeSha256, presetSha256 = config.presetSha256, unityVersion = Application.unityVersion, mappedMaterials = source.materials.Length };
                var candidates = CreateCandidates(sourceCandidate, source, presets, shader, evidence);
                var viewer = Object.Instantiate(template.gameObject).GetComponent<RenBustComparisonViewer>();
                SceneManager.MoveGameObjectToScene(viewer.gameObject, scene); viewer.gameObject.name = "Ren NPR comparison";
                viewer.Title = "Ren / NPR shader comparison"; viewer.Candidates = candidates;
                viewer.SelectCandidate(0, true); viewer.SelectCandidate(1, false);
                viewer.ShowReference = true; viewer.ReferenceIndex = 0;
                viewer.SetLighting(1); viewer.SetSourceNormalMaps(false); viewer.SetView(0); viewer.UpdateLayout();
                foreach (var camera in new[] { viewer.LeftCamera, viewer.RightCamera }) camera.farClipPlane = 6;
                foreach (var light in viewer.NeutralLights.Concat(viewer.NightclubLights))
                    if (light && light.type == LightType.Directional)
                    { light.shadows = LightShadows.Soft; light.shadowBias = .025f; light.shadowNormalBias = .025f; light.shadowNearPlane = .05f; light.GetUniversalAdditionalLightData().usePipelineSettings = false; }
                var controls = viewer.GetComponent<RenCompleteHeadController>(); controls.Viewer = viewer;
                controls.ResetFace(); controls.ShowHair = controls.ShowCap = true; controls.IdleBlink = true; controls.ApplyNow();
                var review = viewer.gameObject.AddComponent<RenNprReviewController>();
                review.Viewer = viewer; review.FaceControls = controls; review.SourceSha256 = source.sourceSha256;
                review.ReviewPipeline = CreateStudyPipeline();
                review.ShaderSha256 = config.shaderSha256; review.ContractSha256 = config.presetSha256;
                review.ShaderIncludeSha256 = config.shaderIncludeSha256;
                review.ControllerSha256 = FileHash("Assets/CharacterArt/Runtime/RenNprReviewController.cs");
                review.ApplyNow();
                var head = viewer.RightInstance.GetComponentsInChildren<Renderer>(true).Single(renderer => renderer.name == "Ren_Head");
                var localHeadBounds = RenNprReviewController.CalculateFaceLocalBounds(viewer.RightInstance.transform, head);
                foreach (var material in candidates[1].LitMaterials.Concat(candidates[1].BaseColorMaterials))
                { RenNprReviewController.SetFaceFrame(material, viewer.RightInstance.transform, localHeadBounds); EditorUtility.SetDirty(material); }
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.skybox = null; RenderSettings.fog = false;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Failed to save isolated NPR scene.");
                File.WriteAllText(Root + "/RenNprReviewEvidence.json", JsonUtility.ToJson(evidence, true));
                AssetDatabase.ImportAsset(Root + "/RenNprReviewEvidence.json"); AssetDatabase.SaveAssets();
                Debug.Log("REN_NPR_REVIEW_BUILD_OK: " + ScenePath);
            }
            finally
            {
                if (opened && original.IsValid() && original.isLoaded) EditorSceneManager.CloseScene(original, true);
                if (!Application.isBatchMode && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        static RenBustCandidate[] CreateCandidates(RenBustCandidate sourceCandidate, RenCompleteHeadManifest source, RenNprPresets presets, Shader shader, Evidence evidence)
        {
            var instance = Object.Instantiate(sourceCandidate.Prefab);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                evidence.sharedMeshes = renderers.Length;
                evidence.triangles = renderers.Sum(renderer => MeshOf(renderer).triangles.Length / 3);
                var diffuse = PrefabUtility.SaveAsPrefabAsset(instance, Root + "/Viewer/RenNprReview-OldDiffuse.prefab");
                var npr = new Material[source.materials.Length]; var unlit = new Material[source.materials.Length];
                for (var i = 0; i < source.materials.Length; i++)
                {
                    var map = source.materials[i]; var preset = presets.materials.Single(item => item.sourceName == map.sourceName);
                    npr[i] = CreateMaterial(i, map, preset, shader, false); unlit[i] = CreateMaterial(i, map, preset, shader, true);
                }
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    {
                        var index = Array.IndexOf(sourceCandidate.LitMaterials, material);
                        if (index < 0) index = Array.IndexOf(sourceCandidate.BaseColorMaterials, material);
                        if (index < 0) throw new InvalidDataException("Frozen V2 material index is unmapped: " + renderer.name);
                        return npr[index];
                    }).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                var nprPrefab = PrefabUtility.SaveAsPrefabAsset(instance, Root + "/Viewer/RenNprReview-NPR.prefab");
                var oldCandidate = Candidate("ren-npr-old", "Current prototype / old diffuse", diffuse, sourceCandidate.LitMaterials, sourceCandidate.BaseColorMaterials, evidence.triangles);
                var newCandidate = Candidate("ren-npr-new", "Current prototype / NPR trial", nprPrefab, npr, unlit, evidence.triangles);
                return new[] { oldCandidate, newCandidate };
            }
            finally { Object.DestroyImmediate(instance); }
        }
        static RenBustCandidate Candidate(string id, string label, GameObject prefab, Material[] lit, Material[] unlit, int triangles) =>
            new RenBustCandidate { Id = id, Label = label, Prefab = prefab, LitMaterials = lit, BaseColorMaterials = unlit,
                SourceNormalLitMaterials = lit, TriangleCount = triangles, Provider = "Same frozen V2", ModelLabel = "Shader comparison only",
                Pose = "Shared mouth / blink / gaze", TextureSummary = "Same source albedo and vertex colors",
                Notes = "Geometry, jaw and hair are unchanged; inspect shading independently." };
        static Material CreateMaterial(int index, RenCompleteHeadMaterial map, RenNprPreset preset, Shader shader, bool unlit)
        {
            var path = Root + "/Viewer/RenNprReview-" + index + (unlit ? "-unlit.mat" : "-lit.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            var color = map.baseColorSrgb.linear;
            material.SetVector("_BaseColor", new Vector4(color.r, color.g, color.b, color.a));
            material.SetTexture("_BaseMap", string.IsNullOrWhiteSpace(map.baseColorAsset) ? Texture2D.whiteTexture : AssetDatabase.LoadAssetAtPath<Texture2D>(map.baseColorAsset));
            material.SetFloat("_UseBaseMap", string.IsNullOrWhiteSpace(map.baseColorAsset) ? 0 : 1);
            material.SetFloat("_UseVertexColor", map.useVertexColor ? 1 : 0);
            material.SetFloat("_VertexColorSrgb", string.Equals(map.vertexColorEncoding, "sRGB", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            foreach (var item in preset.floats)
            { if (!material.HasProperty(item.name)) throw new InvalidDataException("Unknown NPR float " + item.name); material.SetFloat(item.name, item.value); }
            foreach (var item in preset.colors)
            {
                if (!material.HasProperty(item.name) || item.linearRgba == null || item.linearRgba.Length != 4) throw new InvalidDataException("Invalid NPR color " + item.name);
                material.SetColor(item.name, new Color(item.linearRgba[0], item.linearRgba[1], item.linearRgba[2], item.linearRgba[3]));
            }
            material.SetFloat("_Unlit", unlit ? 1 : 0); EditorUtility.SetDirty(material); return material;
        }
        public static void BuildWindowsViewer()
        {
            Build();
            var output = Path.GetFullPath(Argument("-renNprPlayerOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-npr-review-v1/RenNprReview.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var previous = GraphicsSettings.defaultRenderPipeline; var previousQuality = QualitySettings.renderPipeline;
            var previousPath = AssetDatabase.GetAssetPath(previous); var previousQualityPath = AssetDatabase.GetAssetPath(previousQuality);
            try
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/RenNprReviewPipeline.asset");
                // Include the used soft-shadow variants during build; restore scratch settings afterward.
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("NPR review player build failed.");
                WriteShaderAudit();
            }
            finally
            {
                // Player build can unload unused assets; reload persistent references instead of restoring invalid handles.
                GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(previousPath) ? previous : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousPath);
                QualitySettings.renderPipeline = string.IsNullOrEmpty(previousQualityPath) ? previousQuality : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousQualityPath);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("REN_NPR_REVIEW_PLAYER_OK: " + output);
        }
        static UniversalRenderPipelineAsset CreateStudyPipeline()
        {
            var path = Root + "/RenNprReviewPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!pipeline)
            {
                var inherited = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if (!inherited) throw new InvalidOperationException("The isolated review requires an existing URP pipeline to clone.");
                pipeline = Object.Instantiate(inherited); pipeline.name = "RenNprReviewPipeline"; AssetDatabase.CreateAsset(pipeline, path);
            }
            var data = new SerializedObject(pipeline);
            data.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            data.FindProperty("m_SoftShadowsSupported").boolValue = true;
            data.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            data.FindProperty("m_ShadowDistance").floatValue = 6;
            data.FindProperty("m_ShadowCascadeCount").intValue = 1;
            data.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            data.FindProperty("m_SoftShadowQuality").intValue = 2;
            data.FindProperty("m_ShadowDepthBias").floatValue = .025f;
            data.FindProperty("m_ShadowNormalBias").floatValue = .025f;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); return pipeline;
        }
        [Serializable] sealed class ShaderAudit
        {
            public string shader, unityVersion, srpBatcherApi, srpBatcherIssue;
            public int srpBatcherCompatibilityCode = -1;
            public string[] messages, passes;
            public string scope = "D3D11 player build and live review pipeline only. Additional-shadow, Forward+, Metal/iPhone, and all theoretical variants are not verified.";
        }
        public static void WriteShaderAudit()
        {
            var config = JsonUtility.FromJson<RenNprReviewManifest>(File.ReadAllText(ManifestPath));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(config.shaderAsset);
            var messages = ShaderUtil.GetShaderMessages(shader);
            if (messages.Any(item => item.severity.ToString() == "Error")) throw new InvalidOperationException("NPR shader compilation reported errors.");
            var audit = new ShaderAudit { shader = shader.name, unityVersion = Application.unityVersion,
                messages = messages.Select(item => item.severity + ": " + item.message).ToArray() };
            var probe = new Material(shader);
            try
            {
                audit.passes = Enumerable.Range(0, probe.passCount).Select(probe.GetPassName).ToArray();
                foreach (var pass in Enumerable.Range(0, probe.passCount)) ShaderUtil.CompilePass(probe, pass, true);
                probe.SetPass(0);
            }
            finally { Object.DestroyImmediate(probe); }
            var method = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Shader), typeof(int) }, null);
            audit.srpBatcherApi = method == null ? "Unavailable; source layout only" : "ShaderUtil.GetSRPBatcherCompatibilityCode(shader,0)";
            if (method != null) audit.srpBatcherCompatibilityCode = Convert.ToInt32(method.Invoke(null, new object[] { shader, 0 }));
            var reasonMethod = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Shader), typeof(int), typeof(int) }, null);
            if (reasonMethod != null) audit.srpBatcherIssue = Convert.ToString(reasonMethod.Invoke(null, new object[] { shader, 0, audit.srpBatcherCompatibilityCode }));
            File.WriteAllText(Root + "/RenNprReviewShaderAudit.json", JsonUtility.ToJson(audit, true));
            AssetDatabase.ImportAsset(Root + "/RenNprReviewShaderAudit.json");
        }
        static Mesh MeshOf(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        static void Verify(string path, string expected)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Frozen NPR input hash mismatch: " + path);
        }
        static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string Argument(string name)
        { var args = Environment.GetCommandLineArgs(); var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }
}
