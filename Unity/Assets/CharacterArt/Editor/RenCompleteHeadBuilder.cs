using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenCompleteHeadMaterial
    {
        public string sourceName, baseColorAsset, baseColorSha256, vertexColorEncoding;
        public Color baseColorSrgb = Color.white;
        public bool useVertexColor;
    }
    [Serializable] public sealed class RenCompleteHeadManifest
    {
        public int schemaVersion = 1;
        public string sourceFbxAsset, sourceSha256, gazeContractAsset, gazeContractSha256, reviewNote;
        public Vector3 displayEulerAngles = new Vector3(0, 90, 0);
        public RenCompleteHeadMaterial[] materials = Array.Empty<RenCompleteHeadMaterial>();
        public string[] hairObjectNames = Array.Empty<string>();
        public string[] capObjectNames = new[] { "Ren_Cap" };
    }
    public static class RenCompleteHeadBuilder
    {
        public const string Root = "Assets/CharacterArt/Generated/RenCompleteHead";
        public const string ManifestPath = Root + "/RenCompleteHeadManifest.json";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenCompleteHead.unity";
        [Serializable] sealed class Radians { public float yaw_per_x, negative_pitch_per_y; }
        [Serializable] sealed class Contract { public Radians radians; }
        [Serializable] sealed class MeshAudit { public string name; public int vertices, triangles, uv0, blendShapes; }
        [Serializable] sealed class NativeAudit { public string sourceSha256; public MeshAudit[] meshes; }
        [Serializable] sealed class Evidence
        {
            public string sourceSha256, gazeContractSha256, reviewNote, unityVersion;
            public string method = "New source and scene only; ellipsoid gaze axes calibrated from imported EMPTY markers. Stale additive iris keys rejected. Native eye samples are transform audits, not rendered live-control proof.";
            public int triangles;
            public float normalizationScale;
            public Vector3 normalizationOffset, boundsSize;
            public RenCompleteHeadGazeCalibration[] gaze;
        }

        [MenuItem("Lucid Loop/Character Art/Build Ren Complete Head Review")]
        public static void Build()
        {
            var manifest = JsonUtility.FromJson<RenCompleteHeadManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.reviewNote))
                throw new InvalidDataException("Supply a reviewed real complete-head manifest.");
            Verify(manifest.sourceFbxAsset, manifest.sourceSha256); Verify(manifest.gazeContractAsset, manifest.gazeContractSha256);
            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(manifest.gazeContractAsset));
            if (contract?.radians == null || contract.radians.yaw_per_x <= 0 || contract.radians.negative_pitch_per_y >= 0)
                throw new InvalidDataException("Unexpected continuous-gaze contract.");
            var importer = AssetImporter.GetAtPath(manifest.sourceFbxAsset) as ModelImporter;
            if (!importer) throw new InvalidDataException("Complete-head source must be its own FBX.");
            importer.importAnimation = false; importer.importBlendShapes = true; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import; importer.importBlendShapeNormals = ModelImporterNormals.Import;
            importer.preserveHierarchy = true; importer.SaveAndReimport();
            Folder(Root + "/Viewer");
            var evidence = new Evidence { sourceSha256 = manifest.sourceSha256, gazeContractSha256 = manifest.gazeContractSha256,
                reviewNote = manifest.reviewNote, unityVersion = Application.unityVersion };
            var actual = CreateCandidates(manifest, contract, evidence);
            var previous = SceneManager.GetActiveScene();
            var sourceScene = SceneManager.GetSceneByPath(RenFaceStudyBuilder.ScenePath); var openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (Application.isBatchMode)
            {
                // The isolated batch Editor starts with an untitled scene; discard only that scratch scene.
                sourceScene = EditorSceneManager.OpenScene(RenFaceStudyBuilder.ScenePath, OpenSceneMode.Single); openedSource = true;
            }
            else if (openedSource) sourceScene = EditorSceneManager.OpenScene(RenFaceStudyBuilder.ScenePath, OpenSceneMode.Additive);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var template = sourceScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RenBustComparisonViewer>(true)).Single();
                var viewer = Object.Instantiate(template.gameObject).GetComponent<RenBustComparisonViewer>();
                SceneManager.MoveGameObjectToScene(viewer.gameObject, scene); viewer.gameObject.name = "Ren complete-head review";
                var oldController = viewer.GetComponent<RenFaceStudyController>(); if (oldController) Object.DestroyImmediate(oldController);
                viewer.Title = "Ren / complete-head review";
                viewer.Candidates = actual.Concat(template.Candidates.Select(candidate => CopyComparison(candidate, evidence))).ToArray();
                viewer.SelectCandidate(1, true); viewer.SelectCandidate(0, false); viewer.ShowReference = true; viewer.ReferenceIndex = 0;
                viewer.SetLighting(1); viewer.SetSourceNormalMaps(false); viewer.SetView(0); viewer.UpdateLayout();
                var controller = viewer.gameObject.AddComponent<RenCompleteHeadController>(); controller.Viewer = viewer;
                controller.ResetFace(); controller.ShowHair = true; controller.ShowCap = true; controller.IdleBlink = true; controller.ApplyNow();
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.skybox = null; RenderSettings.fog = false;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Failed to save complete-head scene.");
                File.WriteAllText(Root + "/RenCompleteHeadEvidence.json", JsonUtility.ToJson(evidence, true));
                AssetDatabase.ImportAsset(Root + "/RenCompleteHeadEvidence.json"); AssetDatabase.SaveAssets();
                Debug.Log("REN_COMPLETE_HEAD_BUILD_OK: " + ScenePath);
            }
            finally
            {
                if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
                if (!Application.isBatchMode && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static void BuildWindowsViewer() { Build(); BuildPlayerFromSavedScene(); }
        public static void AuditNativeSource()
        {
            var manifest = JsonUtility.FromJson<RenCompleteHeadManifest>(File.ReadAllText(ManifestPath));
            Verify(manifest.sourceFbxAsset, manifest.sourceSha256);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.sourceFbxAsset));
            try
            {
                var output = Path.GetFullPath(Argument("-renCompleteAuditOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-complete-head-audit"));
                WriteNativeMeshAudit(instance, manifest.sourceSha256, output);
                Debug.Log("REN_COMPLETE_HEAD_NATIVE_AUDIT_OK: " + output);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        public static void BuildPlayerFromSavedScene()
        {
            var output = Path.GetFullPath(Argument("-renCompletePlayerOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-complete-head-viewer/RenCompleteHead.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Complete-head player build failed.");
            Debug.Log("REN_COMPLETE_HEAD_PLAYER_OK: " + output);
        }

        static RenBustCandidate[] CreateCandidates(RenCompleteHeadManifest manifest, Contract contract, Evidence evidence)
        {
            var root = new GameObject("RenCompleteHead");
            try
            {
                var frame = new GameObject("Shared normalization").transform; frame.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.sourceFbxAsset));
                if (model.transform.localPosition.sqrMagnitude > 1e-10f || Quaternion.Angle(model.transform.localRotation, Quaternion.identity) > .001f ||
                    (model.transform.localScale - Vector3.one).sqrMagnitude > 1e-10f)
                    throw new InvalidDataException("The complete-head native FBX root needs an explicit import-axis review before display placement.");
                model.transform.SetParent(frame, false); model.transform.localEulerAngles = manifest.displayEulerAngles;
                var rig = root.AddComponent<RenCompleteHeadRig>(); rig.ContractSha256 = manifest.gazeContractSha256;
                rig.SourceSha256 = manifest.sourceSha256;
                rig.RuntimeRigSha256 = FileHash("Assets/CharacterArt/Runtime/RenCompleteHeadRig.cs");
                rig.RuntimeControllerSha256 = FileHash("Assets/CharacterArt/Runtime/RenCompleteHeadController.cs");
                rig.YawRadians = contract.radians.yaw_per_x; rig.NegativePitchRadians = contract.radians.negative_pitch_per_y;
                evidence.gaze = RenCompleteHeadGazeImport.Configure(model, rig);
                var allTransforms = model.GetComponentsInChildren<Transform>(true);
                rig.CapObjects = manifest.capObjectNames.Select(name => RenCompleteHeadGazeImport.One(allTransforms, name).gameObject).ToArray();
                rig.HairObjects = manifest.hairObjectNames.Where(name => !manifest.capObjectNames.Contains(name))
                    .Select(name => RenCompleteHeadGazeImport.One(allTransforms, name).gameObject).ToArray();
                VerifyShapes(model);
                rig.SetCapVisible(true);
                if (rig.CaptureCapWeights().Length == 0) throw new InvalidDataException("Complete-head hair is missing its capOn fitting controls.");
                rig.Apply(1, 0, 0, 0, Vector2.zero); var bounds = BoundsOf(model);
                rig.Apply(0, 1, 0, 0, Vector2.zero); bounds.Encapsulate(BoundsOf(model)); rig.Apply(1, 0, 0, 0, Vector2.zero);
                rig.SetCapVisible(false); bounds.Encapsulate(BoundsOf(model)); rig.SetCapVisible(true);
                evidence.boundsSize = bounds.size; evidence.normalizationScale = 1.65f / bounds.size.y;
                evidence.normalizationOffset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * evidence.normalizationScale;
                var audit = Argument("-renCompleteAuditOutput");
                frame.localScale = Vector3.one * evidence.normalizationScale; frame.localPosition = evidence.normalizationOffset;
                var shader = Shader.Find("LucidLoop/RenFaceStudy/Diffuse"); if (!shader) throw new InvalidOperationException("Review shader unavailable.");
                var lit = new List<Material>(); var unlit = new List<Material>(); var clayLit = new List<Material>(); var clayUnlit = new List<Material>();
                var slots = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var map in manifest.materials)
                {
                    var index = slots.Count; slots.Add(map.sourceName, index);
                    var texture = string.IsNullOrWhiteSpace(map.baseColorAsset) ? null : ImportTexture(map.baseColorAsset, map.baseColorSha256);
                    lit.Add(Material(index, map, texture, shader, false, false)); unlit.Add(Material(index, map, texture, shader, false, true));
                    clayLit.Add(Material(index, map, texture, shader, true, false)); clayUnlit.Add(Material(index, map, texture, shader, true, true));
                }
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh = MeshOf(renderer); if (!mesh) continue; evidence.triangles += mesh.triangles.Length / 3;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    {
                        if (!material || !slots.TryGetValue(material.name, out var index)) throw new InvalidDataException("Unmapped complete-head material: " + renderer.name);
                        return lit[index];
                    }).ToArray(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                if (!string.IsNullOrWhiteSpace(audit)) WriteNativeEyeSamples(model, rig, evidence, Path.GetFullPath(audit));
                var painted = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Viewer/RenCompleteHead-Painted.prefab");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material => clayLit[lit.IndexOf(material)]).ToArray();
                var clay = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Viewer/RenCompleteHead-Clay.prefab");
                return new[] { Candidate("ren-complete-painted", "Complete head / painted trial", painted, lit, unlit, evidence.triangles),
                    Candidate("ren-complete-clay", "Complete head / clay", clay, clayLit, clayUnlit, evidence.triangles) };
            }
            finally { Object.DestroyImmediate(root); }
        }

        static RenBustCandidate CopyComparison(RenBustCandidate source, Evidence evidence)
        {
            var root = Object.Instantiate(source.Prefab);
            try
            {
                var frame = RenCompleteHeadGazeImport.One(root.GetComponentsInChildren<Transform>(true), "Shared normalization");
                frame.localScale = Vector3.one * evidence.normalizationScale; frame.localPosition = evidence.normalizationOffset;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Viewer/RenCompleteHead-Comparison-" + source.Id + ".prefab");
                return new RenBustCandidate { Id = "comparison-" + source.Id, Label = source.Label, Provider = source.Provider, ModelLabel = source.ModelLabel,
                    Pose = "Earlier source held at rest", TextureSummary = source.TextureSummary, Notes = source.Notes,
                    TriangleCount = source.TriangleCount, Prefab = prefab, LitMaterials = source.LitMaterials,
                    BaseColorMaterials = source.BaseColorMaterials, SourceNormalLitMaterials = source.SourceNormalLitMaterials };
            }
            finally { Object.DestroyImmediate(root); }
        }
        static RenBustCandidate Candidate(string id, string label, GameObject prefab, List<Material> lit, List<Material> unlit, int triangles) =>
            new RenBustCandidate { Id = id, Label = label, Provider = "Local assembly", ModelLabel = "Repaired head + hair trial",
                Pose = "Live mouth / blink / ellipsoid gaze", TextureSummary = "Source paint / diffuse review",
                Notes = "Construction and painting trial. Final likeness, full speech and phone performance pending.",
                TriangleCount = triangles, Prefab = prefab, LitMaterials = lit.ToArray(), BaseColorMaterials = unlit.ToArray(), SourceNormalLitMaterials = lit.ToArray() };

        static Material Material(int index, RenCompleteHeadMaterial map, Texture2D texture, Shader shader, bool clay, bool unlit)
        {
            var path = Root + "/Viewer/RenCompleteHead-" + index + (clay ? "-clay" : "-paint") + (unlit ? "-unlit.mat" : "-lit.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            // Clay keeps the separate eye/oral pieces readable. Only authored skin/hair/cap slots lose their paint.
            var useClay = clay && !(map.sourceName.IndexOf("Iris", StringComparison.OrdinalIgnoreCase) >= 0 || map.sourceName.IndexOf("Sclera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                map.sourceName.IndexOf("Teeth", StringComparison.OrdinalIgnoreCase) >= 0 || map.sourceName.IndexOf("Tongue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                map.sourceName.IndexOf("Liner", StringComparison.OrdinalIgnoreCase) >= 0 || map.sourceName.IndexOf("Lash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                map.sourceName.IndexOf("Cavity", StringComparison.OrdinalIgnoreCase) >= 0 || map.sourceName.IndexOf("Pupil", StringComparison.OrdinalIgnoreCase) >= 0);
            material.shader = shader; material.SetVector("_LinearBaseColor", (useClay ? new Color(.6f, .62f, .66f) : map.baseColorSrgb).linear);
            material.SetFloat("_UseVertexColor", !useClay && map.useVertexColor ? 1 : 0);
            material.SetFloat("_VertexColorSrgb", string.Equals(map.vertexColorEncoding, "sRGB", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            material.SetTexture("_BaseMap", texture); material.SetFloat("_UseBaseMap", texture && !useClay ? 1 : 0);
            material.SetFloat("_NormalScale", 0); material.SetFloat("_Unlit", unlit ? 1 : 0); EditorUtility.SetDirty(material); return material;
        }
        static Texture2D ImportTexture(string path, string hash)
        {
            Verify(path, hash); var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidDataException("Invalid complete-head texture: " + path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.maxTextureSize = 8192;
            importer.mipmapEnabled = true; importer.streamingMipmaps = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false; importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = 8192,
                format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed });
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void VerifyShapes(GameObject root)
        {
            var head = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(item => item.name == "Ren_Head");
            foreach (var name in new[] { "mouthSeal", "jawOpen_A", "eyeBlinkL", "eyeBlinkR" })
            {
                var index = Enumerable.Range(0, head.sharedMesh.blendShapeCount).Where(i => RenFaceStudyController.ShapeMatches(head.sharedMesh.GetBlendShapeName(i), name)).DefaultIfEmpty(-1).First();
                if (index < 0) throw new InvalidDataException("Missing complete-head channel " + name);
                var delta = new Vector3[head.sharedMesh.vertexCount]; head.sharedMesh.GetBlendShapeFrameVertices(index, head.sharedMesh.GetBlendShapeFrameCount(index) - 1, delta, null, null);
                if (!delta.Any(point => point.sqrMagnitude > 1e-14f)) throw new InvalidDataException("Empty complete-head channel " + name);
            }
        }
        static void WriteNativeEyeSamples(GameObject model, RenCompleteHeadRig rig, Evidence evidence, string output)
        {
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "RenCompleteHeadGazeCalibration.json"), JsonUtility.ToJson(evidence, true));
            WriteNativeMeshAudit(model, evidence.sourceSha256, output);
            foreach (var gaze in new[] { Vector2.zero, new Vector2(.5f, .5f), new Vector2(-.5f, .5f), new Vector2(.5f, -.5f), new Vector2(-.5f, -.5f), Vector2.one, -Vector2.one })
            {
                rig.Apply(1, 0, 0, 0, gaze);
                foreach (var side in new[] { "L", "R" })
                {
                    var iris = RenCompleteHeadGazeImport.One(model.GetComponentsInChildren<Transform>(true), "Ren_Eye_" + side + "_Iris");
                    var renderer = iris.GetComponent<Renderer>(); var mesh = MeshOf(renderer);
                    using (var writer = new BinaryWriter(File.Create(Path.Combine(output, "iris-" + side + "--" + gaze.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "--" + gaze.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".f32"))))
                        foreach (var point in mesh.vertices)
                        {
                            var native = model.transform.InverseTransformPoint(iris.TransformPoint(point));
                            writer.Write(native.x); writer.Write(native.y); writer.Write(native.z);
                        }
                }
            }
            rig.Apply(1, 0, 0, 0, Vector2.zero);
        }
        static void WriteNativeMeshAudit(GameObject model, string sourceSha256, string output)
        {
            var meshes = model.GetComponentsInChildren<Renderer>(true).Select(renderer =>
            {
                var mesh = MeshOf(renderer);
                return new MeshAudit { name = renderer.name, vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3,
                    uv0 = mesh.uv.Length, blendShapes = mesh.blendShapeCount };
            }).ToArray();
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "RenCompleteHeadNativeMeshes.json"),
                JsonUtility.ToJson(new NativeAudit { sourceSha256 = sourceSha256, meshes = meshes }, true));
        }
        static Bounds BoundsOf(GameObject root)
        {
            var bounds = new Bounds(); var any = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = MeshOf(renderer); if (!mesh) continue; Mesh baked = null;
                try
                {
                    if (renderer is SkinnedMeshRenderer skin) { baked = new Mesh(); skin.BakeMesh(baked, false); mesh = baked; }
                    foreach (var point in mesh.vertices)
                    { var world = renderer.transform.TransformPoint(point); if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; } else bounds.Encapsulate(world); }
                }
                finally { if (baked) Object.DestroyImmediate(baked); }
            }
            if (!any || bounds.size.y < .001f) throw new InvalidDataException("No valid complete-head bounds."); return bounds;
        }
        static Mesh MeshOf(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        static void Verify(string path, string expected)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(Root + "/", StringComparison.Ordinal) || !File.Exists(path))
                throw new InvalidDataException("Complete-head sources must have dedicated copies under " + Root);
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Complete-head source hash mismatch: " + path);
        }
        static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
