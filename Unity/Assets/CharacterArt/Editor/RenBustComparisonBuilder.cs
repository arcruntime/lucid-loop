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
    [Serializable] public sealed class RenBustManifest
    {
        public int schemaVersion = 1;
        public string title;
        public RenBustManifestEntry[] entries = Array.Empty<RenBustManifestEntry>();
        public RenBustManifestReference[] references = Array.Empty<RenBustManifestReference>();
        public RenBustManifestDefaults defaults;
    }
    [Serializable] public sealed class RenBustManifestDefaults { public string leftId, rightId; }
    [Serializable] public sealed class RenBustManifestReference { public string label, textureAsset; }
    [Serializable] public sealed class RenBustManifestEntry
    {
        public string id, label, provider, modelLabel, pose, fbxAsset, textureSummary, notes;
        public int triangleCount;
        public float frontYaw;
        public RenBustManifestMaterial[] materials = Array.Empty<RenBustManifestMaterial>();
    }
    [Serializable] public sealed class RenBustManifestMaterial
    {
        public string sourceName, baseColorAsset, normalAsset, metallicAsset, roughnessAsset;
        public float normalScale = 1f;
        public RenBustColorFactor baseColorFactor;
    }
    [Serializable] public sealed class RenBustColorFactor { public float r = 1, g = 1, b = 1, a = 1; }

    /// <summary>Builds an isolated source comparison without replacing the existing Ren character scene.</summary>
    public static class RenBustComparisonBuilder
    {
        public const string AssetRoot = "Assets/CharacterArt/Generated/BustComparison";
        public const string ManifestPath = AssetRoot + "/bust-comparison.json";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenBustComparison.unity";

        [Serializable] sealed class TextureEvidence
        {
            public string asset, format, type, filtering;
            public int width, height, mipCount;
            public bool sRGB;
        }
        [Serializable] sealed class CandidateEvidence
        {
            public string id, fbxAsset;
            public int importedTriangles, sourceReportedTriangles;
            public float frontYaw, normalizationScale;
            public Vector3 sourceBoundsSize;
            public TextureEvidence[] textures;
        }
        [Serializable] sealed class CaptureEvidence
        {
            public string unityVersion, manifest, scene;
            public string projection = "Orthographic; all busts normalized to 1.65 units overall bounds height; target at (0, 0.825, 0) relative to each stage; ortho size 0.98.";
            public string materials = "Base color is URP/Unlit. Lit diagnostics use diffuse URP/Lit with supplied base color, metallic 0, smoothness 0, specular highlights and environment reflections disabled, double sided. Default captures retain geometry normals and disable supplied normal maps. Metallic/roughness source maps are retained but not assumed to be URP packed maps.";
            public string lighting = "Neutral: flat ambient 0.58; white directional key intensity 0.65 at Euler (20,-20,0); per-stage white point fill intensity 1.5 and cool rim intensity 0.6. No shadows or post-processing.";
            public string nightclubLighting = "Club: flat ambient RGB (0.30,0.28,0.34); gently cool directional key RGB (0.88,0.92,1), intensity 0.55 at Euler (20,-15,0); per-stage magenta point intensity 1 and cyan point intensity 1.4. Diffuse response, no specular highlights, shadows, post-processing, or tone mapping.";
            public bool defaultSourceNormalMaps = false;
            public bool additionalSourceNormalDiagnostics;
            public string limitations = "Static generated source geometry. No facial-control or mobile-performance claim. Source texture import is uncompressed for desktop review.";
            public CandidateEvidence[] candidates;
            public string[] renders;
        }

        [MenuItem("Lucid Loop/Character Art/Build Ren Bust Comparison")]
        public static void Build() => Run(false);

        [MenuItem("Lucid Loop/Character Art/Build and Capture Ren Bust Comparison")]
        public static void BuildAndCapture() => Run(true);

        static void Run(bool capture)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the Ren bust comparison outside Play mode.");
            var manifestPath = ReadArgument("-renBustManifest") ?? ManifestPath;
            ValidateAssetPath(manifestPath);
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Ren bust comparison manifest missing", manifestPath);
            var manifest = JsonUtility.FromJson<RenBustManifest>(File.ReadAllText(manifestPath));
            ValidateManifest(manifest);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            // Other open scenes can contain broad-mask lights. Suppress only those lights during this
            // additive build/capture, then restore their exact enabled state before returning control.
            var otherLights = Resources.FindObjectsOfTypeAll<Light>()
                .Where(light => light.gameObject.scene.IsValid() && light.gameObject.scene != scene && light.enabled).ToArray();
            try
            {
                foreach (var light in otherLights) light.enabled = false;
                SceneManager.SetActiveScene(scene);
                var evidence = new List<CandidateEvidence>();
                var viewer = BuildScene(manifest, scene, evidence);
                var originalLeft = viewer.LeftIndex; var originalRight = viewer.RightIndex;
                var renders = capture ? CaptureCandidates(viewer) : Array.Empty<string>();
                viewer.SelectCandidate(originalLeft, true); viewer.SelectCandidate(originalRight, false);
                viewer.SetSourceNormalMaps(false); viewer.SetLighting(0); viewer.SetView(0); viewer.UpdateLayout();
                EnsureFolder(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                AssetDatabase.SaveAssets();
                if (capture)
                {
                    var output = CaptureOutput();
                    File.WriteAllText(Path.Combine(output, "unity-import-evidence.json"), JsonUtility.ToJson(new CaptureEvidence
                    {
                        unityVersion = Application.unityVersion, manifest = manifestPath, scene = ScenePath,
                        candidates = evidence.ToArray(), renders = renders,
                        additionalSourceNormalDiagnostics = HasArgument("-renBustCaptureSourceNormals")
                    }, true));
                }
                Debug.Log("REN_BUST_COMPARISON_BUILD_OK: " + ScenePath + (capture ? "; captures: " + CaptureOutput() : ""));
            }
            finally
            {
                foreach (var light in otherLights) if (light) light.enabled = true;
                if (!Application.isBatchMode || SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static void ValidateManifest(RenBustManifest manifest)
        {
            if (manifest == null || manifest.schemaVersion != 1)
                throw new InvalidDataException("Expected Ren bust manifest schemaVersion 1.");
            if (manifest.entries == null || manifest.entries.Length < 1)
                throw new InvalidDataException("At least one real generated bust is required; placeholders are not supported.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entry.id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_') || !ids.Add(entry.id))
                    throw new InvalidDataException("Candidate IDs must be unique safe filenames.");
                ValidateAssetPath(entry.fbxAsset);
                if (entry.materials == null || entry.materials.Length == 0)
                    throw new InvalidDataException("Candidate " + entry.id + " has no material mapping.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var material in entry.materials)
                {
                    if (material == null || string.IsNullOrWhiteSpace(material.sourceName) || !names.Add(material.sourceName))
                        throw new InvalidDataException("Candidate " + entry.id + " has missing or duplicate source material names.");
                    if (!string.IsNullOrWhiteSpace(material.baseColorAsset)) ValidateAssetPath(material.baseColorAsset);
                    if (!string.IsNullOrWhiteSpace(material.normalAsset)) ValidateAssetPath(material.normalAsset);
                    if (!string.IsNullOrWhiteSpace(material.metallicAsset)) ValidateAssetPath(material.metallicAsset);
                    if (!string.IsNullOrWhiteSpace(material.roughnessAsset)) ValidateAssetPath(material.roughnessAsset);
                }
            }
            foreach (var reference in manifest.references ?? Array.Empty<RenBustManifestReference>()) ValidateAssetPath(reference.textureAsset);
            if (manifest.defaults != null)
                foreach (var id in new[] { manifest.defaults.leftId, manifest.defaults.rightId })
                    if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id)) throw new InvalidDataException("Unknown default candidate " + id);
        }

        static void ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("..") || path.Contains('\\'))
                throw new InvalidDataException("Use a project-relative Assets/... path: " + path);
        }

        static RenBustComparisonViewer BuildScene(RenBustManifest manifest, Scene scene, List<CandidateEvidence> evidence)
        {
            EnsureFolder(AssetRoot + "/Viewer");
            var viewer = new GameObject("Ren Bust Comparison").AddComponent<RenBustComparisonViewer>();
            viewer.Title = string.IsNullOrWhiteSpace(manifest.title) ? "Ren / bust comparison" : manifest.title;
            var usedLayers = Resources.FindObjectsOfTypeAll<Renderer>()
                .Where(renderer => renderer.gameObject.scene.IsValid()).Select(renderer => renderer.gameObject.layer).ToHashSet();
            var layers = Enumerable.Range(24, 8).Reverse().Where(layer => !usedLayers.Contains(layer)).Take(2).ToArray();
            if (layers.Length != 2) throw new InvalidOperationException("Two unused scene layers are needed for isolated bust comparison.");
            viewer.LeftLayer = layers[0]; viewer.RightLayer = layers[1];
            viewer.LeftStage = Child(viewer.transform, "Left stage", new Vector3(-3, 0, 0));
            viewer.RightStage = Child(viewer.transform, "Right stage", new Vector3(3, 0, 0));
            viewer.LeftCamera = CreateCamera(viewer.transform, "Left comparison camera", 1 << viewer.LeftLayer, 0);
            viewer.RightCamera = CreateCamera(viewer.transform, "Right comparison camera", 1 << viewer.RightLayer, 1);
            var background = CreateCamera(viewer.transform, "Background camera", 0, -100);
            background.rect = new Rect(0, 0, 1, 1); background.backgroundColor = new Color(.055f, .064f, .082f);
            background.tag = "MainCamera";
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            var mask = (1 << viewer.LeftLayer) | (1 << viewer.RightLayer);
            var neutral = new List<Light> { CreateLight(viewer.transform, "Neutral key", LightType.Directional,
                Vector3.zero, new Vector3(20, -20, 0), Color.white, .65f, mask) };
            var club = new List<Light> { CreateLight(viewer.transform, "Club soft key", LightType.Directional,
                Vector3.zero, new Vector3(20, -15, 0), new Color(.88f, .92f, 1f), .55f, mask) };
            foreach (var stage in new[] { viewer.LeftStage, viewer.RightStage })
            {
                var localMask = 1 << (stage == viewer.LeftStage ? viewer.LeftLayer : viewer.RightLayer);
                neutral.Add(CreateLight(stage, "Neutral fill", LightType.Point, new Vector3(-1.3f, 1.1f, 1.8f), Vector3.zero, Color.white, 1.5f, localMask));
                neutral.Add(CreateLight(stage, "Neutral rim", LightType.Point, new Vector3(.8f, 1.5f, -.7f), Vector3.zero, new Color(.83f, .9f, 1f), .6f, localMask));
                club.Add(CreateLight(stage, "Club magenta", LightType.Point, new Vector3(-1.1f, 1.25f, .8f), Vector3.zero, new Color(1f, .08f, .42f), 1f, localMask));
                club.Add(CreateLight(stage, "Club cyan", LightType.Point, new Vector3(1.1f, 1.3f, -.1f), Vector3.zero, new Color(.06f, .75f, 1f), 1.4f, localMask));
            }
            viewer.NeutralLights = neutral.ToArray(); viewer.NightclubLights = club.ToArray();
            viewer.Candidates = manifest.entries.Select(entry => BuildCandidate(entry, evidence)).ToArray();
            viewer.References = (manifest.references ?? Array.Empty<RenBustManifestReference>()).Select(reference =>
                new RenBustReference { Label = reference.label, Texture = ConfigureTexture(reference.textureAsset, false, true) }).ToArray();
            var left = Array.FindIndex(viewer.Candidates, candidate => candidate.Id == manifest.defaults?.leftId);
            var right = Array.FindIndex(viewer.Candidates, candidate => candidate.Id == manifest.defaults?.rightId);
            viewer.SelectCandidate(left < 0 ? 0 : left, true);
            viewer.SelectCandidate(right < 0 ? Mathf.Min(1, viewer.Candidates.Length - 1) : right, false);
            viewer.ApplyLighting(); viewer.ApplyView(); viewer.UpdateLayout();
            return viewer;
        }

        static RenBustCandidate BuildCandidate(RenBustManifestEntry entry, List<CandidateEvidence> evidence)
        {
            AssetDatabase.ImportAsset(entry.fbxAsset, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(entry.fbxAsset) is ModelImporter importer))
                throw new InvalidDataException("Candidate is not a Unity model asset: " + entry.fbxAsset);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.fbxAsset);
            if (!source) throw new InvalidOperationException("Missing model " + entry.fbxAsset);
            var textures = new Dictionary<string, TextureEvidence>(StringComparer.Ordinal);
            var baseMaterials = new List<Material>(); var litMaterials = new List<Material>();
            var sourceNormalLitMaterials = new List<Material>();
            for (var i = 0; i < entry.materials.Length; i++)
            {
                var material = entry.materials[i];
                var color = string.IsNullOrWhiteSpace(material.baseColorAsset) ? null : ConfigureTexture(material.baseColorAsset, false, true);
                var normal = string.IsNullOrWhiteSpace(material.normalAsset) ? null : ConfigureTexture(material.normalAsset, true, false);
                if (color) textures[material.baseColorAsset] = InspectTexture(material.baseColorAsset, color);
                if (normal) textures[material.normalAsset] = InspectTexture(material.normalAsset, normal);
                // Source data is retained with the correct linear import, without pretending that
                // separate generated metallic/roughness maps match URP's packed metallic/smoothness format.
                foreach (var dataPath in new[] { material.metallicAsset, material.roughnessAsset })
                    if (!string.IsNullOrWhiteSpace(dataPath)) textures[dataPath] = InspectTexture(dataPath, ConfigureTexture(dataPath, false, false));
                baseMaterials.Add(CreateMaterial(entry.id + "-" + i + "-basecolor", false, material, color, null));
                litMaterials.Add(CreateMaterial(entry.id + "-" + i + "-lit", true, material, color, null));
                sourceNormalLitMaterials.Add(CreateMaterial(entry.id + "-" + i + "-lit-source-normals", true, material, color, normal));
            }
            var root = new GameObject(entry.id);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform, false);
                model.transform.localRotation = Quaternion.Euler(0, entry.frontYaw, 0) * model.transform.localRotation;
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException("No renderers in " + entry.id);
                var bounds = BoundsOf(renderers);
                if (bounds.size.y < .00001f) throw new InvalidDataException("Degenerate bust bounds in " + entry.id);
                var sourceBounds = bounds.size; var scale = 1.65f / bounds.size.y;
                model.transform.localScale *= scale;
                bounds = BoundsOf(renderers);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var triangles = 0;
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (var slot = 0; slot < materials.Length; slot++)
                    {
                        var originalName = materials[slot] ? materials[slot].name : "";
                        var index = Array.FindIndex(entry.materials, material => material.sourceName == originalName);
                        if (index < 0 && entry.materials.Length == 1) index = 0;
                        if (index < 0) throw new InvalidDataException("Unmapped source material in " + entry.id + ": " + originalName);
                        materials[slot] = baseMaterials[index];
                    }
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var filter = renderer.GetComponent<MeshFilter>();
                    var skin = renderer as SkinnedMeshRenderer;
                    var mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                    if (!mesh) continue;
                    for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                        if (mesh.GetTopology(submesh) == MeshTopology.Triangles) triangles += (int)mesh.GetIndexCount(submesh) / 3;
                }
                if (entry.triangleCount > 0 && entry.triangleCount != triangles)
                    Debug.LogWarning(entry.id + ": source reported " + entry.triangleCount + " triangles; actual Unity renderers have " + triangles + ". Viewer displays imported count.");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetRoot + "/Viewer/" + entry.id + ".prefab");
                evidence.Add(new CandidateEvidence
                {
                    id = entry.id, fbxAsset = entry.fbxAsset, importedTriangles = triangles,
                    sourceReportedTriangles = entry.triangleCount, frontYaw = entry.frontYaw,
                    sourceBoundsSize = sourceBounds, normalizationScale = scale, textures = textures.Values.ToArray()
                });
                return new RenBustCandidate
                {
                    Id = entry.id, Label = entry.label, Provider = entry.provider, ModelLabel = entry.modelLabel,
                    Pose = entry.pose, Notes = entry.notes, TextureSummary = string.Join(", ", entry.materials
                        .Where(material => textures.ContainsKey(material.baseColorAsset ?? ""))
                        .Select(material => textures[material.baseColorAsset]).Select(texture => texture.width + " × " + texture.height).Distinct()) + " base color",
                    TriangleCount = triangles, Prefab = prefab, BaseColorMaterials = baseMaterials.ToArray(), LitMaterials = litMaterials.ToArray(),
                    SourceNormalLitMaterials = sourceNormalLitMaterials.ToArray()
                };
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Bounds BoundsOf(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static Texture2D ConfigureTexture(string path, bool normal, bool sRGB)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new InvalidDataException("Missing texture " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = sRGB;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.isReadable = false;
            // This comparison has its own explicit desktop policy. Mobile platform overrides elsewhere
            // in the project do not establish a device budget for these high fidelity source assets.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = 8192,
                format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed,
                crunchedCompression = false
            });
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new InvalidDataException("Texture failed to import " + path);
            return texture;
        }

        static TextureEvidence InspectTexture(string path, Texture2D texture)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            return new TextureEvidence { asset = path, format = texture.format.ToString(), type = importer.textureType.ToString(),
                filtering = texture.filterMode.ToString(), width = texture.width, height = texture.height,
                mipCount = texture.mipmapCount, sRGB = importer.sRGBTexture };
        }

        static Material CreateMaterial(string name, bool lit, RenBustManifestMaterial source, Texture2D color, Texture2D normal)
        {
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
            if (!shader) throw new InvalidOperationException("Required URP comparison shader is unavailable.");
            var path = AssetRoot + "/Viewer/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetTexture("_BaseMap", color);
            var factor = source.baseColorFactor;
            material.SetVector("_BaseColor", factor == null ? Vector4.one : new Vector4(factor.r, factor.g, factor.b, factor.a));
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_AlphaClip", 0f); material.SetFloat("_Surface", 0f);
            if (lit)
            {
                material.SetFloat("_Metallic", 0f); material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_SpecularHighlights", 0f); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_EnvironmentReflections", 0f); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", source.normalScale);
                if (normal) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); child.localPosition = position;
            return child;
        }

        static Camera CreateCamera(Transform parent, string name, int mask, int depth)
        {
            var camera = Child(parent, name, Vector3.zero).gameObject.AddComponent<Camera>();
            camera.cullingMask = mask; camera.depth = depth;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .01f; camera.farClipPlane = 20f;
            camera.allowHDR = false; camera.allowMSAA = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            return camera;
        }

        static Light CreateLight(Transform parent, string name, LightType type, Vector3 position, Vector3 rotation, Color color, float intensity, int mask)
        {
            var light = Child(parent, name, position).gameObject.AddComponent<Light>();
            light.transform.localEulerAngles = rotation; light.type = type;
            light.color = color; light.intensity = intensity; light.cullingMask = mask;
            light.range = 4f; light.shadows = LightShadows.None;
            return light;
        }

        static string[] CaptureCandidates(RenBustComparisonViewer viewer)
        {
            var output = CaptureOutput(); Directory.CreateDirectory(output);
            var renders = new List<string>();
            var captureSourceNormals = HasArgument("-renBustCaptureSourceNormals");
            viewer.SetSourceNormalMaps(false);
            foreach (var entry in viewer.Candidates)
            {
                viewer.SelectById(entry.Id, true);
                for (var mode = 0; mode < 3; mode++)
                {
                    viewer.SetLighting(mode);
                    foreach (var yaw in new[] { 0f, 45f, 90f })
                    {
                        viewer.SetView(yaw);
                        var file = entry.Id + "--" + new[] { "basecolor", "neutral", "club" }[mode] + "--" + (yaw == 0f ? "front" : yaw == 45f ? "three-quarter" : "profile") + ".png";
                        CaptureCamera(viewer.LeftCamera, Path.Combine(output, file));
                        renders.Add(file);
                    }
                }
                if (captureSourceNormals)
                {
                    // Preserve the source-map diagnostic separately from the default shape review.
                    viewer.SetSourceNormalMaps(true); viewer.SetLighting(1);
                    foreach (var yaw in new[] { 0f, 45f, 90f })
                    {
                        viewer.SetView(yaw);
                        var file = entry.Id + "--neutral-source-normals--" +
                            (yaw == 0f ? "front" : yaw == 45f ? "three-quarter" : "profile") + ".png";
                        CaptureCamera(viewer.LeftCamera, Path.Combine(output, file));
                        renders.Add(file);
                    }
                    viewer.SetSourceNormalMaps(false);
                }
            }
            return renders.ToArray();
        }

        static void CaptureCamera(Camera camera, string path)
        {
            const int resolution = 1536;
            var target = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var previousTarget = RenderTexture.active;
            var previousRect = camera.rect; var previousAspect = camera.aspect;
            var pixels = new Texture2D(resolution, resolution, TextureFormat.RGB24, false, false);
            try
            {
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1f;
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.rect = previousRect; camera.aspect = previousAspect;
                RenderTexture.active = previousTarget;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [MenuItem("Lucid Loop/Character Art/Build Ren Bust Comparison Windows Viewer")]
        public static void BuildWindowsViewer()
        {
            Build();
            BuildPlayerFromSavedScene();
        }

        [MenuItem("Lucid Loop/Character Art/Build Capture and Windows Ren Bust Viewer")]
        public static void BuildCaptureAndWindowsViewer()
        {
            Run(true);
            BuildPlayerFromSavedScene();
        }

        public static void BuildPlayerFromSavedScene()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Build the Ren bust comparison scene before building its player.", ScenePath);
            var output = ReadArgument("-renBustPlayerOutput") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/ren-bust-viewer/RenBustComparison.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Ren bust viewer build failed: " + report.summary.result);
            Debug.Log("REN_BUST_COMPARISON_PLAYER_OK: " + output);
        }

        static string CaptureOutput() => Path.GetFullPath(ReadArgument("-renBustOutput") ??
            Path.Combine(Application.dataPath, "../../.local/ren-bust-comparison-captures"));

        static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
