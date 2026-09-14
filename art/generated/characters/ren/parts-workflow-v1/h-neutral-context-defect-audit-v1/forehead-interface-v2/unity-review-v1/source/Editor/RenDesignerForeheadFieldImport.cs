using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace LucidLoop.CharacterArt.Editor
{
 public static class RenDesignerForeheadFieldImport
 {
  public const string AuditPath="Assets/CharacterArt/Generated/RenDesignerForeheadFieldReview/FieldImportAudit.json";
  const string Root="Assets/CharacterArt/Generated/RenDesignerForeheadFieldReview";
  const string ContractHash="d768e3165fb51786dc6803b185be07842a36976b721676677215a2ef6264f347";
  const float Tolerance=2e-6f;
  [Serializable] class Contract {public string basis_file,basis_sha256,control_field_file,control_field_sha256;public int vertices;}
  [Serializable] class ShaderContract {public string shader_sha256,hlsl_sha256;}
  [Serializable] class Failure {public int vertex;public Vector3 actual;public float nearestDistance;public string reason;}
  [Serializable] class Audit {public string status="PREPARING",contractSha256,fieldSha256,basisSha256,sourceMeshAttributesSha256,outputOriginalAttributesSha256,sourceIndexStreamSha256,outputIndexStreamSha256,shaderSha256,hlslSha256,unityVersion;public int importedVertices,canonicalVertices,usedVertices,triangles,oldSubmeshes,newSubmeshes=1;public float maxCorrespondenceError,fieldMin,fieldMax;public bool sourceMeshUnchanged,sourceMaterialsUnchanged,uv1AbsentBefore,sourceVisibilityUnchanged;public List<Failure> failures=new List<Failure>();public string comparison="Original partitioned renderer stays unchanged as baseline. New renderer initially disabled; runtime toggles renderer pair. Candidate uses graded field1. Field0 is unclassified-body diagnostic, NOT equal to original two-material partition baseline.";}
  public static Renderer Add(RenDesignerEyeReview review,Renderer currentSupport,string folder)
  {
   if(Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch-only helper");
   Directory.CreateDirectory(Root);var audit=new Audit{unityVersion=Application.unityVersion};
   try
   {
    var contractPath=Path.Combine(folder,"forehead-control-contract.json");if(Hash(contractPath)!=ContractHash)throw new InvalidDataException("Frozen field contract mismatch");audit.contractSha256=Hash(contractPath);var c=JsonUtility.FromJson<Contract>(File.ReadAllText(contractPath));var shaderContract=JsonUtility.FromJson<ShaderContract>(File.ReadAllText(Path.Combine(folder,"shader-contract.json")));
    // folder lives at <repo>/art/generated/characters/ren/parts-workflow-v1/h-neutral-context-defect-audit-v1/forehead-interface-v2.
    var repo=new DirectoryInfo(folder);while(repo!=null&&!Directory.Exists(Path.Combine(repo.FullName,".git"))&&!File.Exists(Path.Combine(repo.FullName,".git")))repo=repo.Parent;if(repo==null)throw new InvalidDataException("Cannot resolve repo root for canonical Basis");
    var basisPath=Path.Combine(repo.FullName,c.basis_file.Replace('/',Path.DirectorySeparatorChar));var fieldPath=Path.Combine(folder,c.control_field_file);if(Hash(basisPath)!=c.basis_sha256||Hash(fieldPath)!=c.control_field_sha256)throw new InvalidDataException("Field/Basis hash mismatch");audit.basisSha256=c.basis_sha256;audit.fieldSha256=c.control_field_sha256;
    foreach(var entry in new[]{("RenForeheadVertexControlV2.shader",shaderContract.shader_sha256),("RenForeheadVertexControlV2.hlsl",shaderContract.hlsl_sha256)}){var p=Path.Combine(folder,entry.Item1);if(Hash(p)!=entry.Item2)throw new InvalidDataException("Shader source hash mismatch: "+entry.Item1);File.Copy(p,Root+"/"+entry.Item1,true);}File.Copy(fieldPath,Root+"/"+c.control_field_file,true);File.Copy(contractPath,Root+"/forehead-control-contract.json",true);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    audit.shaderSha256=shaderContract.shader_sha256;audit.hlslSha256=shaderContract.hlsl_sha256;
    var source=currentSupport is SkinnedMeshRenderer ss?ss.sharedMesh:currentSupport.GetComponent<MeshFilter>().sharedMesh;if(!source||!source.isReadable)throw new InvalidDataException("Readable selected support mesh required");if(source.blendShapeCount!=0)throw new InvalidDataException("Support field helper expects rigid support, not bridge or animated mesh");
    var oldMats=currentSupport.sharedMaterials;var oldVisible=currentSupport.enabled;var oldSignature=Attributes(source);var originalIndices=Enumerable.Range(0,source.subMeshCount).SelectMany(i=>source.GetIndices(i)).ToArray();audit.sourceMeshAttributesSha256=oldSignature;audit.sourceIndexStreamSha256=HashInts(originalIndices);audit.oldSubmeshes=source.subMeshCount;
    var priorUV1=new List<Vector2>();source.GetUVs(1,priorUV1);audit.uv1AbsentBefore=!source.HasVertexAttribute(VertexAttribute.TexCoord1)&&priorUV1.Count==0;if(!audit.uv1AbsentBefore)throw new InvalidDataException("UV1 already occupied; cannot replace silently");
    var basis=Vectors(basisPath);var field=Floats(fieldPath);if(basis.Length!=c.vertices||field.Length!=c.vertices||field.Any(x=>float.IsNaN(x)||float.IsInfinity(x)||x<0||x>1.000001f))throw new InvalidDataException("Invalid field rowcount/range");
    var sourceRoot=review.ModelRoot.Find("Source");var head=Find(sourceRoot,"Head");var frame=Matrix4x4.identity;for(int k=0;k<3;k++)frame.SetColumn(k,(Vector4)(Find(sourceRoot,"Ren_H_HeadAxis_"+"XYZ"[k]).position-head.position));frame.SetColumn(3,new Vector4(head.position.x,head.position.y,head.position.z,1));var sourceToUnity=frame*Matrix4x4.Translate(new Vector3(.05999999865889549f,0,.20000000298023224f));var toSource=sourceToUnity.inverse*currentSupport.transform.localToWorldMatrix;
    var used=new HashSet<int>(originalIndices);var vertices=source.vertices;var uv1=new List<Vector2>(vertices.Length);audit.importedVertices=vertices.Length;audit.canonicalVertices=basis.Length;audit.usedVertices=used.Count;audit.triangles=originalIndices.Length/3;
    for(int i=0;i<vertices.Length;i++)
    {
     var v=toSource.MultiplyPoint3x4(vertices[i]);var candidates=Enumerable.Range(0,basis.Length).Where(j=>Vector3.Distance(v,basis[j])<=Tolerance).ToArray();float value=0;
     if(candidates.Length==0){if(used.Contains(i))audit.failures.Add(new Failure{vertex=i,actual=v,nearestDistance=basis.Min(p=>Vector3.Distance(v,p)),reason="No canonical Basis row within2e-6"});}
     else{value=field[candidates[0]];if(candidates.Any(j=>Mathf.Abs(field[j]-value)>1e-6f))audit.failures.Add(new Failure{vertex=i,actual=v,reason="Coincident canonical points disagree on R"});audit.maxCorrespondenceError=Mathf.Max(audit.maxCorrespondenceError,candidates.Min(j=>Vector3.Distance(v,basis[j])));}
     uv1.Add(new Vector2(value,0));
    }
    if(audit.failures.Count>0)throw new InvalidDataException("Canonical field correspondence failed; inspect rows");
    var mesh=Object.Instantiate(source);mesh.name="RenSupport-GradedForehead-OwnedCopy";mesh.SetUVs(1,uv1);mesh.subMeshCount=1;mesh.SetIndices(originalIndices,MeshTopology.Triangles,0,false);mesh.bounds=source.bounds;
    audit.outputOriginalAttributesSha256=Attributes(mesh);audit.outputIndexStreamSha256=HashInts(mesh.GetIndices(0));if(audit.outputOriginalAttributesSha256!=oldSignature||audit.outputIndexStreamSha256!=audit.sourceIndexStreamSha256)throw new InvalidDataException("Unexpected non-UV1 mesh mutation");
    var go=new GameObject("Ren_H_ConcealedScalpBack_GradedForehead");go.transform.SetParent(currentSupport.transform.parent,false);go.transform.localPosition=currentSupport.transform.localPosition;go.transform.localRotation=currentSupport.transform.localRotation;go.transform.localScale=currentSupport.transform.localScale;go.layer=currentSupport.gameObject.layer;
    var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/RenForeheadVertexControlV2.shader");if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidDataException("Forehead variant failed shader import");var material=new Material(oldMats[0]){name="RenSupport-GradedForehead-MatchedMaterial",shader=shader};material.SetFloat("_FaceMode",.9f);material.SetVector("_ControlFallback",new Vector4(0,0,0,1));material.SetFloat("_UseControlMap",0);material.SetFloat("_FaceCastShadowStrength",.05f);material.SetFloat("_UseForeheadVertexControl",1);
    var mf=go.AddComponent<MeshFilter>();mf.sharedMesh=mesh;var output=go.AddComponent<MeshRenderer>();output.sharedMaterial=material;output.shadowCastingMode=currentSupport.shadowCastingMode;output.receiveShadows=currentSupport.receiveShadows;output.lightProbeUsage=currentSupport.lightProbeUsage;output.reflectionProbeUsage=currentSupport.reflectionProbeUsage;output.renderingLayerMask=currentSupport.renderingLayerMask;output.enabled=false;
    AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Root+"/RenSupportForeheadField.asset"));AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Root+"/RenSupportForeheadField.mat"));AssetDatabase.SaveAssets();audit.fieldMin=uv1.Min(v=>v.x);audit.fieldMax=uv1.Max(v=>v.x);audit.sourceMeshUnchanged=Attributes(source)==oldSignature&&HashInts(Enumerable.Range(0,source.subMeshCount).SelectMany(i=>source.GetIndices(i)).ToArray())==audit.sourceIndexStreamSha256;audit.sourceMaterialsUnchanged=currentSupport.sharedMaterials.SequenceEqual(oldMats);audit.sourceVisibilityUnchanged=currentSupport.enabled==oldVisible;if(!audit.sourceMeshUnchanged||!audit.sourceMaterialsUnchanged||!audit.sourceVisibilityUnchanged)throw new InvalidDataException("Original support changed");audit.status="PASS_GRADED_FIELD_IMPORT_ORIGINAL_PARTITION_PRESERVED";return output;
   }
   catch(Exception e){audit.status="FAIL: "+e.Message;throw;}
   finally{File.WriteAllText(AuditPath,JsonUtility.ToJson(audit,true));AssetDatabase.Refresh();}
  }
  // Excludes only new UV1 and submesh grouping; exact original vertex streams remain hashed.
  static string Attributes(Mesh m){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){foreach(var v in m.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}foreach(var n in m.normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}foreach(var t in m.tangents){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}foreach(var c in m.colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}for(int i=0;i<8;i++){if(i==1)continue;var uv=new List<Vector4>();m.GetUVs(i,uv);w.Write(i);w.Write(uv.Count);foreach(var v in uv){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}w.Flush();return Digest(s.ToArray());}}
  static string HashInts(int[] a){var b=new byte[a.Length*4];Buffer.BlockCopy(a,0,b,0,b.Length);return Digest(b);}
  static float[] Floats(string path){var b=File.ReadAllBytes(path);var a=new float[b.Length/4];Buffer.BlockCopy(b,0,a,0,b.Length);return a;}
  static Vector3[] Vectors(string p){var a=Floats(p);return Enumerable.Range(0,a.Length/3).Select(i=>new Vector3(a[i*3],a[i*3+1],a[i*3+2])).ToArray();}
  static Transform Find(Transform t,string name){if(!t)throw new InvalidDataException("Missing Source frame");return t.GetComponentsInChildren<Transform>(true).Single(x=>x.name==name);}
  static string Hash(string path)=>Digest(File.ReadAllBytes(path));
  static string Digest(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 }
}
