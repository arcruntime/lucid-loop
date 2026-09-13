using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenHReferenceMaterial
    {
        public string sourceName, presetSourceName, baseColorAsset, baseColorSha256, vertexColorEncoding;
        public float[] baseColorLinearRgba;
        public bool useVertexColor;
    }
    [Serializable] public sealed class RenHReferenceManifest
    {
        public string sourceFbxAsset, sourceSha256, sourceLabel, reviewNote, mouthConvention;
        public string timelineAsset, timelineSha256, videoAsset, videoSha256, originalArtistAsset;
        public string nprShaderAsset, nprShaderSha256, nprIncludeAsset, nprIncludeSha256, nprPresetAsset, nprPresetSha256;
        public string headRendererPath, animatedHeadFramePath;
        public float frontYaw, normalizedHeight = 1.65f;
        public float cameraDistance = 4, cameraFieldOfView = 28;
        public string translationAmplitudeNote;
        public Vector3 headPivotInNormalizedFrame;
        public RenHReferenceMaterial[] materials = Array.Empty<RenHReferenceMaterial>();
        public RenHReferenceCapSource capAccessory;
        // Paths are relative to the Motion root. The imported FBX is its child named Source.
        public RenHReferenceMorphBinding[] morphBindings = Array.Empty<RenHReferenceMorphBinding>();
        public RenHReferenceRotationBinding[] rotationBindings = Array.Empty<RenHReferenceRotationBinding>();
        public RenHReferenceTranslationBinding[] translationBindings = Array.Empty<RenHReferenceTranslationBinding>();
    }
    [Serializable] public sealed class RenHReferenceCapSource
    {
        public string sourceFbxAsset, sourceSha256, contractAsset, contractSha256;
        public string sharedFemaleAvatarAsset, sharedFemaleAvatarSourceSha256;
        public string sharedFemaleRigDefinitionAsset, sharedFemaleRigDefinitionSha256;
        public RenHReferenceCapBinding binding;
        // Exact reviewed imported root TRS in CapSocket coordinates, not unconverted native Blender coordinates.
        public Vector3 localPosition, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public bool defaultVisible = true;
        public RenHReferenceMaterial[] materials = Array.Empty<RenHReferenceMaterial>();
    }
    public static class RenHReferenceAnimationBuilder
    {
        public const string Root = "Assets/CharacterArt/Generated/RenHReferenceAnimation";
        public const string ManifestPath = Root + "/RenHReferenceAnimationManifest.json";
        public const string ScenePath = "Assets/CharacterArt/Generated/Preview/Scenes/RenHReferenceAnimation.unity";

        [MenuItem("Lucid Loop/Character Art/Build H Reference Animation Study")]
        public static void Build()
        {
            if (!File.Exists(ManifestPath)) throw new InvalidDataException("No H study manifest exists. Wait for the reviewed real H export and control contract; no placeholder scene will be built.");
            var config = JsonUtility.FromJson<RenHReferenceManifest>(File.ReadAllText(ManifestPath));
            Validate(config);
            var timeline = AssetDatabase.LoadAssetAtPath<TextAsset>(config.timelineAsset);
            var timelineData = JsonUtility.FromJson<RenHReferenceTimeline>(timeline.text);
            RenHReferenceAnimationController.ValidateTimeline(timelineData);
            if (!string.Equals(timelineData.sourceVideoSha256, config.videoSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Timeline and video hashes differ.");
            var video = AssetDatabase.LoadAssetAtPath<VideoClip>(config.videoAsset);
            if (!video) throw new InvalidDataException("Import the real reference MP4 as a VideoClip first.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(config.nprShaderAsset);
            var presets = JsonUtility.FromJson<RenNprPresets>(File.ReadAllText(config.nprPresetAsset));
            if (!shader || shader.name != presets.shaderName) throw new InvalidDataException("Reviewed NPR shader contract is missing.");
            if (!config.sourceFbxAsset.StartsWith(Root + "/Sources/", StringComparison.Ordinal))
                throw new InvalidDataException("Import the new H FBX into this study's own Sources folder before building.");
            var importer = AssetImporter.GetAtPath(config.sourceFbxAsset) as ModelImporter;
            if (!importer) throw new InvalidDataException("Expected the reviewed native FBX import.");
            if (!importer.isReadable || !importer.importBlendShapes || importer.importAnimation)
            {
                importer.isReadable = true; importer.importBlendShapes = true; importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(config.sourceFbxAsset);
            if (!source) throw new InvalidDataException("The reviewed real H FBX has not been imported.");
            Folder(Root + "/Materials"); Folder(Root + "/Viewer");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Ren H reference animation");
                var controller = root.AddComponent<RenHReferenceAnimationController>();
                var motion = new GameObject("Motion").transform; motion.SetParent(root.transform, false);
                var model = Object.Instantiate(source, motion, false); model.name = "Source";
                model.transform.localRotation = Quaternion.Euler(0, config.frontYaw, 0) * model.transform.localRotation;
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidDataException("The H export contains no renderers.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y <= .001f) throw new InvalidDataException("The H export has invalid bounds.");
                var scale = config.normalizedHeight / bounds.size.y;
                var sourcePosition = model.transform.position;
                model.transform.localScale *= scale;
                model.transform.position = sourcePosition * scale + new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                motion.position = config.headPivotInNormalizedFrame;
                model.transform.position -= config.headPivotInNormalizedFrame;
                var materialMap = config.materials.ToDictionary(item => item.sourceName, item => MakeMaterial(item, presets, shader), StringComparer.Ordinal);
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    {
                        if (!material || !materialMap.TryGetValue(material.name, out var mapped))
                            throw new InvalidDataException("Unmapped source material on " + renderer.name + ": " + (material ? material.name : "null"));
                        return mapped;
                    }).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                }
                controller.ModelRoot = motion;
                controller.AnimatedHeadFrame = string.IsNullOrEmpty(config.animatedHeadFramePath) ? motion : motion.Find(config.animatedHeadFramePath);
                var head = motion.Find(config.headRendererPath);
                controller.HeadRenderer = head ? head.GetComponent<Renderer>() : null;
                if (!controller.HeadRenderer || !controller.AnimatedHeadFrame) throw new InvalidDataException("Reviewed H head/frame paths are unresolved.");
                controller.CapAttachment = AttachCap(root, motion, controller.HeadRenderer, config, presets, shader);
                var faceBounds = RenNprReviewController.CalculateFaceLocalBounds(controller.AnimatedHeadFrame, controller.HeadRenderer);
                foreach (var material in motion.GetComponentsInChildren<Renderer>(true).SelectMany(item => item.sharedMaterials).Distinct())
                { RenNprReviewController.SetFaceFrame(material, controller.AnimatedHeadFrame, faceBounds); EditorUtility.SetDirty(material); }
                ValidateBindings(motion, config, timelineData);
                controller.TimelineAsset = timeline; controller.MorphBindings = config.morphBindings; controller.RotationBindings = config.rotationBindings;
                controller.TranslationBindings = config.translationBindings;
                controller.CameraDistance = config.cameraDistance; controller.CameraFieldOfView = config.cameraFieldOfView; controller.DiagnosticOrthographic = false;
                controller.SourceLabel = config.sourceLabel; controller.SourceSha256 = config.sourceSha256; controller.MouthConvention = config.mouthConvention;
                controller.OriginalArtist = AssetDatabase.LoadAssetAtPath<Texture2D>(config.originalArtistAsset);
                controller.Playing = false; controller.ReferenceAudio = false;
                var camera = new GameObject("H review camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform, false);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.105f, .115f, .14f);
                camera.nearClipPlane = .01f; camera.farClipPlane = 6; camera.allowHDR = false;
                controller.ModelCamera = camera;
                var player = root.AddComponent<VideoPlayer>(); player.source = VideoSource.VideoClip; player.clip = video;
                player.playOnAwake = false; player.audioOutputMode = VideoAudioOutputMode.Direct; player.renderMode = VideoRenderMode.APIOnly;
                controller.ReferenceVideo = player;
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/RenHReferenceAnimationPipeline.asset");
                if (!pipeline)
                {
                    var template = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenNprReviewBuilder.Root + "/RenNprReviewPipeline.asset");
                    if (!template) throw new InvalidDataException("Reviewed isolated NPR preview pipeline is missing.");
                    pipeline = Object.Instantiate(template); pipeline.name = "RenHReferenceAnimationPipeline";
                    AssetDatabase.CreateAsset(pipeline, Root + "/RenHReferenceAnimationPipeline.asset");
                }
                controller.ReviewPipeline = pipeline;
                MakeLight(root.transform, "Fixed front key", LightType.Directional, Vector3.zero, new Vector3(25, 155, 0), Color.white, .9f, true);
                MakeLight(root.transform, "Neutral fill", LightType.Point, new Vector3(-1.3f, 1.1f, 1.8f), Vector3.zero, Color.white, 1.5f, false);
                MakeLight(root.transform, "Cool rim", LightType.Point, new Vector3(.8f, 1.5f, -.7f), Vector3.zero, new Color(.83f, .9f, 1), .6f, false);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.58f, .58f, .58f);
                RenderSettings.skybox = null; RenderSettings.fog = false;
                controller.SetView(0);
                PrefabUtility.SaveAsPrefabAsset(motion.gameObject, Root + "/Viewer/RenHReferenceAnimation-Source.prefab");
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Failed to save the isolated H reference scene.");
                AssetDatabase.SaveAssets(); Debug.Log("REN_H_REFERENCE_BUILD_OK: " + ScenePath);
            }
            finally
            {
                if (!Application.isBatchMode && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        static void Validate(RenHReferenceManifest config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.sourceLabel) || string.IsNullOrWhiteSpace(config.reviewNote) ||
                string.IsNullOrWhiteSpace(config.mouthConvention) || config.materials.Length == 0 || config.normalizedHeight <= 0)
                throw new InvalidDataException("The H source label, review note, material mappings and explicit mouth convention are required.");
            foreach (var pair in new[] {
                (config.sourceFbxAsset, config.sourceSha256), (config.timelineAsset, config.timelineSha256), (config.videoAsset, config.videoSha256),
                (config.nprShaderAsset, config.nprShaderSha256), (config.nprIncludeAsset, config.nprIncludeSha256), (config.nprPresetAsset, config.nprPresetSha256) }) Verify(pair.Item1, pair.Item2);
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(config.originalArtistAsset)) throw new InvalidDataException("Untouched original artist reference is required.");
            foreach (var material in config.materials)
            {
                if (material.baseColorLinearRgba == null || material.baseColorLinearRgba.Length != 4) throw new InvalidDataException("Linear source color required: " + material.sourceName);
                if (!string.IsNullOrEmpty(material.baseColorAsset)) Verify(material.baseColorAsset, material.baseColorSha256);
            }
            var cap = config.capAccessory;
            if (cap == null || cap.materials == null || cap.materials.Length == 0 || string.IsNullOrWhiteSpace(cap.sourceFbxAsset) ||
                cap.sourceFbxAsset == config.sourceFbxAsset || !cap.sourceFbxAsset.StartsWith(Root + "/Sources/", StringComparison.Ordinal))
                throw new InvalidDataException("The H study requires a separate reviewed cap export under its own Sources folder.");
            RenHReferenceCapAttachment.ValidateDefinition(cap.binding);
            Verify(cap.sourceFbxAsset, cap.sourceSha256); Verify(cap.contractAsset, cap.contractSha256);
            Verify(cap.sharedFemaleRigDefinitionAsset, cap.sharedFemaleRigDefinitionSha256);
            var rigDefinition = JsonUtility.FromJson<SharedFemaleRigIdentity>(File.ReadAllText(cap.sharedFemaleRigDefinitionAsset));
            if (rigDefinition == null || rigDefinition.archetype != "female_base" || rigDefinition.version != 2 || string.IsNullOrWhiteSpace(rigDefinition.rig_identity_sha256))
                throw new InvalidDataException("Cap target must refer to the existing shared female rig definition.");
            if (cap.binding.mode == RenHReferenceCapAttachment.SharedFemaleMode)
                Verify(cap.sharedFemaleAvatarAsset, cap.sharedFemaleAvatarSourceSha256);
            if (!Finite(cap.localPosition) || !Finite(cap.localScale) || cap.localScale.x <= 0 || cap.localScale.y <= 0 || cap.localScale.z <= 0 ||
                !Finite(cap.localRotation.x) || !Finite(cap.localRotation.y) || !Finite(cap.localRotation.z) || !Finite(cap.localRotation.w) ||
                Mathf.Abs(Quaternion.Dot(cap.localRotation, cap.localRotation) - 1) > .001f)
                throw new InvalidDataException("CapSocket attachment requires a finite reviewed imported TRS and unit quaternion.");
            foreach (var material in cap.materials)
            {
                if (material.baseColorLinearRgba == null || material.baseColorLinearRgba.Length != 4)
                    throw new InvalidDataException("Linear cap material color is required.");
                if (!string.IsNullOrEmpty(material.baseColorAsset)) Verify(material.baseColorAsset, material.baseColorSha256);
            }
        }
        [Serializable] sealed class SharedFemaleRigIdentity { public string archetype = null, rig_identity_sha256 = null; public int version = 0; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static RenHReferenceCapAttachment AttachCap(GameObject controllerRoot, Transform motion, Renderer head, RenHReferenceManifest config, RenNprPresets presets, Shader shader)
        {
            var cap = config.capAccessory;
            var socket = motion.Find(cap.binding.socketPath);
            if (!socket || socket.name != "CapSocket") throw new InvalidDataException("The real H assembly must contain the reviewed Head/CapSocket hierarchy; no socket or armature will be invented.");
            var importer = AssetImporter.GetAtPath(cap.sourceFbxAsset) as ModelImporter;
            if (!importer) throw new InvalidDataException("The separate cap FBX import is unavailable.");
            if (importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
            { importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None; importer.SaveAndReimport(); }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(cap.sourceFbxAsset);
            if (!source) throw new InvalidDataException("The separate reviewed cap FBX is missing.");
            var instance = Object.Instantiate(source, socket, false); instance.name = "CapAccessory";
            instance.transform.localPosition = cap.localPosition; instance.transform.localRotation = cap.localRotation; instance.transform.localScale = cap.localScale;
            var materials = cap.materials.ToDictionary(item => item.sourceName, item => MakeMaterial(item, presets, shader, "Cap-"), StringComparer.Ordinal);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                {
                    if (!material || !materials.TryGetValue(material.name, out var mapped)) throw new InvalidDataException("Unmapped separate cap material: " + renderer.name);
                    return mapped;
                }).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            var attachment = controllerRoot.AddComponent<RenHReferenceCapAttachment>();
            attachment.Binding = cap.binding; attachment.CapRoot = instance.transform; attachment.DefaultVisible = cap.defaultVisible;
            attachment.SourceSha256 = cap.sourceSha256;
            if (cap.binding.mode == RenHReferenceCapAttachment.SharedFemaleMode)
                attachment.ExpectedSharedFemaleAvatar = AssetDatabase.LoadAllAssetsAtPath(cap.sharedFemaleAvatarAsset).OfType<Avatar>().SingleOrDefault();
            RenHReferenceCapAttachment.ValidateHierarchy(motion, head, instance.transform, attachment.ExpectedSharedFemaleAvatar, cap.binding, config.morphBindings);
            attachment.Bind(motion, head, config.morphBindings);
            return attachment;
        }
        static void ValidateBindings(Transform modelRoot, RenHReferenceManifest config, RenHReferenceTimeline timeline)
        {
            RenHReferenceAnimationController.ValidateBindingDefinitions(timeline, config.morphBindings, config.rotationBindings, config.translationBindings);
            var channels = new HashSet<string>(timeline.channels.Select(item => item.name));
            foreach (var binding in config.morphBindings)
            {
                var node = modelRoot.Find(binding.rendererPath); var renderer = node ? node.GetComponent<SkinnedMeshRenderer>() : null;
                if (!channels.Contains(binding.control) || !renderer || renderer.sharedMesh.GetBlendShapeIndex(binding.shape) < 0)
                    throw new InvalidDataException("Manifest promises an unavailable H morph binding: " + binding.control + " / " + binding.shape);
            }
            foreach (var binding in config.rotationBindings)
                if ((!string.IsNullOrEmpty(binding.transformPath) && !modelRoot.Find(binding.transformPath)) ||
                    binding.axes.Any(axis => !channels.Contains(axis.control) || Mathf.Abs(axis.localAxis.magnitude - 1) > .001f))
                    throw new InvalidDataException("Manifest rotation basis is unresolved: " + binding.transformPath);
            if (string.IsNullOrWhiteSpace(config.translationAmplitudeNote))
                throw new InvalidDataException("Record the manually interpreted translation amplitudes explicitly; they are not measured motion capture.");
            foreach (var binding in config.translationBindings)
                if (!string.IsNullOrEmpty(binding.transformPath) && !modelRoot.Find(binding.transformPath))
                    throw new InvalidDataException("Manifest translation basis is unresolved: " + binding.transformPath);
            foreach (var required in new[] { "headShiftX", "lean" })
                if (channels.Contains(required) && !config.translationBindings.Any(binding => binding.axes.Any(axis => axis.control == required)))
                    throw new InvalidDataException("The reference performance requires an explicit translation binding for " + required);
        }
        static Material MakeMaterial(RenHReferenceMaterial map, RenNprPresets presets, Shader shader, string labelPrefix = "")
        {
            var preset = presets.materials.Single(item => item.sourceName == map.presetSourceName);
            var path = Root + "/Materials/RenHReferenceAnimation-" + labelPrefix + map.sourceName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; var color = map.baseColorLinearRgba;
            material.SetVector("_BaseColor", new Vector4(color[0], color[1], color[2], color[3]));
            material.SetFloat("_UseBaseMap", string.IsNullOrEmpty(map.baseColorAsset) ? 0 : 1);
            material.SetTexture("_BaseMap", string.IsNullOrEmpty(map.baseColorAsset) ? Texture2D.whiteTexture : AssetDatabase.LoadAssetAtPath<Texture2D>(map.baseColorAsset));
            material.SetFloat("_UseVertexColor", map.useVertexColor ? 1 : 0);
            material.SetFloat("_VertexColorSrgb", string.Equals(map.vertexColorEncoding, "sRGB", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            foreach (var entry in preset.floats) material.SetFloat(entry.name, entry.value);
            foreach (var entry in preset.colors) material.SetColor(entry.name, new Color(entry.linearRgba[0], entry.linearRgba[1], entry.linearRgba[2], entry.linearRgba[3]));
            material.SetFloat("_Unlit", 0); EditorUtility.SetDirty(material); return material;
        }
        static void MakeLight(Transform root, string name, LightType type, Vector3 position, Vector3 rotation, Color color, float intensity, bool shadows)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(root, false);
            light.transform.localPosition = position; light.transform.localEulerAngles = rotation;
            light.type = type; light.color = color; light.intensity = intensity; light.range = 4;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowBias = light.shadowNormalBias = .025f; light.shadowNearPlane = .05f;
            light.GetUniversalAdditionalLightData().usePipelineSettings = false;
        }
        static void Verify(string path, string expected)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path) || string.IsNullOrEmpty(expected))
                throw new InvalidDataException("Expected a portable imported source and exact hash: " + path);
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("H study input hash mismatch: " + path);
        }
        static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }
}
