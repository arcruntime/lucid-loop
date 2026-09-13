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
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenFaceStudyMaterial
    {
        public string sourceName;
        public Color baseColorSrgb = Color.white;
        public bool useVertexColor;
    }

    [Serializable] public sealed class RenFaceStudyManifest
    {
        public int schemaVersion = 1;
        public string sourceFbxAsset, sourceSha256, reviewNote, originalArtistAsset;
        public string hairFbxAsset, hairSha256;
        public Vector3 hairPosition, hairEulerAngles;
        public Vector3 hairScale = Vector3.one;
        public Vector3 position, eulerAngles;
        public Vector3 scale = Vector3.one;
        public RenFaceStudyMaterial[] materials = Array.Empty<RenFaceStudyMaterial>();
        public RenBustManifestReference[] additionalReferences = Array.Empty<RenBustManifestReference>();
        public RenFaceStudyTextureTrial textureTrial;
    }

    /// <summary>Builds an isolated review of actual repaired geometry and its imported mouth shapes.</summary>
    public static class RenFaceStudyBuilder
    {
        public const string AssetRoot = "Assets/CharacterArt/Generated/RenFaceStudy";
        public const string ManifestPath = AssetRoot + "/RenFaceStudyManifest.json";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenFaceStudy.unity";

        [Serializable] sealed class ShapeEvidence
        {
            public string renderer, shape;
            public int frameCount, affectedVertices;
            public float lastFrameWeight, maxLocalDelta;
        }
        [Serializable] sealed class CaptureEvidence
        {
            public string file, lighting, view, poseGeometrySha256, materialStudy = "neutral-clay";
            public float mouthSeal, jawOpenA, blinkLeft, blinkRight, gazeX, gazeY;
            public bool hair;
        }
        [Serializable] sealed class Evidence
        {
            public string unityVersion, sourceFbxAsset, sourceSha256, reviewNote;
            public string status = "V2 local repaired face: independent mouth, stitched eyelids, iris gaze and seeded idle blink. Optional fitted hair is an unpainted geometry trial. Full speech, expressions, final likeness and phone performance remain pending.";
            public string framing = "One union of face poses and fitted hair determines shared normalization. Hair toggle never changes face size or position.";
            public string hairFbxAsset, hairSha256;
            public int hairTriangles;
            public string captureMethod = "Each capture renders temporary CPU-baked snapshots of the actual weighted SkinnedMeshRenderers. This avoids stale GPU skinning within a synchronous Editor render loop. Source/prefab meshes are not changed. Geometry hashes bind every capture to its current mouth pose.";
            public string lighting = "Neutral front key Euler(25,155,0), intensity 0.9, white fill 1.5 and cool rim 0.6. Club cool front key Euler(20,165,0), intensity 0.55, magenta 1.0 and cyan 1.4. Diffuse-only URP materials, no source maps, shadows, HDR or postprocessing.";
            public int triangles;
            public float normalizationScale;
            public Vector3 normalizationOffset, unionBoundsSize;
            public ShapeEvidence[] shapes;
            public CaptureEvidence[] captures;
        }

        [Serializable] sealed class ImportMesh
        {
            public string name;
            public int vertices, colors;
            public Vector3 localEulerAngles, localScale, boundsMin, boundsMax;
            public Color colorMin, colorMax;
            public string[] materials;
        }
        [Serializable] sealed class ImportEvidence
        {
            public string sourceSha256;
            public Vector3 rootEulerAngles, rootScale;
            public float importerScale;
            public ImportMesh[] meshes;
            public ShapeEvidence[] shapes;
        }

        public static void AuditImport()
        {
            var path = Argument("-renFaceAuditAsset") ?? AssetRoot + "/Sources/RenFaceStudy.fbx";
            var output = Path.GetFullPath(Argument("-renFaceAuditOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-face-study-import"));
            Directory.CreateDirectory(output);
            var instance = Object.Instantiate(Load<GameObject>(path));
            try
            {
                var records = new List<ImportMesh>();
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh = MeshOf(renderer); if (!mesh) continue;
                    var points = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
                    var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
                    var colors = mesh.colors;
                    var low = colors.Length == 0 ? Color.clear : new Color(colors.Min(c => c.r), colors.Min(c => c.g), colors.Min(c => c.b), colors.Min(c => c.a));
                    var high = colors.Length == 0 ? Color.clear : new Color(colors.Max(c => c.r), colors.Max(c => c.g), colors.Max(c => c.b), colors.Max(c => c.a));
                    records.Add(new ImportMesh { name = renderer.name, vertices = mesh.vertexCount, colors = colors.Length,
                        localEulerAngles = renderer.transform.localEulerAngles, localScale = renderer.transform.localScale,
                        boundsMin = bounds.min, boundsMax = bounds.max, colorMin = low, colorMax = high,
                        materials = renderer.sharedMaterials.Select(material => material ? material.name : "<missing>").ToArray() });
                    if (renderer.name == "Ren_Head")
                        using (var writer = new BinaryWriter(File.Create(Path.Combine(output, "RenFaceStudy-head-native-world.f32"))))
                            foreach (var point in points) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                }
                File.WriteAllText(Path.Combine(output, "RenFaceStudy-native-import.json"), JsonUtility.ToJson(new ImportEvidence
                {
                    sourceSha256 = Hash(path), rootEulerAngles = instance.transform.localEulerAngles, rootScale = instance.transform.localScale,
                    importerScale = ((ModelImporter)AssetImporter.GetAtPath(path)).globalScale, meshes = records.ToArray(), shapes = InspectShapes(instance)
                }, true));
                Debug.Log("REN_FACE_STUDY_IMPORT_AUDIT_OK: " + output);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [MenuItem("Lucid Loop/Character Art/Build Ren Face Study")]
        public static void Build() => Run(false);
        [MenuItem("Lucid Loop/Character Art/Build and Capture Ren Face Study")]
        public static void BuildAndCapture() => Run(true);
        public static void BuildTextureTrialAndCapture() => Run(false, true);
        public static void BuildTextureWindowsViewer()
        {
            var manifest = JsonUtility.FromJson<RenFaceStudyManifest>(File.ReadAllText(ManifestPath));
            if (manifest?.textureTrial == null || string.IsNullOrWhiteSpace(manifest.textureTrial.sourceFbxAsset))
                throw new InvalidDataException("Supply the audited static texture trial before building its player.");
            Run(false);
            if (!string.IsNullOrWhiteSpace(Argument("-renFaceAuditOutput"))) AuditImport();
            BuildPlayerFromSavedScene();
        }

        public static void BuildPlayerFromSavedScene()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Build the face-study scene first.", ScenePath);
            var output = Path.GetFullPath(Argument("-renFacePlayerOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-face-study/RenFaceStudy.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Face-study player build failed: " + report.summary.result);
            Debug.Log("REN_FACE_STUDY_PLAYER_OK: " + output);
        }

        static void Run(bool capture, bool textureCapture = false)
        {
            var manifest = JsonUtility.FromJson<RenFaceStudyManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.reviewNote))
                throw new InvalidDataException("Supply the reviewed local face export and a construction-review note.");
            if (!manifest.sourceFbxAsset.StartsWith(AssetRoot + "/", StringComparison.Ordinal))
                throw new InvalidDataException("The face study requires its own copied FBX under " + AssetRoot);
            var hash = Hash(manifest.sourceFbxAsset);
            if (!string.Equals(hash, manifest.sourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The source FBX does not match the reviewed manifest hash.");
            if (manifest.scale.x <= 0 || manifest.scale.y <= 0 || manifest.scale.z <= 0)
                throw new InvalidDataException("Source placement scale must be positive.");
            var source = Load<GameObject>(manifest.sourceFbxAsset);
            var artist = Load<Texture2D>(manifest.originalArtistAsset);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            var otherLights = Resources.FindObjectsOfTypeAll<Light>()
                .Where(light => light.gameObject.scene.IsValid() && light.gameObject.scene != scene && light.enabled).ToArray();
            try
            {
                foreach (var light in otherLights) light.enabled = false;
                SceneManager.SetActiveScene(scene); EnsureFolder(AssetRoot + "/Viewer");
                var evidence = new Evidence { unityVersion = Application.unityVersion, sourceFbxAsset = manifest.sourceFbxAsset,
                    sourceSha256 = hash, reviewNote = manifest.reviewNote };
                var candidates = CreateCandidates(source, manifest, evidence);
                if (manifest.textureTrial != null && !string.IsNullOrWhiteSpace(manifest.textureTrial.sourceFbxAsset))
                    candidates = candidates.Concat(new[] { RenFaceStudyTextureTrialBuilder.Create(manifest.textureTrial,
                        evidence.normalizationScale, evidence.normalizationOffset) }).ToArray();
                else if (textureCapture) throw new InvalidDataException("Supply an audited textureTrial manifest block before texture capture.");
                var viewer = CreateStage();
                viewer.Candidates = candidates;
                viewer.References = new[] { new RenBustReference { Label = "Original artist design", Texture = artist } }
                    .Concat(manifest.additionalReferences.Select(reference => new RenBustReference
                        { Label = reference.label, Texture = Load<Texture2D>(reference.textureAsset) })).ToArray();
                viewer.SelectCandidate(0, true); viewer.SelectCandidate(0, false);
                viewer.ShowReference = true; viewer.ReferenceIndex = 0;
                viewer.SetLighting(1); viewer.SetView(0); viewer.UpdateLayout();
                var controller = viewer.gameObject.AddComponent<RenFaceStudyController>(); controller.Viewer = viewer;
                controller.ResetAll();
                if (capture) evidence.captures = CapturePoses(viewer, controller);
                if (textureCapture) CaptureTextureTrial(viewer, controller, manifest.textureTrial);
                viewer.SelectCandidate(candidates.Length > 2 ? 2 : 0, false);
                controller.ResetAll(); controller.ShowHair = false; controller.IdleBlink = true; controller.ApplyNow();
                viewer.SetLighting(1); viewer.SetView(0); viewer.UpdateLayout();
                var quickCapture = Argument("-renFaceQuickCapture");
                if (!string.IsNullOrWhiteSpace(quickCapture))
                {
                    quickCapture = Path.GetFullPath(quickCapture); Directory.CreateDirectory(Path.GetDirectoryName(quickCapture));
                    Capture(viewer.RightCamera, quickCapture);
                    Debug.Log("REN_FACE_QUICK_CAPTURE_OK: " + quickCapture);
                }
                EnsureFolder(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                var evidencePath = AssetRoot + (candidates.Length > 2 ? "/RenFaceStudyTextureEvidence.json" : "/RenFaceStudyEvidence.json");
                File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
                AssetDatabase.ImportAsset(evidencePath); AssetDatabase.SaveAssets();
                Debug.Log("REN_FACE_STUDY_BUILD_OK: " + ScenePath);
            }
            finally
            {
                foreach (var light in otherLights) if (light) light.enabled = true;
                if (!Application.isBatchMode || SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static RenBustCandidate[] CreateCandidates(GameObject source, RenFaceStudyManifest manifest, Evidence evidence)
        {
            var root = new GameObject("RenFaceStudy-LocalFace");
            try
            {
                var frame = Child(root.transform, "Shared normalization", Vector3.zero);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(frame, false); model.transform.localPosition = manifest.position;
                model.transform.localEulerAngles = manifest.eulerAngles; model.transform.localScale = manifest.scale;
                GameObject hair = null;
                if (!string.IsNullOrWhiteSpace(manifest.hairFbxAsset))
                {
                    if (!manifest.hairFbxAsset.StartsWith(AssetRoot + "/", StringComparison.Ordinal) || Hash(manifest.hairFbxAsset) != manifest.hairSha256)
                        throw new InvalidDataException("Fitted hair does not match its dedicated source asset/hash.");
                    hair = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(manifest.hairFbxAsset));
                    hair.name = RenFaceStudyController.HairObjectName;
                    hair.transform.SetParent(frame, false); hair.transform.localPosition = manifest.hairPosition;
                    hair.transform.localEulerAngles = manifest.hairEulerAngles; hair.transform.localScale = manifest.hairScale;
                    evidence.hairFbxAsset = manifest.hairFbxAsset; evidence.hairSha256 = manifest.hairSha256;
                    evidence.hairTriangles = hair.GetComponentsInChildren<Renderer>(true).Sum(renderer => MeshOf(renderer).triangles.Length / 3);
                    if (evidence.hairTriangles != 45536) throw new InvalidDataException("Fitted V5 hair triangle count changed.");
                }
                foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
                var shaderLit = Shader.Find("LucidLoop/RenFaceStudy/Diffuse");
                var shaderUnlit = shaderLit;
                if (!shaderLit || !shaderUnlit) throw new InvalidOperationException("Required URP shaders unavailable.");
                var unlit = new List<Material>(); var lit = new List<Material>();
                var colorUnlit = new List<Material>(); var colorLit = new List<Material>();
                var slots = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var renderer in frame.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned = new Material[renderer.sharedMaterials.Length];
                    for (var i = 0; i < assigned.Length; i++)
                    {
                        var sourceMaterial = renderer.sharedMaterials[i];
                        if (!sourceMaterial) throw new InvalidDataException("An imported renderer has an empty material slot: " + renderer.name);
                        var isHair = hair && renderer.transform.IsChildOf(hair.transform);
                        var name = isHair ? "RenFaceStudyHairClay" : sourceMaterial.name;
                        if (!slots.TryGetValue(name, out var slot))
                        {
                            var color = isHair ? new RenFaceStudyMaterial { sourceName = name, baseColorSrgb = new Color(.60f, .62f, .66f, 1) } : manifest.materials.FirstOrDefault(material => material.sourceName == name);
                            if (color == null) throw new InvalidDataException("Missing explicit review color for source material " + name);
                            slot = slots.Count; slots.Add(name, slot);
                            var clayColor = name == "Ren_Draft_Skin" || name == "Ren_Draft_RoseLips" ? new Color(.60f, .62f, .66f, 1) : color.baseColorSrgb;
                            unlit.Add(Material(slot, false, shaderUnlit, clayColor, color.useVertexColor, false));
                            lit.Add(Material(slot, true, shaderLit, clayColor, color.useVertexColor, false));
                            colorUnlit.Add(Material(slot, false, shaderUnlit, color.baseColorSrgb, color.useVertexColor, true));
                            colorLit.Add(Material(slot, true, shaderLit, color.baseColorSrgb, color.useVertexColor, true));
                        }
                        assigned[i] = lit[slot];
                    }
                    renderer.sharedMaterials = assigned; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                evidence.shapes = InspectShapes(model);
                foreach (var name in new[] { "mouthSeal", "jawOpen_A", "eyeBlinkL", "eyeBlinkR", "gazeLeft", "gazeRight", "gazeUp", "gazeDown" })
                    if (!evidence.shapes.Any(shape => RenFaceStudyController.ShapeMatches(shape.shape, name) && shape.affectedVertices > 0))
                        throw new InvalidDataException("Required genuine facial shape absent: " + name);
                foreach (var name in new[] { "eyeBlinkL", "eyeBlinkR" })
                    if (!evidence.shapes.Any(shape => shape.renderer == "Ren_Head" && RenFaceStudyController.ShapeMatches(shape.shape, name) && shape.affectedVertices > 0))
                        throw new InvalidDataException("Required stitched head eyelid deformation absent: " + name);
                evidence.triangles = model.GetComponentsInChildren<Renderer>(true).Sum(renderer =>
                {
                    var mesh = MeshOf(renderer); if (!mesh) return 0;
                    return Enumerable.Range(0, mesh.subMeshCount).Sum(submesh => mesh.GetTopology(submesh) == MeshTopology.Triangles ?
                        (int)mesh.GetIndexCount(submesh) / 3 : mesh.GetTopology(submesh) == MeshTopology.Quads ? (int)mesh.GetIndexCount(submesh) / 2 : 0);
                });
                RenFaceStudyController.ApplyMouthTo(model, 1, 0); var bounds = BakedBounds(model);
                RenFaceStudyController.ApplyMouthTo(model, 0, 0); bounds.Encapsulate(BakedBounds(model));
                RenFaceStudyController.ApplyMouthTo(model, 0, 1); bounds.Encapsulate(BakedBounds(model));
                if (hair) bounds.Encapsulate(BakedBounds(hair));
                if (bounds.size.y <= .0001f) throw new InvalidDataException("Source face has invalid bounds.");
                evidence.unionBoundsSize = bounds.size;
                evidence.normalizationScale = 1.65f / bounds.size.y;
                evidence.normalizationOffset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * evidence.normalizationScale;
                frame.localScale = Vector3.one * evidence.normalizationScale; frame.localPosition = evidence.normalizationOffset;
                RenFaceStudyController.ApplyMouthTo(model, 1, 0);
                if (hair) hair.SetActive(false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetRoot + "/Viewer/RenFaceStudy-LocalFace.prefab");
                var clay = Candidate("ren-local-face-clay", "Neutral clay / face repair", prefab, unlit, lit, evidence.triangles);
                foreach (var renderer in frame.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material => colorLit[lit.IndexOf(material)]).ToArray();
                var colorPrefab = PrefabUtility.SaveAsPrefabAsset(root, AssetRoot + "/Viewer/RenFaceStudy-TemporaryColor.prefab");
                var colors = Candidate("ren-local-face-colors", "Temporary colors / unpainted", colorPrefab, colorUnlit, colorLit, evidence.triangles);
                return new[] { clay, colors };
            }
            finally { Object.DestroyImmediate(root); }
        }

        static ShapeEvidence[] InspectShapes(GameObject root)
        {
            var result = new List<ShapeEvidence>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh; if (!mesh) continue;
                var delta = new Vector3[mesh.vertexCount];
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var frames = mesh.GetBlendShapeFrameCount(i); var frame = frames - 1;
                    if (frame < 0) continue;
                    mesh.GetBlendShapeFrameVertices(i, frame, delta, null, null);
                    result.Add(new ShapeEvidence { renderer = renderer.name, shape = mesh.GetBlendShapeName(i), frameCount = frames,
                        lastFrameWeight = mesh.GetBlendShapeFrameWeight(i, frame), affectedVertices = delta.Count(value => value.sqrMagnitude > 1e-14f),
                        maxLocalDelta = delta.Max(value => value.magnitude) });
                }
            }
            return result.ToArray();
        }

        static Bounds BakedBounds(GameObject root)
        {
            var bounds = new Bounds(); var hasBounds = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = MeshOf(renderer); if (!mesh) continue;
                Mesh baked = null;
                try
                {
                    if (renderer is SkinnedMeshRenderer skin) { baked = new Mesh(); skin.BakeMesh(baked); mesh = baked; }
                    foreach (var point in mesh.vertices)
                    {
                        var world = renderer.transform.TransformPoint(point);
                        if (!hasBounds) { bounds = new Bounds(world, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(world);
                    }
                }
                finally { if (baked) Object.DestroyImmediate(baked); }
            }
            if (!hasBounds) throw new InvalidDataException("Face source contains no vertices.");
            return bounds;
        }

        static Mesh MeshOf(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;

        static RenBustCandidate Candidate(string id, string label, GameObject prefab, List<Material> unlit, List<Material> lit, int triangles) =>
            new RenBustCandidate { Id = id, Label = label, Provider = "Local construction", ModelLabel = "P2 source + authored repairs",
                Pose = "Local facial controls study", TextureSummary = "Unpainted review materials",
                Notes = "Independent mouth, stitched blink and iris gaze; optional unpainted fitted hair. Final art and full speech remain pending.",
                TriangleCount = triangles, Prefab = prefab, BaseColorMaterials = unlit.ToArray(), LitMaterials = lit.ToArray(), SourceNormalLitMaterials = lit.ToArray() };

        static Material Material(int index, bool lit, Shader shader, Color color, bool vertexColor, bool temporary)
        {
            var path = AssetRoot + "/Viewer/RenFaceStudy-" + (temporary ? "Color-" : "Clay-") + index + (lit ? "-lit.mat" : "-unlit.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetVector("_LinearBaseColor", color.linear);
            material.SetFloat("_UseVertexColor", vertexColor ? 1 : 0); material.SetFloat("_Unlit", lit ? 0 : 1);
            EditorUtility.SetDirty(material); return material;
        }

        static RenBustComparisonViewer CreateStage()
        {
            var viewer = new GameObject("Ren Face Study").AddComponent<RenBustComparisonViewer>();
            viewer.Title = "Ren / repaired face / geometry study";
            var used = Resources.FindObjectsOfTypeAll<Renderer>().Where(renderer => renderer.gameObject.scene.IsValid())
                .Select(renderer => renderer.gameObject.layer).ToHashSet();
            var layers = Enumerable.Range(24, 8).Reverse().Where(layer => !used.Contains(layer)).Take(2).ToArray();
            if (layers.Length != 2) throw new InvalidOperationException("Two unused layers are needed for the face study.");
            viewer.LeftLayer = layers[0]; viewer.RightLayer = layers[1];
            viewer.LeftStage = Child(viewer.transform, "Left stage", new Vector3(-3, 0, 0));
            viewer.RightStage = Child(viewer.transform, "Right stage", new Vector3(3, 0, 0));
            viewer.LeftCamera = Camera(viewer.transform, "Closed-rest camera", 1 << layers[0], 0);
            viewer.RightCamera = Camera(viewer.transform, "Interactive mouth camera", 1 << layers[1], 1);
            var background = Camera(viewer.transform, "Background camera", 0, -100);
            background.tag = "MainCamera"; background.backgroundColor = new Color(.055f, .064f, .082f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.fog = false; RenderSettings.skybox = null;
            var mask = (1 << layers[0]) | (1 << layers[1]);
            var neutral = new List<Light> { Light(viewer.transform, "Neutral front key", LightType.Directional, Vector3.zero,
                new Vector3(25, 155, 0), Color.white, .9f, mask) };
            var club = new List<Light> { Light(viewer.transform, "Club front key", LightType.Directional, Vector3.zero,
                new Vector3(20, 165, 0), new Color(.88f, .92f, 1), .55f, mask) };
            foreach (var stage in new[] { viewer.LeftStage, viewer.RightStage })
            {
                var stageMask = 1 << (stage == viewer.LeftStage ? layers[0] : layers[1]);
                neutral.Add(Light(stage, "Neutral fill", LightType.Point, new Vector3(-1.3f, 1.1f, 1.8f), Vector3.zero, Color.white, 1.5f, stageMask));
                neutral.Add(Light(stage, "Neutral rim", LightType.Point, new Vector3(.8f, 1.5f, -.7f), Vector3.zero, new Color(.83f, .9f, 1), .6f, stageMask));
                club.Add(Light(stage, "Club magenta", LightType.Point, new Vector3(-1.1f, 1.25f, .8f), Vector3.zero, new Color(1, .08f, .42f), 1, stageMask));
                club.Add(Light(stage, "Club cyan", LightType.Point, new Vector3(1.1f, 1.3f, -.1f), Vector3.zero, new Color(.06f, .75f, 1), 1.4f, stageMask));
            }
            viewer.NeutralLights = neutral.ToArray(); viewer.NightclubLights = club.ToArray(); return viewer;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child;
        }
        static Camera Camera(Transform parent, string name, int mask, int depth)
        {
            var camera = Child(parent, name, Vector3.zero).gameObject.AddComponent<Camera>();
            camera.cullingMask = mask; camera.depth = depth; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .01f; camera.farClipPlane = 20; camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; return camera;
        }
        static Light Light(Transform parent, string name, LightType type, Vector3 position, Vector3 euler, Color color, float intensity, int mask)
        {
            var light = Child(parent, name, position).gameObject.AddComponent<Light>();
            light.transform.localEulerAngles = euler; light.type = type; light.color = color;
            light.intensity = intensity; light.cullingMask = mask; light.range = 4; light.shadows = LightShadows.None; return light;
        }

        static CaptureEvidence[] CapturePoses(RenBustComparisonViewer viewer, RenFaceStudyController controller)
        {
            var output = Path.GetFullPath(Argument("-renFaceOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-face-study-captures"));
            Directory.CreateDirectory(output); var result = new List<CaptureEvidence>();
            foreach (var pose in new[] { "closed-rest", "open-A", "blink-full", "combined-full", "combined-half",
                "blink-left", "blink-right", "gaze-left-up", "gaze-right-down", "hair-closed", "hair-combined" })
            {
                controller.ResetAll(); controller.ShowHair = pose.StartsWith("hair", StringComparison.Ordinal);
                var open = pose == "open-A" || pose.StartsWith("combined", StringComparison.Ordinal) || pose == "hair-combined";
                var full = pose == "blink-full" || pose == "combined-full" || pose == "hair-combined";
                var half = pose == "combined-half";
                var gaze = pose == "gaze-left-up" ? new Vector2(-1, 1) : pose == "gaze-right-down" || pose == "combined-full" || pose == "hair-combined" ? new Vector2(1, -1) : half ? new Vector2(-.5f, .5f) : Vector2.zero;
                controller.SetFace(open ? 0 : 1, open ? 1 : 0, full || pose == "blink-left" ? 1 : half ? .5f : 0, full || pose == "blink-right" ? 1 : half ? .5f : 0, gaze);
                var single = pose == "blink-left" || pose == "blink-right" || pose.StartsWith("gaze", StringComparison.Ordinal);
                foreach (var mode in pose == "closed-rest" || pose == "combined-full" || pose == "hair-combined" ? new[] { 1, 2 } : new[] { 1 })
                    foreach (var yaw in single ? new[] { 0f } : mode == 2 ? new[] { 0f, 45f } : new[] { 0f, 45f, -45f })
                    {
                        viewer.SetLighting(mode); viewer.SetView(yaw); controller.ApplyNow();
                        var record = new CaptureEvidence { mouthSeal = controller.MouthSeal, jawOpenA = controller.JawOpenA,
                            blinkLeft = controller.EffectiveBlinkLeft, blinkRight = controller.EffectiveBlinkRight,
                            gazeX = controller.Gaze.x, gazeY = controller.Gaze.y, hair = controller.ShowHair,
                            lighting = mode == 1 ? "neutral" : "club", view = yaw == 0 ? "front" : yaw == 45 ? "three-quarter" : "other-quarter",
                            poseGeometrySha256 = PoseHash(viewer.RightInstance) };
                        record.file = "RenFaceStudy--clay--" + pose + "--" + record.lighting + "--" + record.view + ".png";
                        Capture(viewer.RightCamera, Path.Combine(output, record.file)); result.Add(record);
                    }
            }
            if (result.Select(record => record.poseGeometrySha256).Distinct().Count() < 9)
                throw new InvalidDataException("The independent facial poses did not produce nine distinct actual deformations.");
            viewer.SelectCandidate(1, false); controller.ResetAll(); controller.ShowHair = false;
            viewer.SetLighting(1); viewer.SetView(0); controller.ApplyNow();
            var color = new CaptureEvidence { mouthSeal = 1, view = "front", materialStudy = "temporary-colors-unpainted",
                lighting = "neutral", file = "RenFaceStudy--temporary-colors--closed-rest--front.png", poseGeometrySha256 = PoseHash(viewer.RightInstance) };
            Capture(viewer.RightCamera, Path.Combine(output, color.file)); result.Add(color);
            viewer.SelectCandidate(0, false); controller.ResetAll(); return result.ToArray();
        }

        static void CaptureTextureTrial(RenBustComparisonViewer viewer, RenFaceStudyController controller, RenFaceStudyTextureTrial trial)
        {
            var output = Path.GetFullPath(Argument("-renFaceOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-face-texture-captures"));
            Directory.CreateDirectory(output); controller.ResetAll(); controller.ShowHair = false;
            viewer.SelectCandidate(2, false); viewer.SetSourceNormalMaps(false); controller.ApplyNow();
            foreach (var mode in new[] { 0, 1, 2 })
                foreach (var yaw in new[] { 0f, 45f, -45f, 90f })
                {
                    viewer.SetLighting(mode); viewer.SetView(yaw);
                    var lighting = mode == 0 ? "basecolor" : mode == 1 ? "neutral" : "club";
                    var view = yaw == 0 ? "front" : yaw == 45 ? "three-quarter" : yaw == -45 ? "other-quarter" : "profile";
                    Capture(viewer.RightCamera, Path.Combine(output, "RenFaceStudy--tripo-static--" + lighting + "--" + view + ".png"));
                    Capture(viewer.LeftCamera, Path.Combine(output, "RenFaceStudy--clay-rest--" + lighting + "--" + view + ".png"));
                }
            File.WriteAllText(Path.Combine(output, "RenFaceStudyTextureManifest.json"), JsonUtility.ToJson(trial, true));
            Debug.Log("REN_FACE_TEXTURE_CAPTURE_OK: " + output);
        }

        static void Capture(Camera camera, string path)
        {
            const int size = 1536;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var active = RenderTexture.active; var rect = camera.rect; var aspect = camera.aspect;
            var image = new Texture2D(size, size, TextureFormat.RGB24, false, false);
            var snapshots = new List<GameObject>(); var meshes = new List<Mesh>(); var disabled = new List<SkinnedMeshRenderer>();
            try
            {
                foreach (var skin in camera.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy || (camera.cullingMask & (1 << skin.gameObject.layer)) == 0) continue;
                    var baked = new Mesh(); skin.BakeMesh(baked, false); meshes.Add(baked);
                    if (baked.colors.Length == 0 && skin.sharedMesh.colors.Length > 0) baked.colors = skin.sharedMesh.colors;
                    var snapshot = new GameObject("RenFaceStudyCaptureSnapshot"); snapshots.Add(snapshot);
                    snapshot.transform.SetParent(skin.transform, false); snapshot.layer = skin.gameObject.layer;
                    snapshot.AddComponent<MeshFilter>().sharedMesh = baked;
                    var renderer = snapshot.AddComponent<MeshRenderer>(); renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    disabled.Add(skin); skin.enabled = false;
                }
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1;
                target.Create(); RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active;
                foreach (var skin in disabled) if (skin) skin.enabled = true;
                foreach (var snapshot in snapshots) Object.DestroyImmediate(snapshot);
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        static string PoseHash(GameObject root)
        {
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(renderer => renderer.name, StringComparer.Ordinal))
                    {
                        var mesh = new Mesh();
                        try
                        {
                            skin.BakeMesh(mesh, false); writer.Write(skin.name);
                            foreach (var point in mesh.vertices) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                        }
                        finally { Object.DestroyImmediate(mesh); }
                    }
                memory.Position = 0;
                using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(memory)).Replace("-", "").ToLowerInvariant();
            }
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidDataException("Missing imported asset " + path); return asset;
        }
        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/'); if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path)); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
