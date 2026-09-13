using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenTokonTextureInput { public string asset, sha256; public bool sRGB; }
    [Serializable] public sealed class RenTokonMaterialInput
    {
        public string sourceMaterialAsset, category;
        public RenTokonTextureInput baseOverride, shadow, control, closedBase, closedShadow, revisedControl;
        public float faceMode = -1, revisedFaceMode = -1;
    }
    [Serializable] public sealed class RenTokonManifest
    {
        public string sourceScene, sourceFbxAsset, sourceSha256, shaderAsset, shaderSha256, includeAsset, includeSha256, inkAsset, inkSha256, presetAsset, presetSha256;
        public RenTokonMaterialInput[] materials;
        public string[] notes;
    }
    public static class RenTokonReviewBuilder
    {
        public const string Root = "Assets/CharacterArt/Generated/RenTokonReview";
        public const string ManifestPath = Root + "/RenTokonManifest.json";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenTokonReview.unity";
        [Serializable] sealed class ShaderAudit
        {
            public string shader, sha256, srpReason; public int srpCode = -1;
            public string[] passes, messages;
        }
        public static void BuildWindowsViewer()
        {
            if (!Application.isBatchMode || Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Tokon compilation/review is scratch-only while main imports are held.");
            var config = JsonUtility.FromJson<RenTokonManifest>(File.ReadAllText(ManifestPath));
            foreach (var pair in new[] { (config.sourceFbxAsset,config.sourceSha256),(config.shaderAsset,config.shaderSha256),(config.includeAsset,config.includeSha256),(config.inkAsset,config.inkSha256),(config.presetAsset,config.presetSha256) }) Verify(pair.Item1,pair.Item2);
            if (File.ReadAllText(config.shaderAsset).Contains("_ADDITIONAL_LIGHTS_VERTEX")) throw new InvalidDataException("Unimplemented vertex-light variant must be removed before compilation.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(config.shaderAsset); var ink = AssetDatabase.LoadAssetAtPath<Shader>(config.inkAsset);
            var presets = JsonUtility.FromJson<RenNprPresets>(File.ReadAllText(config.presetAsset));
            if (!shader || !ink || shader.name != presets.shaderName) throw new InvalidDataException("Tokon shader/preset contract mismatch.");
            Folder(Root + "/Materials");
            foreach (var input in config.materials)
                foreach (var texture in new[] { input.baseOverride,input.shadow,input.control,input.closedBase,input.closedShadow,input.revisedControl }) if (texture != null && !string.IsNullOrEmpty(texture.asset)) ImportTexture(texture);
            var scene = EditorSceneManager.OpenScene(config.sourceScene, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<RenHReferenceAnimationController>();
            if (!controller || controller.SourceSha256 != config.sourceSha256) throw new InvalidDataException("The frozen H diagnostic scene/source does not match.");
            foreach (var priorProbe in Object.FindObjectsByType<RenHReferenceCaptureProbe>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Object.DestroyImmediate(priorProbe);
            controller.SourceLabel = "Tokon shader diagnostic / unchanged rejected H eye geometry / no appearance approval";
            var materialPairs = new Dictionary<string, (Material baseline,Material tokon,bool closed,Texture2D revisedControl,float revisedFaceMode)>(StringComparer.Ordinal);
            foreach (var input in config.materials)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>(input.sourceMaterialAsset);
                if (!source) throw new InvalidDataException("Missing frozen source material: " + input.sourceMaterialAsset);
                var name = Path.GetFileNameWithoutExtension(input.sourceMaterialAsset);
                var baseline = MaterialAsset(source,Root + "/Materials/Previous-" + name + ".mat");
                if (input.baseOverride != null && !string.IsNullOrEmpty(input.baseOverride.asset)) baseline.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(input.baseOverride.asset));
                var candidate = new Material(shader); var preset = presets.materials.Single(p => p.sourceName == input.category);
                foreach (var property in new[] { "_BaseColor", "_FaceForwardWS", "_FaceRightWS", "_FaceUpWS", "_FaceCenterWS" }) candidate.SetVector(property,baseline.GetVector(property));
                foreach (var property in new[] { "_UseBaseMap", "_UseVertexColor", "_VertexColorSrgb", "_FaceWidth" }) candidate.SetFloat(property,baseline.GetFloat(property));
                candidate.SetTexture("_BaseMap",baseline.GetTexture("_BaseMap")); candidate.SetTextureScale("_BaseMap",baseline.GetTextureScale("_BaseMap")); candidate.SetTextureOffset("_BaseMap",baseline.GetTextureOffset("_BaseMap"));
                foreach (var entry in preset.floats) { RequireProperty(candidate,entry.name); candidate.SetFloat(entry.name,entry.value); }
                foreach (var entry in preset.colors) { RequireProperty(candidate,entry.name); candidate.SetVector(entry.name,new Vector4(entry.linearRgba[0],entry.linearRgba[1],entry.linearRgba[2],entry.linearRgba[3])); }
                candidate.SetFloat("_Cull",baseline.GetFloat("_Cull")); candidate.SetFloat("_Unlit",0);
                BindTexture(candidate,"_ShadowMap","_UseShadowMap",input.shadow); BindTexture(candidate,"_ControlMap","_UseControlMap",input.control);
                var closed = input.closedBase != null && !string.IsNullOrEmpty(input.closedBase.asset);
                if (closed && (input.closedShadow == null || string.IsNullOrEmpty(input.closedShadow.asset))) throw new InvalidDataException("Closed base and shadow endpoints must be paired.");
                if (closed) { candidate.SetTexture("_ClosedBaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(input.closedBase.asset)); candidate.SetTexture("_ClosedShadowMap",AssetDatabase.LoadAssetAtPath<Texture2D>(input.closedShadow.asset)); }
                candidate.SetFloat("_ClosedWeight",0);
                if (input.faceMode >= 0) candidate.SetFloat("_FaceMode",input.faceMode);
                var saved = MaterialAsset(candidate,Root + "/Materials/Tokon-" + name + ".mat"); Object.DestroyImmediate(candidate);
                var revisedControl = input.revisedControl == null || string.IsNullOrEmpty(input.revisedControl.asset) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(input.revisedControl.asset);
                materialPairs.Add(input.sourceMaterialAsset,(baseline,saved,closed,revisedControl,input.revisedFaceMode));
            }
            var study = controller.gameObject.AddComponent<RenTokonReviewController>(); study.Controller = controller;
            study.ManifestSha256 = Hash(ManifestPath); study.ShaderSha256 = config.shaderSha256; study.IncludeSha256 = config.includeSha256; study.InkSha256 = config.inkSha256;
            study.RuntimeSha256 = Hash("Assets/CharacterArt/Runtime/RenTokonReviewController.cs"); study.BuilderSha256 = Hash("Assets/CharacterArt/Editor/RenTokonReviewBuilder.cs");
            var slots = new List<RenTokonReviewSlot>();
            foreach (var renderer in controller.ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var path = AssetDatabase.GetAssetPath(materials[slot]);
                    if (!materialPairs.TryGetValue(path,out var pair)) throw new InvalidDataException("Unmapped frozen material: " + renderer.name + " / " + path);
                    materials[slot] = pair.baseline; slots.Add(new RenTokonReviewSlot { renderer = renderer,slot = slot,tokon = pair.tokon,closedEndpoint = pair.closed,revisedControl = pair.revisedControl,revisedFaceMode = pair.revisedFaceMode });
                }
                renderer.sharedMaterials = materials;
            }
            study.Slots = slots.ToArray();
            var pipelinePath = Root + "/RenTokonReviewPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (!pipeline) { pipeline = Object.Instantiate(controller.ReviewPipeline); pipeline.name = "RenTokonReviewPipeline"; AssetDatabase.CreateAsset(pipeline,pipelinePath); }
            var pipelineData = new SerializedObject(pipeline); pipelineData.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipelineData.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false; pipelineData.ApplyModifiedPropertiesWithoutUndo();
            if (pipeline.additionalLightsRenderingMode != LightRenderingMode.PerPixel) throw new InvalidOperationException("PerPixel additional lighting configuration failed.");
            controller.ReviewPipeline = pipeline; EditorUtility.SetDirty(pipeline);
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save scratch-only Tokon scene.");
            AssetDatabase.SaveAssets();
            var audits = new[] { Audit(shader,config.shaderSha256),Audit(ink,config.inkSha256) };
            File.WriteAllText(Root + "/RenTokonShaderAudit.json", "{\"shaders\":[" + string.Join(",",audits.Select(a => JsonUtility.ToJson(a,true))) + "]}");
            var output = Argument("-renTokonPlayerOutput"); if (string.IsNullOrEmpty(output)) throw new InvalidDataException("Explicit isolated player output required.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var prior = GraphicsSettings.defaultRenderPipeline; var priorQuality = QualitySettings.renderPipeline;
            var priorPath = AssetDatabase.GetAssetPath(prior); var priorQualityPath = AssetDatabase.GetAssetPath(priorQuality);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64); var defaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            try
            {
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false); PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[] { GraphicsDeviceType.Direct3D11 });
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },locationPathName = output,target = BuildTarget.StandaloneWindows64,options = BuildOptions.Development });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Tokon player build failed: " + report.summary.result);
                // Capture compiler messages after build as well; do not call a precompile empty error array proof.
                audits = new[] { Audit(shader,config.shaderSha256),Audit(ink,config.inkSha256) };
                File.WriteAllText(Root + "/RenTokonShaderAudit.json", "{\"shaders\":[" + string.Join(",",audits.Select(a => JsonUtility.ToJson(a,true))) + "]}");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(priorPath) ? prior : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(priorPath);
                QualitySettings.renderPipeline = string.IsNullOrEmpty(priorQualityPath) ? priorQuality : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(priorQualityPath);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,apis); PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,defaultApis); AssetDatabase.SaveAssets();
            }
            Debug.Log("REN_TOKON_PLAYER_OK: " + output);
        }
        static Material MaterialAsset(Material template,string path)
        {
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result) { result = new Material(template); AssetDatabase.CreateAsset(result,path); }
            else { result.shader = template.shader; result.CopyPropertiesFromMaterial(template); }
            result.name = Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(result); return result;
        }
        static void RequireProperty(Material material,string property) { if (!material.HasProperty(property)) throw new InvalidDataException("Unknown Tokon preset property " + property); }
        static void BindTexture(Material material,string property,string flag,RenTokonTextureInput input)
        {
            var enabled = input != null && !string.IsNullOrEmpty(input.asset); material.SetFloat(flag,enabled ? 1 : 0);
            if (enabled) material.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture2D>(input.asset));
        }
        static void ImportTexture(RenTokonTextureInput input)
        {
            Verify(input.asset,input.sha256);
            var importer = AssetImporter.GetAtPath(input.asset) as TextureImporter; if (!importer) throw new InvalidDataException("Texture missing: " + input.asset);
            var changed = importer.sRGBTexture != input.sRGB || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 8192;
            importer.sRGBTexture = input.sRGB; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 8192;
            foreach (var platform in new[] { "Standalone","Android","iPhone" }) if (importer.GetPlatformTextureSettings(platform).overridden) { importer.ClearPlatformTextureSettings(platform); changed = true; }
            if (changed) importer.SaveAndReimport();
        }
        static ShaderAudit Audit(Shader shader,string hash)
        {
            var result = new ShaderAudit { shader = shader.name,sha256 = hash }; var material = new Material(shader);
            try { result.passes = Enumerable.Range(0,material.passCount).Select(material.GetPassName).ToArray(); for (var i = 0; i < material.passCount; i++) ShaderUtil.CompilePass(material,i,true); material.SetPass(0); }
            finally { Object.DestroyImmediate(material); }
            var messages = ShaderUtil.GetShaderMessages(shader); result.messages = messages.Select(m => m.severity + ": " + m.message).ToArray();
            if (messages.Any(m => m.severity.ToString() == "Error")) throw new InvalidOperationException("Shader errors in " + shader.name + ": " + string.Join("; ",result.messages));
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var code = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",flags,null,new[] { typeof(Shader),typeof(int) },null);
            if (code != null) result.srpCode = Convert.ToInt32(code.Invoke(null,new object[] { shader,0 }));
            var reason = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",flags,null,new[] { typeof(Shader),typeof(int),typeof(int) },null);
            result.srpReason = reason == null ? "API unavailable; not verified" : Convert.ToString(reason.Invoke(null,new object[] { shader,0,result.srpCode }));
            return result;
        }
        static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
        static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static void Verify(string path,string expected) { if (!File.Exists(path) || !string.Equals(Hash(path),expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Frozen input hash mismatch: " + path); }
        static string Argument(string name) { var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args,name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    }
}
