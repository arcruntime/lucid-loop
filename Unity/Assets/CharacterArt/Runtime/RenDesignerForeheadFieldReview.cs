using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace LucidLoop.CharacterArt
{
 [DefaultExecutionOrder(800)] public sealed class RenDesignerForeheadFieldReview:MonoBehaviour
 {
  public RenDesignerEyeHairReview Context;public Renderer OriginalSupport,CandidateSupport;public bool GradedField;public string ImportAuditSha256,RuntimeSha256,BuilderSha256;
  void LateUpdate(){Context.IrisPigment=Context.LowerReturn=Context.DirectionalHair=true;OriginalSupport.enabled=!GradedField;CandidateSupport.enabled=GradedField;}
  [Serializable] class Record{public string file,sha256,pose;public bool gradedField,originalVisible,candidateVisible;public float faceMode,fieldToggle,receiverStrength,bodyStrength,unlit;public Vector4 controlFallback;public Rect cameraRect;public Vector3 cameraPosition;public bool orthographic;}
  [Serializable] class Evidence{public string status="GRADED_FOREHEAD_CAUSAL_PAIR_NOT_ART_ACCEPTANCE",importAuditSha256,runtimeSha256,builderSha256,unityVersion,graphicsApi;public Record[] captures;public string limitation="Original local189-triangle partition vs one private support copy with graded adjacent-head R in UV1.x. Geometry, original vertex attributes and concatenated triangle indices are unchanged; two material groups merge to one draw. New iris/return/coherent hair and receiver.05 stay fixed. Field0 alone is not equal to the old partition; baseline uses unchanged original renderer. No final style, face controls or phone claim.";}
  IEnumerator Start(){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,"-renDesignerForeheadFieldCapture");if(i<0||i+1>=a.Length)yield break;var output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);var routine=Capture(output);while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);Application.Quit(2);yield break;}yield return next;}}
  IEnumerator Capture(string output)
  {
   var review=Context.Review;var records=new List<Record>();
   foreach(var pose in new[]{"neutral-front","neutral-quarter","neutral-profile","club-front","club-quarter","unlit-front","source-camera"})
   {
    review.SourceCamera=pose=="source-camera";review.Yaw=pose.EndsWith("quarter",StringComparison.Ordinal)?45:pose=="neutral-profile"?90:0;review.LightMode=pose.StartsWith("club",StringComparison.Ordinal)?2:1;review.Lit=pose!="unlit-front";review.ShowCap=review.ShowHair=true;
    foreach(var field in new[]{false,true})
    {
     GradedField=field;for(int frame=0;frame<12;frame++)yield return null;
     var m=CandidateSupport.sharedMaterial;if(OriginalSupport.enabled==field||CandidateSupport.enabled!=field||m.GetFloat("_UseForeheadVertexControl")!=1||m.GetFloat("_FaceMode")!=.9f||m.GetFloat("_FaceCastShadowStrength")!=.05f||m.GetFloat("_Unlit")!=(review.Lit?0:1)||!Context.IrisPigment||!Context.LowerReturn||!Context.DirectionalHair)throw new InvalidOperationException("Actual forehead field state mismatch");
     if(review.ModelCamera.rect!=new Rect(0,0,1,1)||review.ModelCamera.orthographic!=review.SourceCamera||(!review.SourceCamera&&Vector3.Distance(review.ModelCamera.transform.position,review.Target+Quaternion.Euler(0,review.Yaw,0)*(Vector3.forward*review.Distance))>.0001f))throw new InvalidOperationException("Actual camera state mismatch");
     var file=(field?"graded-field":"partition-baseline")+"--"+pose+".png";Render(Path.Combine(output,file),false);records.Add(Read(file,pose,output));if(review.SourceCamera){file=(field?"graded-field":"partition-baseline")+"--source-portrait-paired.png";Render(Path.Combine(output,file),true);records.Add(Read(file,"source-portrait-paired",output));}
    }
   }
   File.WriteAllText(Path.Combine(output,"ForeheadFieldLiveEvidence.json"),JsonUtility.ToJson(new Evidence{importAuditSha256=ImportAuditSha256,runtimeSha256=RuntimeSha256,builderSha256=BuilderSha256,unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),captures=records.ToArray()},true));Debug.Log("REN_DESIGNER_FOREHEAD_FIELD_CAPTURE_OK: "+output);Application.Quit(0);
  }
  Record Read(string file,string pose,string output){var m=CandidateSupport.sharedMaterial;return new Record{file=file,sha256=Hash(Path.Combine(output,file)),pose=pose,gradedField=GradedField,originalVisible=OriginalSupport.enabled,candidateVisible=CandidateSupport.enabled,faceMode=m.GetFloat("_FaceMode"),fieldToggle=m.GetFloat("_UseForeheadVertexControl"),receiverStrength=m.GetFloat("_FaceCastShadowStrength"),bodyStrength=m.GetFloat("_ShadowStrength"),unlit=m.GetFloat("_Unlit"),controlFallback=m.GetVector("_ControlFallback"),cameraRect=Context.Review.ModelCamera.rect,cameraPosition=Context.Review.ModelCamera.transform.position,orthographic=Context.Review.ModelCamera.orthographic};}
  void Render(string path,bool cropPair)
  {
   var review=Context.Review;var camera=review.ModelCamera;var scene=RenderTexture.GetTemporary(1536,review.SourceCamera?1024:1536,24,RenderTextureFormat.ARGB32);var prior=camera.targetTexture;var active=RenderTexture.active;RenderTexture pair=null;
   try{camera.targetTexture=scene;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=scene});if(cropPair){pair=RenderTexture.GetTemporary(1720,800,0,RenderTextureFormat.ARGB32);RenderTexture.active=pair;GL.Clear(true,true,Color.black);GL.PushMatrix();GL.LoadPixelMatrix(0,1720,800,0);var uv=new Rect(275f/1536,1-425f/1024,430f/1536,400f/1024);Graphics.DrawTexture(new Rect(0,0,860,800),scene,uv,0,0,0,0);Graphics.DrawTexture(new Rect(860,0,860,800),review.Sketch,uv,0,0,0,0);GL.PopMatrix();}var target=pair?pair:scene;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);}finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(scene);if(pair)RenderTexture.ReleaseTemporary(pair);}
  }
  void OnGUI(){GradedField=GUI.Toggle(new Rect(25,Screen.height-65,650,30),GradedField,"Graded forehead R field / same support geometry / diagnostic");}
  static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
