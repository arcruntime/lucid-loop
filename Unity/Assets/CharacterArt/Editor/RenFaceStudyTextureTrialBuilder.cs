using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    [Serializable] public sealed class RenFaceStudyTextureMap
    {
        public string sourceName, baseColorAsset, baseColorSha256, normalAsset, normalSha256;
        public Color baseColorSrgb = Color.white;
        public float normalScale = 1;
        public bool useVertexColor;
    }

    [Serializable] public sealed class RenFaceStudyTextureTrial
    {
        public string sourceFbxAsset, sourceSha256, reviewNote, vertexColorEncoding;
        public Vector3 position, eulerAngles;
        public Vector3 scale = Vector3.one;
        public RenFaceStudyTextureMap[] materials = Array.Empty<RenFaceStudyTextureMap>();
        public string[] allowMissingUv0OnRenderers = Array.Empty<string>();
    }

    /// <summary>Imports only audited static Tripo output. No transfer onto the working facial mesh.</summary>
    public static class RenFaceStudyTextureTrialBuilder
    {
        const string Root = RenFaceStudyBuilder.AssetRoot + "/TextureTrial";
        public static RenBustCandidate Create(RenFaceStudyTextureTrial trial, float normalization, Vector3 offset)
        {
            Verify(trial.sourceFbxAsset, trial.sourceSha256);
            if (string.IsNullOrWhiteSpace(trial.reviewNote) || trial.materials == null || trial.materials.Length == 0)
                throw new InvalidDataException("Static texture trial requires reviewed material mappings and a review note.");
            if (trial.scale.x <= 0 || trial.scale.y <= 0 || trial.scale.z <= 0)
                throw new InvalidDataException("Static texture placement scale must be positive.");
            var importer = AssetImporter.GetAtPath(trial.sourceFbxAsset) as ModelImporter;
            if (!importer) throw new InvalidDataException("Static texture trial must supply a native FBX asset.");
            importer.importAnimation = false; importer.importBlendShapes = false; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            Folder(Root + "/Viewer");
            var shader = Shader.Find("LucidLoop/RenFaceStudy/Diffuse");
            if (!shader) throw new InvalidOperationException("The source texture review shader is missing.");
            var maps = new Dictionary<string, int>(StringComparer.Ordinal);
            var unlit = new List<Material>(); var lit = new List<Material>(); var normals = new List<Material>();
            var descriptions = new List<string>();
            for (var i = 0; i < trial.materials.Length; i++)
            {
                var map = trial.materials[i]; maps.Add(map.sourceName, i);
                var baseColor = Texture(map.baseColorAsset, map.baseColorSha256, false);
                var normal = string.IsNullOrWhiteSpace(map.normalAsset) ? null : Texture(map.normalAsset, map.normalSha256, true);
                var colorSrgb = string.Equals(trial.vertexColorEncoding, "sRGB", StringComparison.OrdinalIgnoreCase);
                unlit.Add(Material(i, "basecolor", shader, map, baseColor, normal, true, false, colorSrgb));
                lit.Add(Material(i, "lit", shader, map, baseColor, normal, false, false, colorSrgb));
                normals.Add(Material(i, "source-normals", shader, map, baseColor, normal, false, true, colorSrgb));
                descriptions.Add(baseColor.width + "×" + baseColor.height);
            }
            var root = new GameObject("RenFaceStudy-TripoTexture-Static");
            try
            {
                var frame = new GameObject("Shared normalization").transform; frame.SetParent(root.transform, false);
                frame.localScale = Vector3.one * normalization; frame.localPosition = offset;
                var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(trial.sourceFbxAsset));
                source.transform.SetParent(frame, false); source.transform.localPosition = trial.position;
                source.transform.localEulerAngles = trial.eulerAngles; source.transform.localScale = trial.scale;
                foreach (var camera in source.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in source.GetComponentsInChildren<Light>(true)) light.enabled = false;
                var triangleCount = 0; var used = new HashSet<string>();
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (!mesh) throw new InvalidDataException("Missing source mesh: " + renderer.name);
                    if (mesh.uv.Length != mesh.vertexCount && !(mesh.uv.Length == 0 && trial.allowMissingUv0OnRenderers.Contains(renderer.name)))
                        throw new InvalidDataException("Undeclared missing source UV0: " + renderer.name + " vertices=" + mesh.vertexCount + " uv0=" + mesh.uv.Length);
                    if (mesh.blendShapeCount != 0) throw new InvalidDataException("Static texture trial unexpectedly retains blendshapes.");
                    triangleCount += mesh.triangles.Length / 3;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    {
                        if (!material || !maps.TryGetValue(material.name, out var slot))
                            throw new InvalidDataException("Unmapped returned material on " + renderer.name + ": " + (material ? material.name : "<missing>"));
                        if (trial.materials[slot].useVertexColor && mesh.colors.Length != mesh.vertexCount)
                            throw new InvalidDataException("Missing returned source vertex colors on " + renderer.name);
                        used.Add(material.name); return lit[slot];
                    }).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                if (triangleCount == 0 || used.Count != maps.Count)
                    throw new InvalidDataException("Static trial geometry/material mapping is incomplete.");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Viewer/RenFaceStudy-TripoTexture-Static.prefab");
                return new RenBustCandidate
                {
                    Id = RenFaceStudyController.StaticTextureId, Label = "Tripo texture trial · static", Provider = "Tripo",
                    ModelLabel = "Reference-guided texture trial", Pose = "Static returned source; no facial controls",
                    TextureSummary = string.Join(", ", descriptions.Distinct()) + " source base color / desktop uncompressed",
                    Notes = "Actual returned geometry and UVs. Source normals default OFF. Painting, likeness and deformation transfer are unaccepted.",
                    TriangleCount = triangleCount, Prefab = prefab, BaseColorMaterials = unlit.ToArray(), LitMaterials = lit.ToArray(),
                    SourceNormalLitMaterials = normals.ToArray()
                };
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Texture2D Texture(string path, string hash, bool normal)
        {
            Verify(path, hash);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidDataException("Invalid source texture " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.maxTextureSize = 8192; importer.mipmapEnabled = true;
            importer.streamingMipmaps = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false; importer.alphaSource = TextureImporterAlphaSource.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true,
                maxTextureSize = 8192, format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed });
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(int index, string variant, Shader shader, RenFaceStudyTextureMap map,
            Texture2D baseColor, Texture2D normal, bool unlit, bool sourceNormals, bool vertexColorSrgb)
        {
            var path = Root + "/Viewer/RenFaceStudy-TripoTexture-" + index + "-" + variant + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetVector("_LinearBaseColor", map.baseColorSrgb.linear);
            material.SetFloat("_UseVertexColor", map.useVertexColor ? 1 : 0); material.SetFloat("_UseBaseMap", 1); material.SetTexture("_BaseMap", baseColor);
            material.SetFloat("_VertexColorSrgb", vertexColorSrgb ? 1 : 0);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_NormalScale", sourceNormals && normal ? map.normalScale : 0);
            material.SetFloat("_Unlit", unlit ? 1 : 0); EditorUtility.SetDirty(material); return material;
        }
        static void Verify(string path, string expected)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(Root + "/", StringComparison.Ordinal) || !File.Exists(path))
                throw new InvalidDataException("Texture trial sources must be copied under " + Root);
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Texture trial source hash mismatch: " + path);
            }
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
