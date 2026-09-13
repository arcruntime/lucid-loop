using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.CharacterArt.Editor
{
    /// <summary>Measures native FBX import coordinates without changing source bytes or importer settings.</summary>
    public static class RenPartsStudyImportAudit
    {
        [Serializable] sealed class Node
        {
            public string name, parent;
            public Vector3 position, eulerAngles, scale;
        }
        [Serializable] sealed class Source
        {
            public string role, asset, sha256, points;
            public float importerGlobalScale, importerFileScale;
            public int vertices;
            public Vector3 boundsMin, boundsMax;
            public Node[] nodes;
        }
        [Serializable] sealed class Evidence
        {
            public string unityVersion;
            public string status = "Native Unity FBX transforms and world-space vertex positions; no placement or geometry modification.";
            public Source[] sources;
        }

        public static void Audit()
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-renPartsAuditOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Provide -renPartsAuditOutput.");
            var output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); var records = new List<Source>();
                foreach (var role in new[] { "Head", "Hair" })
                {
                    var path = RenPartsStudyBuilder.AssetRoot + "/Sources/RenPartsStudy-" + role + ".fbx";
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!asset) throw new FileNotFoundException("Native FBX is not imported.", path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                    try
                    {
                        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                        var points = new List<Vector3>();
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        {
                            var filter = renderer.GetComponent<MeshFilter>(); var skin = renderer as SkinnedMeshRenderer;
                            var mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                            if (mesh) points.AddRange(mesh.vertices.Select(renderer.transform.TransformPoint));
                        }
                        if (points.Count == 0) throw new InvalidDataException("Native source has no mesh vertices.");
                        var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
                        var file = "RenPartsStudy-" + role.ToLowerInvariant() + "-unity-world.f32";
                        using (var stream = new BinaryWriter(File.Create(Path.Combine(output, file))))
                            foreach (var point in points) { stream.Write(point.x); stream.Write(point.y); stream.Write(point.z); }
                        string hash; using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path))
                            hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        records.Add(new Source { role = role, asset = path, sha256 = hash, points = file,
                            importerGlobalScale = importer.globalScale, importerFileScale = importer.fileScale, vertices = points.Count,
                            boundsMin = bounds.min, boundsMax = bounds.max,
                            nodes = instance.GetComponentsInChildren<Transform>(true).Select(node => new Node { name = node.name,
                                parent = node.parent ? node.parent.name : null, position = node.localPosition,
                                eulerAngles = node.localEulerAngles, scale = node.localScale }).ToArray() });
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
                File.WriteAllText(Path.Combine(output, "RenPartsStudy-native-import.json"), JsonUtility.ToJson(new Evidence
                    { unityVersion = Application.unityVersion, sources = records.ToArray() }, true));
                Debug.Log("REN_PARTS_NATIVE_IMPORT_AUDIT_OK: " + output);
            }
            finally
            {
                if (!Application.isBatchMode || SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
