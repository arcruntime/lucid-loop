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
 public static class RenDesignerIrisColorImport
 {
  const string Root="Assets/CharacterArt/Generated/RenDesignerEyeHairReview/Iris";
  const string Expected="da81e1d8aab0c93ea06bfde97936795551a0b5e54cb9341d4dd827c25bca3907";
  [Serializable] class Frame{public float[] nativeToUnity;public CornerSpec[] eyes;}
  [Serializable] class CornerSpec{public string name,cornersSha256;}
  [Serializable] class Transfer{public Entry[] objects;}
  [Serializable] class Entry{public string rendererName;public int sourceVertexCount;public Point[] vertices;}
  [Serializable] class Point{public int sourceVertexIndex;public float[] nativeHPosition,sourceUV,oldLinearRgba,newLinearRgba;}
  [Serializable] class Row{public string renderer,sourceAsset,sourceGuid,cloneAsset,sourceNonColorSha256,cloneNonColorSha256,originalColorSha256,candidateColorSha256;public long sourceFileId;public int vertices,drawnVertices,authoredCovered;public string rawCornersSha256;public float transferToRawPositionError,transferToRawUvError,transferToRawColorError,nativePositionError,uvError,oldColorError,newColorQuantizationError;public int[] importedToSource;}
  [Serializable] class Audit{public string status="PASS_COLOR_ONLY_CLONES",transferSha256=Expected,frameAuditSha256;public float oldColorTolerance=.5f/255+.00002f,transferToRawPositionTolerance=1e-6f,existingUnityToRawPositionTolerance=2e-6f;public Row[] meshes;public string limitation="Only two existing iris COLOR arrays change. New colors retain imported UNorm8 format; explicit quantization is measured. Transfer positions/UV/old colors independently match frozen raw FBX corners; actual imported position uses the pre-existing2e-6 gate because historic registration already has1.67e-6 error. No geometry, UV, materials, pupil, ink, transforms or controls change.";}
  public static RenDesignerEyeHairReview.IrisBinding[] Add(RenDesignerEyeReview review,string source)
  {
   if(Hash(source)!=Expected)throw new InvalidDataException("Frozen iris transfer mismatch");
   Directory.CreateDirectory(Root);var framePath="Assets/CharacterArt/Generated/RenDesignerEyeReview/ImportAudit.json";var fh=Hash(framePath);if(fh!=review.ImportAuditSha256)throw new InvalidDataException("Eye frame provenance differs");
   var frame=JsonUtility.FromJson<Frame>(File.ReadAllText(framePath));var matrix=Matrix4x4.identity;for(int i=0;i<16;i++)matrix[i/4,i%4]=frame.nativeToUnity[i];
   var data=JsonUtility.FromJson<Transfer>(File.ReadAllText(source));if(data.objects.Length!=2)throw new InvalidDataException("Expected two iris objects");var rows=new List<Row>();var bindings=new List<RenDesignerEyeHairReview.IrisBinding>();
   foreach(var entry in data.objects)
   {
    if(entry.rendererName!="Ren_DesignerEye_L_Iris"&&entry.rendererName!="Ren_DesignerEye_R_Iris")throw new InvalidDataException("Non-iris mutation rejected");
    var renderer=review.NewEyes.Single(r=>r.name==entry.rendererName);var filter=renderer.GetComponent<MeshFilter>();if(!filter||renderer is SkinnedMeshRenderer)throw new InvalidDataException("Expected rigid neutral iris MeshFilter");var mesh=filter.sharedMesh;
    if(mesh.blendShapeCount!=0||mesh.GetVertexAttributeFormat(VertexAttribute.Color)!=VertexAttributeFormat.UNorm8)throw new InvalidDataException("Unexpected iris shape/color layout");
    var vertices=mesh.vertices;var uv=mesh.uv;var oldColors=mesh.colors;var colors=mesh.colors32;var used=Enumerable.Range(0,mesh.subMeshCount).SelectMany(s=>mesh.GetIndices(s)).Distinct().ToArray();var map=Enumerable.Repeat(-1,vertices.Length).ToArray();
    var row=new Row{renderer=entry.rendererName,vertices=vertices.Length,drawnVertices=used.Length,sourceAsset=AssetDatabase.GetAssetPath(mesh),sourceNonColorSha256=NonColorHash(mesh),originalColorSha256=RenDesignerEyeHairReview.ColorHash(mesh)};AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out row.sourceGuid,out row.sourceFileId);
    var cornerPath="Assets/CharacterArt/Generated/RenDesignerEyeReview/CanonicalColorCorners/"+entry.rendererName+".f32";row.rawCornersSha256=Hash(cornerPath);if(row.rawCornersSha256!=frame.eyes.Single(e=>e.name==entry.rendererName).cornersSha256)throw new InvalidDataException("Frozen raw iris corners differ");
    var raw=new List<Point>();using(var reader=new BinaryReader(File.OpenRead(cornerPath)))while(reader.BaseStream.Position<reader.BaseStream.Length)raw.Add(new Point{nativeHPosition=new[]{reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()},sourceUV=new[]{reader.ReadSingle(),reader.ReadSingle()},oldLinearRgba=new[]{reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()}});
    foreach(var point in entry.vertices){var matches=raw.Where(c=>Vector3.Distance(V3(c.nativeHPosition),V3(point.nativeHPosition))<=1e-6f&&Vector2.Distance(new Vector2(c.sourceUV[0],c.sourceUV[1]),new Vector2(point.sourceUV[0],point.sourceUV[1]))<=2e-5f).ToArray();if(matches.Length==0||matches.Any(c=>Error(Col(c.oldLinearRgba),Col(point.oldLinearRgba))>1e-7f))throw new InvalidDataException("Transfer does not match authoritative raw FBX corners");var match=matches[0];row.transferToRawPositionError=Mathf.Max(row.transferToRawPositionError,Vector3.Distance(V3(match.nativeHPosition),V3(point.nativeHPosition)));row.transferToRawUvError=Mathf.Max(row.transferToRawUvError,Vector2.Distance(new Vector2(match.sourceUV[0],match.sourceUV[1]),new Vector2(point.sourceUV[0],point.sourceUV[1])));row.transferToRawColorError=Mathf.Max(row.transferToRawColorError,Error(Col(match.oldLinearRgba),Col(point.oldLinearRgba)));}
    var toNative=matrix.inverse*renderer.transform.localToWorldMatrix;var covered=new HashSet<int>();
    foreach(int v in used)
    {
     var p=toNative.MultiplyPoint3x4(vertices[v]);var candidates=entry.vertices.Where(x=>Vector3.Distance(p,V3(x.nativeHPosition))<=2e-6f&&Vector2.Distance(uv[v],new Vector2(x.sourceUV[0],x.sourceUV[1]))<=2e-5f).ToArray();
     if(candidates.Length!=1){var nearest=entry.vertices.OrderBy(x=>Vector3.Distance(p,V3(x.nativeHPosition))).First();throw new InvalidDataException("Iris correspondence: "+entry.rendererName+"/"+v+" candidates="+candidates.Length+" actualNative="+p.ToString("R")+" nearestNative="+V3(nearest.nativeHPosition).ToString("R")+" xyzError="+Vector3.Distance(p,V3(nearest.nativeHPosition)).ToString("R")+" actualUV="+uv[v].ToString("R")+" expectedUV="+new Vector2(nearest.sourceUV[0],nearest.sourceUV[1]).ToString("R")+" transform="+toNative.ToString("R"));}
     var point=candidates[0];var old=Col(point.oldLinearRgba);var error=Error(old,oldColors[v]);if(error>.5f/255+.00002f)throw new InvalidDataException("Old rendered iris pigment mismatch "+v+": "+error);
     row.nativePositionError=Mathf.Max(row.nativePositionError,Vector3.Distance(p,V3(point.nativeHPosition)));row.uvError=Mathf.Max(row.uvError,Vector2.Distance(uv[v],new Vector2(point.sourceUV[0],point.sourceUV[1])));row.oldColorError=Mathf.Max(row.oldColorError,error);
     var next=Col(point.newLinearRgba);if(next.a!=1||Enumerable.Range(0,4).Any(c=>!float.IsFinite(next[c])||next[c]<0||next[c]>1))throw new InvalidDataException("Invalid proposed iris pigment");colors[v]=(Color32)next;row.newColorQuantizationError=Mathf.Max(row.newColorQuantizationError,Error(next,(Color)colors[v]));map[v]=point.sourceVertexIndex;covered.Add(point.sourceVertexIndex);
    }
    row.authoredCovered=covered.Count;if(covered.Count!=entry.sourceVertexCount||entry.sourceVertexCount!=257)throw new InvalidDataException("Missing canonical iris positions");
    var clone=Object.Instantiate(mesh);clone.name=entry.rendererName+"_PigmentCandidate";clone.colors32=colors;row.cloneNonColorSha256=NonColorHash(clone);if(row.cloneNonColorSha256!=row.sourceNonColorSha256)throw new InvalidDataException("Non-color mesh data changed");
    row.candidateColorSha256=RenDesignerEyeHairReview.ColorHash(clone);row.importedToSource=map;row.cloneAsset=AssetDatabase.GenerateUniqueAssetPath(Root+"/"+clone.name+".asset");AssetDatabase.CreateAsset(clone,row.cloneAsset);AssetDatabase.SaveAssetIfDirty(clone);rows.Add(row);bindings.Add(new RenDesignerEyeHairReview.IrisBinding{Filter=filter,Original=mesh,Candidate=clone,OriginalColorSha256=row.originalColorSha256,CandidateColorSha256=row.candidateColorSha256});
   }
   File.Copy(source,Root+"/SourceTransfer.json",true);File.WriteAllText(Root+"/IrisImportAudit.json",JsonUtility.ToJson(new Audit{frameAuditSha256=fh,meshes=rows.ToArray()},true));return bindings.ToArray();
  }
  static string NonColorHash(Mesh m){using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){w.Write(m.vertexCount);w.Write((int)m.indexFormat);foreach(var a in new[]{m.vertices,m.normals}){w.Write(a.Length);foreach(var v in a){w.Write(v.x);w.Write(v.y);w.Write(v.z);}}foreach(var v in m.tangents){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}for(int channel=0;channel<8;channel++){var a=new List<Vector4>();m.GetUVs(channel,a);w.Write(a.Count);foreach(var v in a){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}w.Write(m.subMeshCount);for(int s=0;s<m.subMeshCount;s++){w.Write((int)m.GetTopology(s));w.Write(m.GetBaseVertex(s));foreach(var i in m.GetIndices(s))w.Write(i);}foreach(var v in new[]{m.bounds.center,m.bounds.extents}){w.Write(v.x);w.Write(v.y);w.Write(v.z);}w.Write(m.blendShapeCount);return Hash(stream.ToArray());}}
  static Vector3 V3(float[] a)=>new Vector3(a[0],a[1],a[2]);static Color Col(float[] a)=>new Color(a[0],a[1],a[2],a[3]);static float Error(Color a,Color b)=>Enumerable.Range(0,4).Max(i=>Mathf.Abs(a[i]-b[i]));
  static string Hash(string path)=>Hash(File.ReadAllBytes(path));static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 }
}
