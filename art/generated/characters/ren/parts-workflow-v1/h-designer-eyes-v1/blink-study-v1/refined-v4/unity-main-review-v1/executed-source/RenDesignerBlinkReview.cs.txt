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
 [DefaultExecutionOrder(1000)] public sealed class RenDesignerBlinkReview:MonoBehaviour
 {
  public RenDesignerMouthReview MouthContext;public SkinnedMeshRenderer[] Controlled;public Shader BlinkShader,SelectedReceiverShader;public float BlinkL,BlinkR;public bool IdleBlink;
  public string ImportAuditSha256,RuntimeSha256,BuilderSha256,BlinkShaderSha256,BlinkHlslSha256;
  int[] keys;bool[] left;Material[][] materials;bool capturing,shaderControl;float nextBlink=2,blinkStart=-100;System.Random random=new System.Random(1729);
  RenDesignerEyeReview Review=>MouthContext.Context.Context.Review;
  void Awake()
  {
   var refinement=MouthContext.Context.Context.Forehead.Refinement;refinement.enabled=false;refinement.InterfacePatch.enabled=true;
   foreach(var m in Review.Head.sharedMaterials){m.shader=SelectedReceiverShader;m.SetFloat("_FaceCastShadowStrength",.05f);m.SetFloat("_ShadowStrength",.8f);}
   if(Controlled.Length!=13)throw new InvalidOperationException("Expected13 controlled eye layers");
   left=Controlled.Select(r=>r.sharedMesh.GetBlendShapeIndex("eyeBlinkL")>=0).ToArray();keys=Controlled.Select((r,i)=>r.sharedMesh.GetBlendShapeIndex(left[i]?"eyeBlinkL":"eyeBlinkR")).ToArray();if(keys.Any(i=>i<0))throw new InvalidOperationException("Own-side blink key absent");
   materials=Controlled.Select(r=>r.sharedMaterials).ToArray();foreach(var row in materials)foreach(var m in row){m.shader=BlinkShader;m.SetFloat("_EyeBlinkWeight",0);if(m.GetFloat("_Unlit")!=1)m.SetFloat("_FaceCastShadowStrength",.05f);}
  }
  void LateUpdate()
  {
   if(!capturing&&IdleBlink){if(Time.time>=nextBlink){blinkStart=Time.time;nextBlink=Time.time+2+(float)random.NextDouble()*4;}BlinkL=BlinkR=Pulse(Time.time,blinkStart);}
   BlinkL=Mathf.Clamp01(BlinkL);BlinkR=Mathf.Clamp01(BlinkR);for(int i=0;i<Controlled.Length;i++){var weight=left[i]?BlinkL:BlinkR;Controlled[i].SetBlendShapeWeight(keys[i],100*weight);foreach(var m in materials[i])m.SetFloat("_EyeBlinkWeight",weight);}
  }
  static float Pulse(float t,float start){t-=start;if(t<0||t>=.245f)return 0;if(t<.065f)return Mathf.SmoothStep(0,1,t/.065f);if(t<.105f)return 1;return 1-Mathf.SmoothStep(0,1,(t-.105f)/.14f);}
  [Serializable] class Layer{public string renderer,key,shader;public float actualWeight,colorWeight,unlit;public bool enabled;public string caster;}
  [Serializable] class Record{public string file,sha256,pose;public int actualFrame,outputFrame;public float outputTime,blinkL,blinkR,openA,seal;public Layer[] layers;}
  [Serializable] class Evidence{public string status="LIVE_BLINK_AND_BASIC_MOUTH_DIAGNOSTIC_NOT_ART_ACCEPTANCE",importAuditSha256,runtimeSha256,builderSha256,shaderSha256,hlslSha256,unityVersion,graphicsApi;public Record[] captures,motion;public int motionFrames=150,outputFps=30;public float duration=5;public bool audioIncluded=false;public string limitation="Thirteen controlled eye layers only; static blue iris/backings remain. EyeBlink mesh weight and linear open/closed pigment use identical per-eye values. Original refinement shader switching is disabled and its selected receiver.05 state is applied to runtime clones. Motion is deterministic5-second rendered evidence, not reference-video tracking or phone performance. Full speech/emotions/gaze and final silhouette/style remain unaccepted.";}
  IEnumerator Start(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-renDesignerBlinkCapture");if(i>=0&&i+1<a.Length)BeginCapture(a[i+1]);yield break;}
  public void BeginCapture(string output)
  {
   if(!Application.isPlaying||capturing)throw new InvalidOperationException("Capture requires Play mode and one active capture only.");
   output=Path.GetFullPath(output);if(!output.Replace('\\','/').StartsWith("B:/lucid-loop/",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Capture output must remain under B:/lucid-loop.");
   if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new InvalidOperationException("Use a fresh capture folder; existing evidence is frozen.");
   Directory.CreateDirectory(output);capturing=true;IdleBlink=false;StartCoroutine(RunCapture(output));
  }
  IEnumerator RunCapture(string output){var routine=Capture(output);while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);FinishCapture(2);yield break;}yield return next;}}
  static void FinishCapture(int code)
  {
#if UNITY_EDITOR
   UnityEditor.EditorApplication.isPlaying=false;
#else
   Application.Quit(code);
#endif
  }
  IEnumerator Capture(string output)
  {
   var records=new List<Record>();var motion=new List<Record>();var poses=new[]{("rest",0f,0f,0f),("left-closed",1f,0f,0f),("right-closed",0f,1f,0f),("half",.5f,.5f,0f),("closed",1f,1f,0f),("mixed-partial",.35f,.65f,.5f),("mixed-closed",1f,1f,.5f),("return-rest",0f,0f,0f)};
   foreach(var view in new[]{"front","quarter","profile"})
   {
    Review.SourceCamera=false;Review.Yaw=view=="quarter"?45:view=="profile"?90:0;Review.LightMode=1;Review.Lit=true;Review.ShowCap=Review.ShowHair=true;
    foreach(var pose in poses){BlinkL=pose.Item2;BlinkR=pose.Item3;MouthContext.OpenA=pose.Item4;MouthContext.Seal=1;for(int frame=0;frame<16;frame++)yield return null;Validate();var file=pose.Item1+"--"+view+".png";Render(Path.Combine(output,file),1536);records.Add(Read(file,pose.Item1+"/"+view,output,-1,0));}
   }
   Review.Yaw=0;Review.Lit=false;MouthContext.OpenA=0;foreach(float w in new[]{0f,1f}){BlinkL=BlinkR=w;for(int frame=0;frame<16;frame++)yield return null;Validate();var file=(w==0?"rest":"closed")+"--unlit-front.png";Render(Path.Combine(output,file),1536);records.Add(Read(file,"unlit",output,-1,0));}
   BlinkL=BlinkR=0;MouthContext.OpenA=0;shaderControl=true;foreach(var row in materials)foreach(var m in row)m.shader=SelectedReceiverShader;
   foreach(bool lit in new[]{true,false}){Review.Lit=lit;for(int frame=0;frame<16;frame++)yield return null;Validate();var file="rest--original-shader-"+(lit?"front":"unlit-front")+".png";Render(Path.Combine(output,file),1536);records.Add(Read(file,"same imported geometry / original shader",output,-1,0));}
   shaderControl=false;foreach(var row in materials)foreach(var m in row)m.shader=BlinkShader;
   Review.Lit=true;Review.Yaw=15;var frames=Path.Combine(output,"motion-frames");Directory.CreateDirectory(frames);
   for(int frame=0;frame<150;frame++){float time=frame/30f;BlinkL=BlinkR=Mathf.Max(Pulse(time,.9f),Pulse(time,2.95f));var phase=Mathf.Clamp01((time-1.5f)/3);MouthContext.OpenA=.5f*Mathf.Sin(Mathf.PI*phase)*Mathf.Sin(Mathf.PI*phase);MouthContext.Seal=1;for(int wait=0;wait<3;wait++)yield return null;Validate();var file="motion-frames/"+frame.ToString("D4")+".png";Render(Path.Combine(output,file),960);motion.Add(Read(file,"motion",output,frame,time));}
   File.WriteAllText(Path.Combine(output,"BlinkLiveEvidence.json"),JsonUtility.ToJson(new Evidence{importAuditSha256=ImportAuditSha256,runtimeSha256=RuntimeSha256,builderSha256=BuilderSha256,shaderSha256=BlinkShaderSha256,hlslSha256=BlinkHlslSha256,unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),captures=records.ToArray(),motion=motion.ToArray()},true));Debug.Log("REN_DESIGNER_BLINK_CAPTURE_OK: "+output);FinishCapture(0);
  }
  void Validate(){if(Review.ModelCamera.rect!=new Rect(0,0,1,1)||Review.ModelCamera.orthographic||Vector3.Distance(Review.ModelCamera.transform.position,Review.Target+Quaternion.Euler(0,Review.Yaw,0)*(Vector3.forward*Review.Distance))>.0001f)throw new InvalidOperationException("Actual blink camera mismatch");for(int i=0;i<Controlled.Length;i++){var w=left[i]?BlinkL:BlinkR;if(!Controlled[i].enabled||Mathf.Abs(Controlled[i].GetBlendShapeWeight(keys[i])-100*w)>.00001f)throw new InvalidOperationException("Actual blink renderer weight mismatch");foreach(var m in materials[i])if(m.shader!=(shaderControl?SelectedReceiverShader:BlinkShader)||(!shaderControl&&Mathf.Abs(m.GetFloat("_EyeBlinkWeight")-w)>.000001f)||m.GetFloat("_UseVertexColor")!=1||m.GetFloat("_VertexColorSrgb")!=0||m.GetFloat("_UseBaseMap")!=0)throw new InvalidOperationException("Actual closed pigment shader/weight mismatch");}}
  Record Read(string file,string pose,string output,int frame,float time)=>new Record{file=file,sha256=Hash(Path.Combine(output,file)),pose=pose,actualFrame=Time.frameCount,outputFrame=frame,outputTime=time,blinkL=BlinkL,blinkR=BlinkR,openA=MouthContext.OpenA,seal=MouthContext.Seal,layers=Controlled.Select((r,i)=>new Layer{renderer=r.name,key=r.sharedMesh.GetBlendShapeName(keys[i]),actualWeight=r.GetBlendShapeWeight(keys[i]),colorWeight=shaderControl?0:materials[i][0].GetFloat("_EyeBlinkWeight"),unlit=materials[i][0].GetFloat("_Unlit"),shader=materials[i][0].shader.name,enabled=r.enabled,caster=r.shadowCastingMode.ToString()}).ToArray()};
  void Render(string path,int size){var camera=Review.ModelCamera;var target=RenderTexture.GetTemporary(size,size,24,RenderTextureFormat.ARGB32);var prior=camera.targetTexture;var active=RenderTexture.active;try{camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(size,size,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size,size),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);}finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);}}
  void OnGUI(){var prior=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*Mathf.Min(Screen.width/1600f,Screen.height/900f));GUI.Box(new Rect(25,745,710,45),GUIContent.none);GUI.Label(new Rect(35,752,50,25),"Blink L");BlinkL=GUI.HorizontalSlider(new Rect(90,760,170,20),BlinkL,0,1);GUI.Label(new Rect(285,752,50,25),"Blink R");BlinkR=GUI.HorizontalSlider(new Rect(345,760,170,20),BlinkR,0,1);IdleBlink=GUI.Toggle(new Rect(550,752,165,25),IdleBlink,"Idle blink");GUI.matrix=prior;}
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
