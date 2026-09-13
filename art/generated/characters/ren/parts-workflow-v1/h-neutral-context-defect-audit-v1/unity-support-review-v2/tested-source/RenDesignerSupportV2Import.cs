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
 public static class RenDesignerSupportV2Import
 {
  const string Root="Assets/CharacterArt/Generated/RenDesignerSupportV2Review";
  const string FbxHash="a74d5cfd934bf8ab177b6922ea0434be7baaae04da36d06e3f10f578ffcb7d0d";
  const float PositionTolerance=2e-6f,ColorTolerance=.5f/255f+.00002f;
  [Serializable] class FileSpec {public string file,sha256;public int[] shape;}
  [Serializable] class MorphSpec {public FileSpec jawOpen_A,mouthSeal;}
  [Serializable] class Spec {public string name;public int vertices,triangles;public FileSpec basis_world,color_linear_rgba,head_boundary_vertex_index,source_h_vertex_id;public MorphSpec morphs;}
  [Serializable] class Contract {public string source_sha256;public FileSpec fbx;public Spec[] objects;}
  [Serializable] class VertexFailure {public string renderer,reason;public int vertex;public Vector3 actual,expected;public float error;}
  [Serializable] class MeshAudit {public string name,rendererType;public int drawnVertices,triangles,boundaryPoints;public float basisMax,colorMax,endpointMax,neutralMax,boundaryMax;public float[] boundaryDistances;public bool canonicalMorphsStatic;public string[] importedShapes,missingStaticShapes;public float[] colorMin,colorMaxValues;}
  [Serializable] class Audit {public string status="PREPARING",fbxSha256,contractSha256,sourcePigmentSha256,unityVersion;public float markerError,sourceScale,colorTolerance=ColorTolerance,positionTolerance=PositionTolerance;public bool sourceUnchanged,oldSupportStillVisible;public List<MeshAudit> meshes=new List<MeshAudit>();public List<VertexFailure> failures=new List<VertexFailure>();public string limitation="Neutral context only: canonical bridge A/seal targets are identical to Basis, so this does not prove functional bridge deformation. Linear colors are consumed once; explicit Unity UNorm8 allowance is recorded, never silently repaired. Boundary distances are nearest neutral historical-head vertex distances, not native-ID equality.";}
  public static Renderer[] Add(RenDesignerEyeReview review,string repoSupportExportFolder)
  {
   var audit=new Audit{unityVersion=Application.unityVersion};Directory.CreateDirectory(Root);var auditPath=Root+"/SupportImportAudit.json";
   try
   {
    var contractPath=Path.Combine(repoSupportExportFolder,"export-contract.json");audit.contractSha256=Hash(contractPath);var c=JsonUtility.FromJson<Contract>(File.ReadAllText(contractPath));audit.sourcePigmentSha256=c.source_sha256;
    if(c.fbx.sha256!=FbxHash||Hash(Path.Combine(repoSupportExportFolder,c.fbx.file))!=FbxHash)throw new InvalidDataException("Exact frozen support FBX mismatch.");
    var files=new List<FileSpec>{c.fbx};foreach(var s in c.objects){files.Add(s.basis_world);files.Add(s.color_linear_rgba);files.Add(s.head_boundary_vertex_index);files.Add(s.source_h_vertex_id);if(s.morphs!=null){files.Add(s.morphs.jawOpen_A);files.Add(s.morphs.mouthSeal);}}
    foreach(var f in files.Where(f=>f!=null&&!string.IsNullOrEmpty(f.file))){var src=Path.Combine(repoSupportExportFolder,f.file);if(Hash(src)!=f.sha256)throw new InvalidDataException("Canonical array hash mismatch: "+f.file);File.Copy(src,Root+"/"+Path.GetFileName(f.file),true);}
    File.Copy(contractPath,Root+"/export-contract.json",true);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    var asset=Root+"/"+c.fbx.file;var imp=(ModelImporter)AssetImporter.GetAtPath(asset);imp.isReadable=true;imp.importAnimation=false;imp.animationType=ModelImporterAnimationType.None;imp.importNormals=ModelImporterNormals.Import;imp.importBlendShapeNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.Import;imp.importBlendShapes=true;imp.meshCompression=ModelImporterMeshCompression.Off;imp.optimizeMeshPolygons=false;imp.optimizeMeshVertices=false;imp.weldVertices=false;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.SaveAndReimport();
    var source=review.ModelRoot.Find("Source");if(!source)throw new InvalidDataException("Missing historical Source root.");var head=Find(source,"Head");var frame=Matrix4x4.identity;
    for(int i=0;i<3;i++)frame.SetColumn(i,(Vector4)(Find(source,"Ren_H_HeadAxis_"+"XYZ"[i]).position-head.position));frame.SetColumn(3,new Vector4(head.position.x,head.position.y,head.position.z,1));
    var toWorld=frame*Matrix4x4.Translate(new Vector3(.05999999865889549f,0,.20000000298023224f));audit.sourceScale=toWorld.MultiplyVector(Vector3.right).magnitude;
    var old=Find(source,"Ren_H_ConcealedScalpBack").GetComponent<Renderer>();audit.oldSupportStillVisible=old.enabled;
    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));go.name="SupportBridgeCandidate";var origin=Find(go.transform,"RenSupport_FrameOrigin");var imported=Matrix4x4.identity;
    for(int i=0;i<3;i++)imported.SetColumn(i,(Vector4)((Find(go.transform,"RenSupport_Frame"+"XYZ"[i]).position-origin.position)/.1f));imported.SetColumn(3,new Vector4(origin.position.x,origin.position.y,origin.position.z,1));
    var wrapper=new GameObject("DesignerSupportRegistration").transform;wrapper.SetParent(review.ModelRoot,false);go.transform.SetParent(wrapper,false);SetMatrix(wrapper,review.ModelRoot.worldToLocalMatrix*toWorld*imported.inverse);
    foreach(var pair in new[]{("Origin",Vector3.zero),("X",Vector3.right*.1f),("Y",Vector3.up*.1f),("Z",Vector3.forward*.1f)})audit.markerError=Mathf.Max(audit.markerError,Vector3.Distance(Find(go.transform,"RenSupport_Frame"+pair.Item1).position,toWorld.MultiplyPoint3x4(pair.Item2))/audit.sourceScale);
    if(audit.markerError>PositionTolerance)throw new InvalidDataException("Support frame marker mismatch "+audit.markerError);
    var presets=JsonUtility.FromJson<RenNprPresets>(File.ReadAllText("Assets/CharacterArt/Generated/RenTokonReview/Contracts/ren-tokon-presets.json"));var preset=presets.materials.Single(p=>p.sourceName=="Skin");
    var renderers=new List<Renderer>();var materials=new List<Material>();var inverse=toWorld.inverse;var headMesh=new Mesh();Vector3[] headWorld;
    if(review.Head is SkinnedMeshRenderer hs){hs.BakeMesh(headMesh,false);headWorld=headMesh.vertices.Select(v=>inverse.MultiplyPoint3x4(hs.transform.TransformPoint(v))).ToArray();}else{headWorld=review.Head.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>inverse.MultiplyPoint3x4(review.Head.transform.TransformPoint(v))).ToArray();}Object.DestroyImmediate(headMesh);
    foreach(var s in c.objects)
    {
     var t=Find(go.transform,s.name);var r=t.GetComponent<Renderer>();var mesh=r is SkinnedMeshRenderer sk?sk.sharedMesh:t.GetComponent<MeshFilter>().sharedMesh;
     // A mesh with genuine imported shapes needs an actual renderer capable of applying them.
     if(!(r is SkinnedMeshRenderer)&&mesh.blendShapeCount>0){var mr=(MeshRenderer)r;var saved=mr.sharedMaterials;Object.DestroyImmediate(mr);Object.DestroyImmediate(t.GetComponent<MeshFilter>());var promoted=t.gameObject.AddComponent<SkinnedMeshRenderer>();promoted.sharedMesh=mesh;promoted.sharedMaterials=saved;promoted.bones=Array.Empty<Transform>();promoted.localBounds=mesh.bounds;promoted.updateWhenOffscreen=true;r=promoted;}
     var basis=Vectors(s.basis_world);var colors=Floats(s.color_linear_rgba);var used=Enumerable.Range(0,mesh.subMeshCount).SelectMany(i=>mesh.GetIndices(i)).Distinct().ToArray();var actual=mesh.vertices;var cs=mesh.colors;var row=new MeshAudit{name=s.name,rendererType=r.GetType().Name,drawnVertices=used.Length,triangles=Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(int)mesh.GetIndexCount(i))/3,canonicalMorphsStatic=true};audit.meshes.Add(row);
     if(row.triangles!=s.triangles||cs.Length!=actual.Length)throw new InvalidDataException("Drawn triangle/color count mismatch "+s.name);
     var map=new Dictionary<int,int>();var shapeNames=Enumerable.Range(0,mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();row.importedShapes=shapeNames;var missing=new List<string>();var targets=new Dictionary<string,Vector3[]>();if(s.morphs!=null){if(s.morphs.jawOpen_A!=null&&!string.IsNullOrEmpty(s.morphs.jawOpen_A.file))targets.Add("jawOpen_A",Vectors(s.morphs.jawOpen_A));if(s.morphs.mouthSeal!=null&&!string.IsNullOrEmpty(s.morphs.mouthSeal.file))targets.Add("mouthSeal",Vectors(s.morphs.mouthSeal));}
     var deltas=new Dictionary<string,Vector3[]>();foreach(var kv in targets){row.canonicalMorphsStatic&=kv.Value.SequenceEqual(basis);var si=mesh.GetBlendShapeIndex(kv.Key);var d=new Vector3[mesh.vertexCount];if(si<0){if(!kv.Value.SequenceEqual(basis))throw new InvalidDataException("Missing moving morph "+kv.Key);missing.Add(kv.Key);}else{if(mesh.GetBlendShapeFrameCount(si)!=1)throw new InvalidDataException("Unexpected morph frame count");mesh.GetBlendShapeFrameVertices(si,0,d,null,null);}deltas.Add(kv.Key,d);}row.missingStaticShapes=missing.ToArray();
     foreach(var i in used)
     {
      var v=inverse.MultiplyPoint3x4(t.TransformPoint(actual[i]));var candidates=Enumerable.Range(0,basis.Length).Where(j=>Vector3.Distance(v,basis[j])<=PositionTolerance).ToArray();int match=-1;
      foreach(var j in candidates){bool ok=true;for(int q=0;q<4;q++)ok&=Mathf.Abs(cs[i][q]-colors[j*4+q])<=ColorTolerance;foreach(var kv in targets){var e=inverse.MultiplyPoint3x4(t.TransformPoint(actual[i]+deltas[kv.Key][i]));ok&=Vector3.Distance(e,kv.Value[j])<=PositionTolerance;}if(ok){match=j;break;}}
      if(match<0){var j=Enumerable.Range(0,basis.Length).OrderBy(k=>Vector3.SqrMagnitude(v-basis[k])).First();audit.failures.Add(new VertexFailure{renderer=s.name,vertex=i,reason="No consistent Basis/color/morph canonical row",actual=v,expected=basis[j],error=Vector3.Distance(v,basis[j])});continue;}map[i]=match;row.basisMax=Mathf.Max(row.basisMax,Vector3.Distance(v,basis[match]));for(int q=0;q<4;q++)row.colorMax=Mathf.Max(row.colorMax,Mathf.Abs(cs[i][q]-colors[match*4+q]));foreach(var kv in targets)row.endpointMax=Mathf.Max(row.endpointMax,Vector3.Distance(inverse.MultiplyPoint3x4(t.TransformPoint(actual[i]+deltas[kv.Key][i])),kv.Value[match]));
     }
     row.colorMin=Enumerable.Range(0,4).Select(q=>used.Min(i=>cs[i][q])).ToArray();row.colorMaxValues=Enumerable.Range(0,4).Select(q=>used.Max(i=>cs[i][q])).ToArray();
     if(r is SkinnedMeshRenderer skin){for(int i=0;i<mesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,mesh.GetBlendShapeName(i)=="mouthSeal"?100:0);var baked=new Mesh();skin.BakeMesh(baked,false);foreach(var kv in map)row.neutralMax=Mathf.Max(row.neutralMax,Vector3.Distance(inverse.MultiplyPoint3x4(t.TransformPoint(baked.vertices[kv.Key])),targets.ContainsKey("mouthSeal")?targets["mouthSeal"][kv.Value]:basis[kv.Value]));Object.DestroyImmediate(baked);}
     if(s.head_boundary_vertex_index!=null&&!string.IsNullOrEmpty(s.head_boundary_vertex_index.file)){var distances=new List<float>();var ids=Ints(s.head_boundary_vertex_index);for(int j=0;j<ids.Length;j++)if(ids[j]>=0){row.boundaryPoints++;var distance=headWorld.Min(v=>Vector3.Distance(v,basis[j]));distances.Add(distance);row.boundaryMax=Mathf.Max(row.boundaryMax,distance);}row.boundaryDistances=distances.ToArray();}
     var material=new Material(old.sharedMaterial){name=s.name+"-MatchedSkin"};foreach(var fp in preset.floats)material.SetFloat(fp.name,fp.value);foreach(var cp in preset.colors)material.SetVector(cp.name,new Vector4(cp.linearRgba[0],cp.linearRgba[1],cp.linearRgba[2],cp.linearRgba[3]));material.SetVector("_BaseColor",Vector4.one);material.SetFloat("_UseVertexColor",1);material.SetFloat("_VertexColorSrgb",0);material.SetFloat("_UseBaseMap",0);material.SetFloat("_UseControlMap",0);material.SetFloat("_UseShadowMap",0);material.SetVector("_ControlFallback",new Vector4(0,0,0,1));material.SetFloat("_ClosedWeight",0);material.SetFloat("_Unlit",0);
     var mp=Root+"/"+material.name+".mat";AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(mp));r.sharedMaterials=Enumerable.Repeat(material,mesh.subMeshCount).ToArray();materials.Add(material);renderers.Add(r);
     if(row.neutralMax>PositionTolerance)throw new InvalidDataException("Actual neutral bridge mismatch "+row.neutralMax);
    }
    if(audit.failures.Count>0)throw new InvalidDataException("Support canonical vertex verification failed; inspect detailed rows.");if(renderers.Count!=2)throw new InvalidDataException("Expected exactly2 support renderers.");audit.fbxSha256=Hash(asset);audit.sourceUnchanged=Hash(Path.Combine(repoSupportExportFolder,c.fbx.file))==FbxHash&&Hash(contractPath)==audit.contractSha256;audit.status="PASS_IMPORTED_NEUTRAL_SUPPORT_CANONICAL_CHECKS_BOUNDARY_DISTANCE_REPORTED";AssetDatabase.SaveAssets();return renderers.ToArray();
   }
   catch(Exception e){audit.status="FAIL: "+e.Message;throw;}
   finally{File.WriteAllText(auditPath,JsonUtility.ToJson(audit,true));AssetDatabase.Refresh();}
  }
  static float[] Floats(FileSpec f){if(f==null||string.IsNullOrEmpty(f.file)||!File.Exists(Root+"/"+f.file))throw new InvalidDataException("Missing canonical payload file");var b=File.ReadAllBytes(Root+"/"+f.file);var a=new float[b.Length/4];Buffer.BlockCopy(b,0,a,0,b.Length);return a;}
  static int[] Ints(FileSpec f){if(f==null||string.IsNullOrEmpty(f.file)||!File.Exists(Root+"/"+f.file))throw new InvalidDataException("Missing canonical payload file");var b=File.ReadAllBytes(Root+"/"+f.file);var a=new int[b.Length/4];Buffer.BlockCopy(b,0,a,0,b.Length);return a;}
  static Vector3[] Vectors(FileSpec f){var a=Floats(f);return Enumerable.Range(0,a.Length/3).Select(i=>new Vector3(a[i*3],a[i*3+1],a[i*3+2])).ToArray();}
  static Transform Find(Transform t,string name)=>t.GetComponentsInChildren<Transform>(true).Single(x=>x.name==name);
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  static void SetMatrix(Transform t,Matrix4x4 m){var x=(Vector3)m.GetColumn(0);var y=(Vector3)m.GetColumn(1);var z=(Vector3)m.GetColumn(2);float sx=x.magnitude;if(Vector3.Dot(Vector3.Cross(x,y),z)<0)sx=-sx;t.localPosition=m.GetColumn(3);t.localRotation=Quaternion.LookRotation(z,y);t.localScale=new Vector3(sx,y.magnitude,z.magnitude);var actual=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale);if(Enumerable.Range(0,16).Any(i=>Mathf.Abs(actual[i]-m[i])>1e-4f))throw new InvalidDataException("Unsupported support registration shear");}
 }
}
