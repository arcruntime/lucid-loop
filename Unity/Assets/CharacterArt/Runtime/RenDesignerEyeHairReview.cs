using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace LucidLoop.CharacterArt
{
 [DefaultExecutionOrder(700)] public sealed class RenDesignerEyeHairReview:MonoBehaviour
 {
  [Serializable] public sealed class IrisBinding{public MeshFilter Filter;public Mesh Original,Candidate;public string OriginalColorSha256,CandidateColorSha256;}
  public RenDesignerEyeReview Review;public RenDesignerForeheadReview Forehead;public IrisBinding[] Irises;public Renderer ApertureReturn;
  public Texture2D BaselineHairBase,BaselineHairShadow,CandidateHairBase,CandidateHairShadow;
  public bool IrisPigment,LowerReturn,DirectionalHair;public string RuntimeSha256,BuilderSha256,IrisAuditSha256,ReturnAuditSha256,HairBaseSha256,HairShadowSha256;
  Material[] hair;
  void LateUpdate()
  {
   Forehead.ForeheadResponse=true;
   foreach(var iris in Irises)iris.Filter.sharedMesh=IrisPigment?iris.Candidate:iris.Original;
   ApertureReturn.enabled=LowerReturn;
   if(hair==null)hair=Review.Hair.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
   foreach(var m in hair){m.SetTexture("_BaseMap",DirectionalHair?CandidateHairBase:BaselineHairBase);m.SetTexture("_ShadowMap",DirectionalHair?CandidateHairShadow:BaselineHairShadow);}
  }
  [Serializable] sealed class IrisReadback{public string renderer,mesh,colorSha256;public float useVertexColor,vertexColorSrgb,useBaseMap,unlit;public Vector4 tint;}
  [Serializable] sealed class HairReadback{public string material,baseMap,shadowMap;public float useBaseMap,useShadowMap,useVertexColor;public Vector4 tint;}
  [Serializable] sealed class Record{public string file,sha256,pose,variant;public bool irisPigment,lowerReturn,directionalHair,returnEnabled;public string returnCaster;public float returnUnlit,foreheadFaceMode,foreheadR,receiverFaceCast;public Rect cameraRect;public Vector3 cameraPosition;public bool orthographic;public IrisReadback[] iris;public HairReadback[] hair;}
  [Serializable] sealed class Evidence{public string status="NEUTRAL_EYE_RETURN_HAIR_COMPARISON_NOT_LIKENESS_ACCEPTANCE",runtimeSha256,builderSha256,irisAuditSha256,returnAuditSha256,hairBaseSha256,hairShadowSha256,unityVersion,graphicsApi;public Record[] captures;public string limitation="Same historical H073e geometry, neutral designer-eye v6, corrected support v2, temporal patch, local189-triangle forehead response and receiver classification/.05 in all states. Only iris COLOR, one118-triangle lower return and paired hair maps vary independently. No new blink/gaze/mouth motion or phone-performance claim. Source portrait uses fixed source camera and identical430x400 source-pixel crop at2x; existing ~18pixel mouth/chin fit error remains. Skin boundary and silhouette defects remain diagnostic.";}
  IEnumerator Start(){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,"-renDesignerEyeHairCapture");if(i<0||i+1>=a.Length)yield break;var output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);var routine=Capture(output);while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);Application.Quit(2);yield break;}yield return next;}}
  IEnumerator Capture(string output)
  {
   var records=new List<Record>();
   foreach(var pose in new[]{"neutral-front","neutral-quarter","neutral-profile","neutral-other-profile","club-front","unlit-front","source-camera"})
   {
    Review.SourceCamera=pose=="source-camera";Review.Yaw=pose=="neutral-quarter"?45:pose=="neutral-profile"?90:pose=="neutral-other-profile"?-90:0;Review.LightMode=pose.StartsWith("club",StringComparison.Ordinal)?2:1;Review.Lit=pose!="unlit-front";Review.ShowCap=Review.ShowHair=true;
    foreach(var variant in new[]{"baseline","iris-pigment","lower-return","directional-hair","combined"})
    {
     IrisPigment=variant=="iris-pigment"||variant=="combined";LowerReturn=variant=="lower-return"||variant=="combined";DirectionalHair=variant=="directional-hair"||variant=="combined";
     for(int frame=0;frame<12;frame++)yield return null;Validate();var file=variant+"--"+pose+".png";Render(Path.Combine(output,file),false);records.Add(Readback(file,pose,variant,output));
     if(Review.SourceCamera){file=variant+"--source-portrait-paired.png";Render(Path.Combine(output,file),true);records.Add(Readback(file,"source-portrait-paired",variant,output));}
    }
   }
   File.WriteAllText(Path.Combine(output,"EyeHairLiveEvidence.json"),JsonUtility.ToJson(new Evidence{runtimeSha256=RuntimeSha256,builderSha256=BuilderSha256,irisAuditSha256=IrisAuditSha256,returnAuditSha256=ReturnAuditSha256,hairBaseSha256=HairBaseSha256,hairShadowSha256=HairShadowSha256,unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),captures=records.ToArray()},true));Debug.Log("REN_DESIGNER_EYE_HAIR_CAPTURE_OK: "+output);Application.Quit(0);
  }
  void Validate()
  {
   if(Review.ModelCamera.rect!=new Rect(0,0,1,1)||Review.ModelCamera.orthographic!=Review.SourceCamera)throw new InvalidOperationException("Base camera not initialized/applied");
   if(!Review.SourceCamera&&Vector3.Distance(Review.ModelCamera.transform.position,Review.Target+Quaternion.Euler(0,Review.Yaw,0)*(Vector3.forward*Review.Distance))>.0001f)throw new InvalidOperationException("Camera pose mismatch");
   if(!Forehead.ForeheadResponse||Forehead.Support.sharedMaterials[1].GetFloat("_FaceMode")!=.9f||Forehead.Support.sharedMaterials[1].GetFloat("_Unlit")!=(Review.Lit?0:1)||Forehead.Refinement.Mode!=2||Review.Head.sharedMaterial.GetFloat("_FaceCastShadowStrength")!=.05f)throw new InvalidOperationException("Selected fixed response baseline mismatch");
   if(ApertureReturn.enabled!=LowerReturn||ApertureReturn.shadowCastingMode!=ShadowCastingMode.Off||ApertureReturn.sharedMaterial.GetFloat("_Unlit")!=1)throw new InvalidOperationException("Return visibility/material mismatch");
   foreach(var b in Irises){var m=b.Filter.GetComponent<Renderer>().sharedMaterial;if(b.Filter.sharedMesh!=(IrisPigment?b.Candidate:b.Original)||ColorHash(b.Filter.sharedMesh)!=(IrisPigment?b.CandidateColorSha256:b.OriginalColorSha256)||m.GetFloat("_UseVertexColor")!=1||m.GetFloat("_VertexColorSrgb")!=0||m.GetFloat("_UseBaseMap")!=0||m.GetFloat("_Unlit")!=1||m.GetVector("_BaseColor")!=Vector4.one)throw new InvalidOperationException("Actual iris binding/color flags mismatch");}
   foreach(var m in hair)if(m.GetTexture("_BaseMap")!=(DirectionalHair?CandidateHairBase:BaselineHairBase)||m.GetTexture("_ShadowMap")!=(DirectionalHair?CandidateHairShadow:BaselineHairShadow)||m.GetFloat("_UseBaseMap")!=1||m.GetFloat("_UseShadowMap")!=1||m.GetFloat("_UseVertexColor")!=0||m.GetVector("_BaseColor")!=Vector4.one)throw new InvalidOperationException("Actual paired hair map binding mismatch");
  }
  Record Readback(string file,string pose,string variant,string output)=>new Record{file=file,sha256=Hash(Path.Combine(output,file)),pose=pose,variant=variant,irisPigment=IrisPigment,lowerReturn=LowerReturn,directionalHair=DirectionalHair,returnEnabled=ApertureReturn.enabled,returnCaster=ApertureReturn.shadowCastingMode.ToString(),returnUnlit=ApertureReturn.sharedMaterial.GetFloat("_Unlit"),foreheadFaceMode=Forehead.Support.sharedMaterials[1].GetFloat("_FaceMode"),foreheadR=Forehead.Support.sharedMaterials[1].GetVector("_ControlFallback").x,receiverFaceCast=Review.Head.sharedMaterial.GetFloat("_FaceCastShadowStrength"),cameraRect=Review.ModelCamera.rect,cameraPosition=Review.ModelCamera.transform.position,orthographic=Review.ModelCamera.orthographic,iris=Irises.Select(b=>{var m=b.Filter.GetComponent<Renderer>().sharedMaterial;return new IrisReadback{renderer=b.Filter.name,mesh=b.Filter.sharedMesh.name,colorSha256=ColorHash(b.Filter.sharedMesh),useVertexColor=m.GetFloat("_UseVertexColor"),vertexColorSrgb=m.GetFloat("_VertexColorSrgb"),useBaseMap=m.GetFloat("_UseBaseMap"),unlit=m.GetFloat("_Unlit"),tint=m.GetVector("_BaseColor")};}).ToArray(),hair=hair.Select(m=>new HairReadback{material=m.name,baseMap=m.GetTexture("_BaseMap").name,shadowMap=m.GetTexture("_ShadowMap").name,useBaseMap=m.GetFloat("_UseBaseMap"),useShadowMap=m.GetFloat("_UseShadowMap"),useVertexColor=m.GetFloat("_UseVertexColor"),tint=m.GetVector("_BaseColor")}).ToArray()};
  void Render(string path,bool cropPair)
  {
   var camera=Review.ModelCamera;var scene=RenderTexture.GetTemporary(1536,Review.SourceCamera?1024:1536,24,RenderTextureFormat.ARGB32);var prior=camera.targetTexture;var active=RenderTexture.active;RenderTexture pair=null;
   try{camera.targetTexture=scene;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=scene});if(cropPair){pair=RenderTexture.GetTemporary(1720,800,0,RenderTextureFormat.ARGB32);RenderTexture.active=pair;GL.Clear(true,true,Color.black);GL.PushMatrix();GL.LoadPixelMatrix(0,1720,800,0);var uv=new Rect(275f/1536,1-425f/1024,430f/1536,400f/1024);Graphics.DrawTexture(new Rect(0,0,860,800),scene,uv,0,0,0,0);Graphics.DrawTexture(new Rect(860,0,860,800),Review.Sketch,uv,0,0,0,0);GL.PopMatrix();}var target=pair?pair:scene;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);}finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(scene);if(pair)RenderTexture.ReleaseTemporary(pair);}
  }
  void OnGUI(){var prior=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*Mathf.Min(Screen.width/1600f,Screen.height/900f));GUI.Box(new Rect(25,795,710,43),GUIContent.none);IrisPigment=GUI.Toggle(new Rect(35,803,180,28),IrisPigment,"Iris pigment trial");LowerReturn=GUI.Toggle(new Rect(240,803,200,28),LowerReturn,"Lower return trial");DirectionalHair=GUI.Toggle(new Rect(465,803,240,28),DirectionalHair,"Directional hair trial");GUI.matrix=prior;}
  public static string ColorHash(Mesh mesh){var colors=mesh.colors32;var bytes=new byte[colors.Length*4];for(int i=0;i<colors.Length;i++){bytes[i*4]=colors[i].r;bytes[i*4+1]=colors[i].g;bytes[i*4+2]=colors[i].b;bytes[i*4+3]=colors[i].a;}return Hash(bytes);}
  static string Hash(string path)=>Hash(File.ReadAllBytes(path));static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 }
}
