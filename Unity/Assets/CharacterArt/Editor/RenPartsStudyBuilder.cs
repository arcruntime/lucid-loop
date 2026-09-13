using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    [Serializable] public sealed class RenPartsStudyManifest
    {
        public int schemaVersion = 1;
        public bool placementReviewed;
        public string placementReviewNote, originalArtistAsset, coordinateMapping;
        public RenPartsStudyPart head, hair;
        public RenBustManifestReference[] additionalReferences = Array.Empty<RenBustManifestReference>();
        public string[] existingCandidateIds = Array.Empty<string>();
    }

    [Serializable] public sealed class RenPartsStudyPart
    {
        public string fbxAsset, modelLabel, notes;
        public Vector3 position, eulerAngles;
        public Vector3 scale = Vector3.one;
    }

    /// <summary>Read-only source-part placement plus new review assets; never rebuilds old bust candidates.</summary>
    public static class RenPartsStudyBuilder
    {
        public const string AssetRoot = "Assets/CharacterArt/Generated/RenPartsStudy";
        public const string ManifestPath = AssetRoot + "/RenPartsStudyManifest.json";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenPartsStudy.unity";
        const string HeadId = "parts-head-open-source", AssemblyId = "parts-assembled-open-source";

        [Serializable] sealed class PartEvidence
        {
            public string role, asset, modelLabel;
            public Vector3 localPosition, localEulerAngles, localScale;
            public int vertices, triangles, quads, blendShapes;
        }
        [Serializable] sealed class StudyEvidence
        {
            public string unityVersion, manifest, placementReviewNote, coordinateMapping;
            public string status = "Untextured construction geometry with a generated open mouth. Neither a neutral face nor working facial controls. Unity imported topology counts do not certify the native FBX author's topology.";
            public string framing = "The placed head-and-hair union determines one normalization transform shared identically by head-only and assembled views. Hair is included in the common frame without changing the relative head scale or position between views. Existing comparison sources retain their original review normalization.";
            public string materials = "New parts use uniform clay materials and imported geometry normals. Base color/unlit is a silhouette diagnostic; Neutral lighting is diffuse clay; Nightclub lighting is the same clay under colored lights. No source textures, normal maps, or generated PBR maps are bound to the new parts.";
            public string lighting = "Neutral front-facing directional key: Euler(25,155,0), intensity0.9; per-stage white point fill1.5 and cool rim0.6. Club front-facing cool key: Euler(20,165,0), intensity0.55; magenta point1 and cyan point1.4. Head clay RGBA(0.52,0.55,0.60,1), hair(0.32,0.35,0.40,1). Diffuse-only, no shadow casting, no HDR or postprocessing.";
            public float normalizationScale;
            public Vector3 normalizationOffset, placedHeadBounds, placedAssemblyBounds;
            public PartEvidence[] parts;
            public string[] renders;
        }

        [MenuItem("Lucid Loop/Character Art/Build Ren Parts Study")]
        public static void Build() => Run(false);

        [MenuItem("Lucid Loop/Character Art/Build and Capture Ren Parts Study")]
        public static void BuildAndCapture() => Run(true);

        public static void ValidateManifest(RenPartsStudyManifest manifest)
        {
            if (manifest == null || manifest.schemaVersion != 1) throw new InvalidDataException("Expected parts-study schemaVersion 1.");
            if (!manifest.placementReviewed || string.IsNullOrWhiteSpace(manifest.placementReviewNote))
                throw new InvalidDataException("Review real head/hair placement and record it before building the parts study.");
            foreach (var part in new[] { manifest.head, manifest.hair })
            {
                if (part == null) throw new InvalidDataException("Both real source parts are required.");
                ValidatePath(part.fbxAsset);
                if (!part.fbxAsset.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || !File.Exists(part.fbxAsset))
                    throw new InvalidDataException("Missing actual source FBX: " + part.fbxAsset);
                if (string.IsNullOrWhiteSpace(part.modelLabel)) throw new InvalidDataException("Record the source model label for both parts.");
                foreach (var value in new[] { part.position, part.eulerAngles, part.scale })
                    if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z)) throw new InvalidDataException("Placement contains a non-finite value.");
                if (part.scale.x <= 0 || part.scale.y <= 0 || part.scale.z <= 0)
                    throw new InvalidDataException("Source-part scales must be positive; do not hide a reflection in placement.");
            }
            ValidatePath(manifest.originalArtistAsset);
            if (!File.Exists(manifest.originalArtistAsset)) throw new FileNotFoundException("Original artist reference missing.", manifest.originalArtistAsset);
            foreach (var reference in manifest.additionalReferences ?? Array.Empty<RenBustManifestReference>())
            {
                ValidatePath(reference.textureAsset);
                if (!File.Exists(reference.textureAsset)) throw new FileNotFoundException("Additional reference missing.", reference.textureAsset);
            }
            foreach (var id in manifest.existingCandidateIds ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                    throw new InvalidDataException("Invalid existing candidate ID.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains('\\'))
                throw new InvalidDataException("Expected an explicit Assets/... path: " + path);
        }

        static void Run(bool capture)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build outside Play mode.");
            var path = Argument("-renPartsManifest") ?? ManifestPath;
            ValidatePath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Provide the reviewed real-parts manifest first.", path);
            var manifest = JsonUtility.FromJson<RenPartsStudyManifest>(File.ReadAllText(path));
            ValidateManifest(manifest);
            // Validation deliberately precedes all scene creation and output writes.
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            var otherLights = Resources.FindObjectsOfTypeAll<Light>()
                .Where(light => light.gameObject.scene.IsValid() && light.gameObject.scene != scene && light.enabled).ToArray();
            try
            {
                foreach (var light in otherLights) light.enabled = false;
                SceneManager.SetActiveScene(scene);
                EnsureFolder(AssetRoot + "/Viewer");
                var evidence = new StudyEvidence { unityVersion = Application.unityVersion, manifest = path,
                    placementReviewNote = manifest.placementReviewNote, coordinateMapping = manifest.coordinateMapping };
                var viewer = CreateStage();
                var candidates = CreateParts(manifest, evidence).ToList();
                candidates.AddRange(ExistingCandidates(manifest.existingCandidateIds));
                viewer.Candidates = candidates.ToArray();
                viewer.References = new[] { new RenBustReference { Label = "Original artist design", Texture = Load<Texture2D>(manifest.originalArtistAsset) } }
                    .Concat((manifest.additionalReferences ?? Array.Empty<RenBustManifestReference>()).Select(reference =>
                        new RenBustReference { Label = reference.label, Texture = LoadReference(reference.textureAsset) })).ToArray();
                viewer.SelectById(HeadId, true); viewer.SelectById(AssemblyId, false);
                viewer.ShowReference = true; viewer.ReferenceIndex = 0;
                viewer.SetLighting(1); viewer.SetView(0); viewer.UpdateLayout();
                if (capture) evidence.renders = CaptureParts(viewer);
                viewer.SelectById(HeadId, true); viewer.SelectById(AssemblyId, false);
                viewer.SetLighting(1); viewer.SetView(0); viewer.UpdateLayout();
                EnsureFolder(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                File.WriteAllText(AssetRoot + "/RenPartsStudyEvidence.json", JsonUtility.ToJson(evidence, true));
                AssetDatabase.SaveAssets();
                Debug.Log("REN_PARTS_STUDY_BUILD_OK: " + ScenePath);
            }
            finally
            {
                foreach (var light in otherLights) if (light) light.enabled = true;
                if (!Application.isBatchMode || SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static RenBustCandidate[] CreateParts(RenPartsStudyManifest manifest, StudyEvidence evidence)
        {
            var headAsset = Load<GameObject>(manifest.head.fbxAsset);
            var hairAsset = Load<GameObject>(manifest.hair.fbxAsset);
            var roots = new List<GameObject>();
            try
            {
                var headRoot = new GameObject("RenPartsStudy-HeadOnly"); roots.Add(headRoot);
                var assemblyRoot = new GameObject("RenPartsStudy-Assembly"); roots.Add(assemblyRoot);
                var headFrame = Child(headRoot.transform, "Shared normalization", Vector3.zero);
                var assemblyFrame = Child(assemblyRoot.transform, "Shared normalization", Vector3.zero);
                var head = Place(headAsset, headFrame, manifest.head);
                var assemblyHead = Place(headAsset, assemblyFrame, manifest.head);
                var hair = Place(hairAsset, assemblyFrame, manifest.hair);
                var headBounds = BoundsOf(head);
                var bounds = BoundsOf(assemblyRoot);
                if (bounds.size.y < .00001f) throw new InvalidDataException("Source head has degenerate bounds.");
                var scale = 1.65f / bounds.size.y;
                var offset = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                evidence.normalizationScale = scale; evidence.normalizationOffset = offset;
                evidence.placedHeadBounds = headBounds.size; evidence.placedAssemblyBounds = bounds.size;
                evidence.parts = new[] { Inspect("head", head, manifest.head), Inspect("hair", hair, manifest.hair) };
                foreach (var frame in new[] { headFrame, assemblyFrame })
                { frame.localScale = Vector3.one * scale; frame.localPosition = offset; }
                var unlit = new[] { Clay("Head", false, new Vector4(.52f, .55f, .60f, 1)), Clay("Hair", false, new Vector4(.32f, .35f, .40f, 1)) };
                var lit = new[] { Clay("Head", true, new Vector4(.52f, .55f, .60f, 1)), Clay("Hair", true, new Vector4(.32f, .35f, .40f, 1)) };
                Bind(head, unlit[0]); Bind(assemblyHead, unlit[0]); Bind(hair, unlit[1]);
                var headPrefab = PrefabUtility.SaveAsPrefabAsset(headRoot, AssetRoot + "/Viewer/RenPartsStudy-HeadOnly.prefab");
                var assemblyPrefab = PrefabUtility.SaveAsPrefabAsset(assemblyRoot, AssetRoot + "/Viewer/RenPartsStudy-Assembly.prefab");
                var headTriangles = evidence.parts[0].triangles + evidence.parts[0].quads * 2;
                var hairTriangles = evidence.parts[1].triangles + evidence.parts[1].quads * 2;
                return new[]
                {
                    Candidate(HeadId, "Head only / open source", manifest.head.modelLabel, headPrefab, unlit, lit, headTriangles,
                        "Generated open-mouth construction head. Untextured clay; no neutral face or working speech controls. " + manifest.head.notes),
                    Candidate(AssemblyId, "Head + hair / open source", manifest.head.modelLabel + " + separate hair", assemblyPrefab, unlit, lit, headTriangles + hairTriangles,
                        "Same head scale and position; separate hair placed from reviewed transforms. Untextured construction geometry. " + manifest.hair.notes)
                };
            }
            finally { foreach (var root in roots) Object.DestroyImmediate(root); }
        }

        static RenBustCandidate Candidate(string id, string label, string model, GameObject prefab, Material[] unlit, Material[] lit, int triangles, string notes)
            => new RenBustCandidate { Id = id, Label = label, Provider = "Tripo", ModelLabel = model, Pose = "Construction / open mouth",
                TextureSummary = "Untextured clay", Notes = notes, TriangleCount = triangles, Prefab = prefab,
                BaseColorMaterials = unlit, LitMaterials = lit, SourceNormalLitMaterials = lit };

        static GameObject Place(GameObject asset, Transform parent, RenPartsStudyPart part)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = part.position;
            instance.transform.localEulerAngles = part.eulerAngles;
            instance.transform.localScale = part.scale;
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
            return instance;
        }

        static PartEvidence Inspect(string role, GameObject part, RenPartsStudyPart placement)
        {
            var result = new PartEvidence { role = role, asset = placement.fbxAsset, modelLabel = placement.modelLabel,
                localPosition = placement.position, localEulerAngles = placement.eulerAngles, localScale = placement.scale };
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>(); var skin = renderer as SkinnedMeshRenderer;
                var mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                if (!mesh) continue;
                result.vertices += mesh.vertexCount; result.blendShapes += mesh.blendShapeCount;
                for (var i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) result.triangles += (int)mesh.GetIndexCount(i) / 3;
                    else if (mesh.GetTopology(i) == MeshTopology.Quads) result.quads += (int)mesh.GetIndexCount(i) / 4;
            }
            return result;
        }

        static Bounds BoundsOf(GameObject part)
        {
            var renderers = part.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Source part contains no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void Bind(GameObject part, Material material)
        {
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
        }

        static Material Clay(string part, bool lit, Vector4 color)
        {
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
            if (!shader) throw new InvalidOperationException("Required URP shader unavailable.");
            var path = AssetRoot + "/Viewer/RenPartsStudy-" + part + (lit ? "-ClayLit.mat" : "-ClayUnlit.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetVector("_BaseColor", color); material.SetTexture("_BaseMap", null);
            material.SetFloat("_Cull", (float)CullMode.Off); material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
            if (lit)
            {
                material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", 0);
                material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_EnvironmentReflections", 0); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.SetTexture("_BumpMap", null); material.DisableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material); return material;
        }

        static IEnumerable<RenBustCandidate> ExistingCandidates(string[] ids)
        {
            if (ids == null || ids.Length == 0) yield break;
            var manifest = JsonUtility.FromJson<RenBustManifest>(File.ReadAllText(RenBustComparisonBuilder.ManifestPath));
            foreach (var id in ids.Distinct())
            {
                var entry = manifest.entries.FirstOrDefault(candidate => candidate.id == id);
                if (entry == null) throw new InvalidDataException("Unknown existing comparison candidate " + id);
                var prefix = RenBustComparisonBuilder.AssetRoot + "/Viewer/" + id;
                var unlit = new List<Material>(); var lit = new List<Material>(); var normals = new List<Material>();
                for (var i = 0; i < entry.materials.Length; i++)
                {
                    unlit.Add(Load<Material>(prefix + "-" + i + "-basecolor.mat"));
                    lit.Add(Load<Material>(prefix + "-" + i + "-lit.mat"));
                    normals.Add(Load<Material>(prefix + "-" + i + "-lit-source-normals.mat"));
                }
                var prefab = Load<GameObject>(prefix + ".prefab");
                var actual = Inspect("existing source", prefab, new RenPartsStudyPart { fbxAsset = entry.fbxAsset, modelLabel = entry.modelLabel });
                yield return new RenBustCandidate { Id = entry.id, Label = entry.label, Provider = entry.provider, ModelLabel = entry.modelLabel,
                    Pose = entry.pose, TextureSummary = entry.textureSummary, Notes = "Earlier textured source; retains its original review normalization. " + entry.notes,
                    TriangleCount = actual.triangles + actual.quads * 2, Prefab = prefab, BaseColorMaterials = unlit.ToArray(), LitMaterials = lit.ToArray(), SourceNormalLitMaterials = normals.ToArray() };
            }
        }

        static RenBustComparisonViewer CreateStage()
        {
            var viewer = new GameObject("Ren Parts Study").AddComponent<RenBustComparisonViewer>();
            viewer.Title = "Ren / untextured parts / open-mouth construction";
            var used = Resources.FindObjectsOfTypeAll<Renderer>().Where(renderer => renderer.gameObject.scene.IsValid())
                .Select(renderer => renderer.gameObject.layer).ToHashSet();
            var layers = Enumerable.Range(24, 8).Reverse().Where(layer => !used.Contains(layer)).Take(2).ToArray();
            if (layers.Length != 2) throw new InvalidOperationException("Two unused layers are needed for the parts study.");
            viewer.LeftLayer = layers[0]; viewer.RightLayer = layers[1];
            viewer.LeftStage = Child(viewer.transform, "Left stage", new Vector3(-3, 0, 0));
            viewer.RightStage = Child(viewer.transform, "Right stage", new Vector3(3, 0, 0));
            viewer.LeftCamera = Camera(viewer.transform, "Left parts camera", 1 << layers[0], 0);
            viewer.RightCamera = Camera(viewer.transform, "Right parts camera", 1 << layers[1], 1);
            var background = Camera(viewer.transform, "Background camera", 0, -100);
            background.tag = "MainCamera"; background.backgroundColor = new Color(.055f, .064f, .082f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.fog = false; RenderSettings.skybox = null;
            var mask = (1 << layers[0]) | (1 << layers[1]);
            // The reviewed source faces +Z; directional light rays must travel toward -Z to light the face.
            var neutral = new List<Light> { Light(viewer.transform, "Neutral key", LightType.Directional, Vector3.zero,
                new Vector3(25, 155, 0), Color.white, .9f, mask) };
            var club = new List<Light> { Light(viewer.transform, "Club soft key", LightType.Directional, Vector3.zero,
                new Vector3(20, 165, 0), new Color(.88f, .92f, 1), .55f, mask) };
            foreach (var stage in new[] { viewer.LeftStage, viewer.RightStage })
            {
                var stageMask = 1 << (stage == viewer.LeftStage ? layers[0] : layers[1]);
                neutral.Add(Light(stage, "Neutral fill", LightType.Point, new Vector3(-1.3f, 1.1f, 1.8f), Vector3.zero, Color.white, 1.5f, stageMask));
                neutral.Add(Light(stage, "Neutral rim", LightType.Point, new Vector3(.8f, 1.5f, -.7f), Vector3.zero, new Color(.83f, .9f, 1), .6f, stageMask));
                club.Add(Light(stage, "Club magenta", LightType.Point, new Vector3(-1.1f, 1.25f, .8f), Vector3.zero, new Color(1, .08f, .42f), 1, stageMask));
                club.Add(Light(stage, "Club cyan", LightType.Point, new Vector3(1.1f, 1.3f, -.1f), Vector3.zero, new Color(.06f, .75f, 1), 1.4f, stageMask));
            }
            viewer.NeutralLights = neutral.ToArray(); viewer.NightclubLights = club.ToArray();
            return viewer;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position; return child;
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

        static string[] CaptureParts(RenBustComparisonViewer viewer)
        {
            var output = Path.GetFullPath(Argument("-renPartsOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-parts-study-captures"));
            Directory.CreateDirectory(output); var files = new List<string>();
            foreach (var id in new[] { HeadId, AssemblyId })
            {
                viewer.SelectById(id, true);
                for (var mode = 0; mode < 3; mode++)
                    foreach (var yaw in new[] { 0f, 45f, 90f })
                    {
                        viewer.SetLighting(mode); viewer.SetView(yaw);
                        var file = id + "--" + new[] { "clay-unlit", "clay-neutral", "clay-club" }[mode] + "--" +
                            (yaw == 0 ? "front" : yaw == 45 ? "three-quarter" : "profile") + ".png";
                        Capture(viewer.LeftCamera, Path.Combine(output, file)); files.Add(file);
                    }
            }
            return files.ToArray();
        }

        static void Capture(Camera camera, string path)
        {
            const int size = 1536;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var active = RenderTexture.active; var rect = camera.rect; var aspect = camera.aspect;
            var image = new Texture2D(size, size, TextureFormat.RGB24, false, false);
            try
            {
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1;
                target.Create(); RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.rect = rect; camera.aspect = aspect; RenderTexture.active = active;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        public static void BuildPlayerFromSavedScene()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Build the reviewed parts-study scene first.", ScenePath);
            var output = Path.GetFullPath(Argument("-renPartsPlayerOutput") ?? Path.Combine(Application.dataPath, "../../.local/ren-parts-study/RenPartsStudy.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Parts-study player build failed: " + report.summary.result);
            Debug.Log("REN_PARTS_STUDY_PLAYER_OK: " + output);
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidDataException("Missing imported asset " + path);
            return asset;
        }

        static Texture2D LoadReference(string path)
        {
            // Only new study copies receive an explicit import policy; earlier comparison sources stay untouched.
            if (path.StartsWith(AssetRoot + "/References/", StringComparison.Ordinal) && AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 8192;
                importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear; importer.SaveAndReimport();
            }
            return Load<Texture2D>(path);
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
