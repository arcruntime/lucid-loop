using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LucidLoop.CharacterArt.Editor
{
 public static class RenDesignerForeheadPartition
 {
  const string Root="Assets/CharacterArt/Generated/RenDesignerForeheadReview";
  [Serializable] class Triangle{public int[] canonical_vertices;public int source_polygon,region;}
  [Serializable] class Contract{public string v2_fbx_sha256,control_map_sha256;public int[] selected_polygons;public int selected_triangles,total_triangles;public Triangle[] triangles;}
  [Serializable] class Frame{public float[] nativeToUnity;}
  [Serializable] class Audit{public string status,contractSha256,supportFbxSha256,sourceMeshAsset,cloneMeshAsset;public int sourceVertices,sourceTriangles,region0Triangles,region1Triangles;public float canonicalPositionError;public bool positionNormalUvColorExact,allIndicesPreserved,sourceMeshUnchanged;public string scope="Private static support mesh copy; only triangle-index partition into two material regions, no vertex or triangle geometry change. Face response changes only region1; transition/neck/back remain baseline.";}
  public static Renderer Add(RenDesignerEyeReview review,string contractPath,Shader receiver)
  {
   Directory.CreateDirectory(Root);var c=JsonUtility.FromJson<Contract>(File.ReadAllText(contractPath));if(c.v2_fbx_sha256!="a74d5cfd934bf8ab177b6922ea0434be7baaae04da36d06e3f10f578ffcb7d0d"||c.selected_triangles!=189||c.total_triangles!=1478)throw new InvalidDataException("Frozen forehead partition contract mismatch");
   var candidate=review.ModelRoot.Find("DesignerSupportRegistration");if(!candidate)throw new InvalidDataException("Expected corrected support registration missing");var r=candidate.GetComponentsInChildren<Renderer>(true).Single(x=>x.name=="Ren_H_ConcealedScalpBack");var f=r.GetComponent<MeshFilter>();if(!f||r is SkinnedMeshRenderer)throw new InvalidDataException("Expected static source support");var source=f.sharedMesh;
   var sourceAsset=AssetDatabase.GetAssetPath(source);if(Hash(sourceAsset)!=c.v2_fbx_sha256)throw new InvalidDataException("Exact imported support FBX mismatch");var vertices=source.vertices;var normals=source.normals;var tangents=source.tangents;var uv=source.uv;var colors=source.colors32;var indices=source.triangles;if(source.blendShapeCount!=0||source.subMeshCount!=1||indices.Length!=1478*3)throw new InvalidDataException("Unexpected support layout");
   var headRoot=review.ModelRoot.Find("Source");var head=Find(headRoot,"Head");var matrix=Matrix4x4.identity;for(int i=0;i<3;i++)matrix.SetColumn(i,(Vector4)(Find(headRoot,"Ren_H_HeadAxis_"+"XYZ"[i]).position-head.position));matrix.SetColumn(3,new Vector4(head.position.x,head.position.y,head.position.z,1));var toNative=(matrix*Matrix4x4.Translate(new Vector3(.05999999865889549f,0,.20000000298023224f))).inverse*r.transform.localToWorldMatrix;
   var basisPath="Assets/CharacterArt/Generated/RenDesignerSupportV2Review/Ren_H_ConcealedScalpBack-Basis-world.f32";var bytes=File.ReadAllBytes(basisPath);var floats=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,floats,0,bytes.Length);var canonical=Enumerable.Range(0,floats.Length/3).Select(i=>new Vector3(floats[i*3],floats[i*3+1],floats[i*3+2])).ToArray();var map=new int[vertices.Length];float maximum=0;
   for(int i=0;i<map.Length;i++){var v=toNative.MultiplyPoint3x4(vertices[i]);var matches=Enumerable.Range(0,canonical.Length).Where(j=>Vector3.Distance(v,canonical[j])<=2e-6f).ToArray();if(matches.Length!=1)throw new InvalidDataException("Ambiguous canonical support vertex "+i+" count"+matches.Length);map[i]=matches[0];maximum=Mathf.Max(maximum,Vector3.Distance(v,canonical[map[i]]));}
   var polygons=c.triangles.GroupBy(t=>t.source_polygon).ToDictionary(g=>g.Key,g=>new HashSet<int>(g.SelectMany(t=>t.canonical_vertices)));var selected=new HashSet<int>(c.selected_polygons);var regions=new[]{new List<int>(),new List<int>()};var originalTriangleOrdinals=new[]{new List<int>(),new List<int>()};
   for(int i=0;i<indices.Length;i+=3){var tri=new[]{map[indices[i]],map[indices[i+1]],map[indices[i+2]]};var matches=polygons.Where(kv=>tri.All(v=>kv.Value.Contains(v))).Select(kv=>kv.Key).ToArray();if(matches.Length!=1)throw new InvalidDataException("Imported triangle has no unique source polygon "+i/3);var region=selected.Contains(matches[0])?1:0;regions[region].AddRange(new[]{indices[i],indices[i+1],indices[i+2]});originalTriangleOrdinals[region].Add(i/3);}
   if(regions[1].Count!=189*3||regions[0].Count!=1289*3)throw new InvalidDataException("Forehead partition counts differ");var clone=Object.Instantiate(source);clone.name="Support-ForeheadPartition";clone.subMeshCount=2;clone.SetTriangles(regions[0],0,false);clone.SetTriangles(regions[1],1,false);clone.bounds=source.bounds;
   bool exact=clone.vertices.SequenceEqual(vertices)&&clone.normals.SequenceEqual(normals)&&clone.tangents.SequenceEqual(tangents)&&clone.uv.SequenceEqual(uv)&&clone.colors32.SequenceEqual(colors);if(!exact)throw new InvalidDataException("Partition altered source vertex attributes");
   var clonePath=Root+"/Support-ForeheadPartition.asset";AssetDatabase.CreateAsset(clone,AssetDatabase.GenerateUniqueAssetPath(clonePath));f.sharedMesh=clone;var original=r.sharedMaterial;var materials=new Material[2];for(int i=0;i<2;i++){materials[i]=new Material(original){name="SupportRegion"+i,shader=receiver};AssetDatabase.CreateAsset(materials[i],AssetDatabase.GenerateUniqueAssetPath(Root+"/SupportRegion"+i+".mat"));}r.sharedMaterials=materials;var bound=review.ModelRoot.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).ToHashSet();review.Materials=review.Materials.Where(m=>bound.Contains(m)).Concat(materials).Distinct().ToArray();
   var audit=new Audit{status="PASS_PRIVATE_SUPPORT_TRIANGLE_PARTITION",contractSha256=Hash(contractPath),supportFbxSha256=c.v2_fbx_sha256,sourceMeshAsset=sourceAsset,cloneMeshAsset=AssetDatabase.GetAssetPath(clone),sourceVertices=vertices.Length,sourceTriangles=1478,region0Triangles=1289,region1Triangles=189,canonicalPositionError=maximum,positionNormalUvColorExact=exact,allIndicesPreserved=originalTriangleOrdinals.SelectMany(x=>x).OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,1478)),sourceMeshUnchanged=source.vertices.SequenceEqual(vertices)&&source.triangles.SequenceEqual(indices)};if(!audit.allIndicesPreserved||!audit.sourceMeshUnchanged)throw new InvalidDataException("Source index preservation failed");File.WriteAllText(Root+"/PartitionAudit.json",JsonUtility.ToJson(audit,true));File.Copy(contractPath,Root+"/SourcePartition.json",true);AssetDatabase.SaveAssets();return r;
  }
  static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
