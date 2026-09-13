using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenFbxWeldAudit
    {
        const string Root = "Assets/CharacterArt/Generated/RenFbxWeldAudit";
        [Serializable] sealed class Input { public string id,kind,asset,sha256; public bool weld; }
        [Serializable] sealed class Config { public Input[] inputs; public string output; }
        [Serializable] sealed class Frame
        {
            public string shape,file,sha256; public int vertices,rawNonzeroDeltaVertices,rawZeroDeltaVertices; public float frameWeight;
        }
        [Serializable] sealed class MeshRecord
        {
            public string name,path; public int vertices,triangles; public float[] meshLocalToSourceWorld; public Frame[] frames;
        }
        [Serializable] sealed class Case
        {
            public string id,kind,fbxSha256,metaSha256; public bool weldVertices,isReadable,importBlendShapes,importAnimation,optimizeMeshVertices,optimizeMeshPolygons;
            public string meshCompression,importNormals,importBlendShapeNormals; public MeshRecord[] meshes;
        }
        [Serializable] sealed class Audit
        {
            public string unityVersion,editorSourceSha256,status = "FOUR_COPIED_IMPORTS_EXPORTED_FOR_AUTHORED_ENDPOINT_COMPARISON";
            public float[] sourceWorldToUnityWorld; public Case[] cases;
            public string limits = "Numeric import experiment only. Four new FBX copies use identical paired importer settings apart from weldVertices and fresh GUIDs. Original imports and all scenes/materials remain untouched. No rendering or player build.";
        }
        public static void Run()
        {
            if (!Application.isBatchMode || Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Weld audit requires the authorized scratch batch Editor.");
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(Root + "/audit-input.json"));
            Directory.CreateDirectory(config.output); var cases = new List<Case>(); var sourceToUnity = Matrix4x4.identity; var calibrated = false;
            foreach (var input in config.inputs)
            {
                if (!input.asset.StartsWith(Root + "/",StringComparison.Ordinal) || Hash(input.asset) != input.sha256) throw new InvalidDataException("Copied audit input mismatch.");
                AssetDatabase.ImportAsset(input.asset,ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(input.asset) as ModelImporter;
                if (!importer || importer.weldVertices != input.weld || !importer.isReadable || !importer.importBlendShapes || importer.importAnimation || importer.meshCompression != ModelImporterMeshCompression.Off || importer.optimizeMeshVertices || importer.optimizeMeshPolygons)
                    throw new InvalidDataException("Audit importer settings do not match frozen baseline.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(input.asset); var instance = Object.Instantiate(prefab);
                try
                {
                    if (input.kind == "source")
                    {
                        var head = Find(instance.transform,"Head"); var frame = Matrix4x4.identity;
                        frame.SetColumn(0,(Vector4)(Find(instance.transform,"Ren_H_HeadAxis_X").position-head.position));
                        frame.SetColumn(1,(Vector4)(Find(instance.transform,"Ren_H_HeadAxis_Y").position-head.position));
                        frame.SetColumn(2,(Vector4)(Find(instance.transform,"Ren_H_HeadAxis_Z").position-head.position));
                        frame.SetColumn(3,new Vector4(head.position.x,head.position.y,head.position.z,1));
                        var candidate = frame * Matrix4x4.Translate(new Vector3(.05999999865889549f,0,.20000000298023224f));
                        if (calibrated && Enumerable.Range(0,16).Any(i => Mathf.Abs(candidate[i]-sourceToUnity[i]) > 1e-6f)) throw new InvalidDataException("Source coordinate calibration changed across weld modes.");
                        sourceToUnity = candidate; calibrated = true;
                    }
                    if (!calibrated) throw new InvalidDataException("Source marker calibration must precede outline imports.");
                    var folder = Path.Combine(config.output,input.id); Directory.CreateDirectory(folder); var meshes = new List<MeshRecord>();
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        if (!mesh || mesh.blendShapeCount == 0) continue;
                        var matrix = sourceToUnity.inverse * renderer.transform.localToWorldMatrix;
                        var vertices = mesh.vertices; var frames = new List<Frame>();
                        frames.Add(WriteFrame(folder,renderer.name,"Basis",vertices,null,matrix,0));
                        for (var i = 0; i < mesh.blendShapeCount; i++)
                        {
                            if (mesh.GetBlendShapeFrameCount(i) != 1) throw new InvalidDataException("Unexpected multi-frame shape in audit.");
                            var delta = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(i,0,delta,null,null);
                            frames.Add(WriteFrame(folder,renderer.name,mesh.GetBlendShapeName(i),vertices,delta,matrix,mesh.GetBlendShapeFrameWeight(i,0)));
                        }
                        meshes.Add(new MeshRecord { name = renderer.name,path = PathOf(instance.transform,renderer.transform),vertices = mesh.vertexCount,
                            triangles = Enumerable.Range(0,mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i))/3,
                            meshLocalToSourceWorld = Enumerable.Range(0,16).Select(i => matrix[i/4,i%4]).ToArray(),frames = frames.ToArray() });
                    }
                    cases.Add(new Case { id = input.id,kind = input.kind,fbxSha256 = input.sha256,metaSha256 = Hash(input.asset+".meta"),weldVertices = importer.weldVertices,isReadable = importer.isReadable,
                        importBlendShapes = importer.importBlendShapes,importAnimation = importer.importAnimation,optimizeMeshVertices = importer.optimizeMeshVertices,optimizeMeshPolygons = importer.optimizeMeshPolygons,
                        meshCompression = importer.meshCompression.ToString(),importNormals = importer.importNormals.ToString(),importBlendShapeNormals = importer.importBlendShapeNormals.ToString(),meshes = meshes.ToArray() });
                    Debug.Log("REN_FBX_WELD_CASE_OK: " + input.id + " meshes=" + meshes.Count);
                }
                finally { Object.DestroyImmediate(instance); }
            }
            File.WriteAllText(Path.Combine(config.output,"imported-manifest.json"),JsonUtility.ToJson(new Audit { unityVersion = Application.unityVersion,
                editorSourceSha256 = Hash("Assets/CharacterArt/Editor/RenFbxWeldAudit.cs"),sourceWorldToUnityWorld = Enumerable.Range(0,16).Select(i => sourceToUnity[i/4,i%4]).ToArray(),cases = cases.ToArray() },true));
            Debug.Log("REN_FBX_WELD_AUDIT_OK: " + config.output);
        }
        static Frame WriteFrame(string folder,string meshName,string shape,Vector3[] vertices,Vector3[] deltas,Matrix4x4 matrix,float weight)
        {
            var file = meshName + "--" + shape + ".f32"; var path = Path.Combine(folder,file); var nonzero = 0;
            using (var writer = new BinaryWriter(File.Create(path)))
                for (var i = 0; i < vertices.Length; i++)
                {
                    var delta = deltas == null ? Vector3.zero : deltas[i]; if (delta.x != 0 || delta.y != 0 || delta.z != 0) nonzero++;
                    var p = matrix.MultiplyPoint3x4(vertices[i]+delta); writer.Write(p.x); writer.Write(p.y); writer.Write(p.z);
                }
            return new Frame { shape = shape,file = file,sha256 = Hash(path),vertices = vertices.Length,rawNonzeroDeltaVertices = nonzero,rawZeroDeltaVertices = vertices.Length-nonzero,frameWeight = weight };
        }
        static Transform Find(Transform root,string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        static string PathOf(Transform root,Transform node) => node == root ? "Root" : PathOf(root,node.parent)+"/"+node.name;
        static string Hash(string path) { using (var file = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant(); }
    }
}
