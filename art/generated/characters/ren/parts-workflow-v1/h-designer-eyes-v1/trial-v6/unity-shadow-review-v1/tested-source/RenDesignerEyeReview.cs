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
    public sealed class RenDesignerEyeReview : MonoBehaviour
    {
        public Camera ModelCamera;
        public Transform ModelRoot, FaceFrame;
        public Renderer Head, Cap;
        public Renderer[] OldEyes, NewEyes, Hair;
        public Material[] Materials, GraphicMaterials;
        public Texture2D Artist, Sketch;
        public UniversalRenderPipelineAsset Pipeline;
        public Light[] NeutralLights, ClubLights;
        public Vector3 Target = new Vector3(0,.825f,0);
        public float Distance = 4, FieldOfView = 28, Yaw;
        public string SourceSha256, ImportAuditSha256, RuntimeSha256;
        public bool Lit = true, ShowCap = true, ShowHair = true;
        public int LightMode = 1;
        public bool SourceCamera;
        public Vector3 SourceCameraPosition, SourceCameraTarget, SourceCameraUp = Vector3.up;
        public float SourceCameraOrthoSize = .45f;
        RenderPipelineAsset prior, priorQuality;
        Bounds faceBounds;
        RenderTexture display;
        readonly Dictionary<Material,Material> copies = new Dictionary<Material,Material>();
        readonly List<(Renderer renderer,Material[] source)> originalSlots = new List<(Renderer,Material[])>();
        GUIStyle label,small,button;
        void Awake()
        {
            prior=GraphicsSettings.defaultRenderPipeline;priorQuality=QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline=Pipeline;QualitySettings.renderPipeline=Pipeline;
            faceBounds=RenNprReviewController.CalculateFaceLocalBounds(FaceFrame,Head);
            foreach(var r in ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var source=r.sharedMaterials;originalSlots.Add((r,source));
                r.sharedMaterials=source.Select(m=>{if(!copies.TryGetValue(m,out var clone)){clone=new Material(m){hideFlags=HideFlags.DontSave};copies.Add(m,clone);}return clone;}).ToArray();
            }
            Materials=Materials.Select(m=>copies[m]).Distinct().ToArray();GraphicMaterials=GraphicMaterials.Select(m=>copies[m]).Distinct().ToArray();
            foreach(var r in OldEyes) r.enabled=false;
            display=new RenderTexture(900,900,24,RenderTextureFormat.ARGB32);display.Create();ModelCamera.targetTexture=display;ModelCamera.rect=new Rect(0,0,1,1);ModelCamera.aspect=1;
            Application.runInBackground=true;Apply();
        }
        void LateUpdate(){Apply();}
        void Apply()
        {
            foreach(var r in OldEyes) if(r.enabled) throw new InvalidOperationException("Rejected eye renderer enabled.");
            foreach(var l in NeutralLights)if(l)l.gameObject.SetActive(LightMode!=2);
            foreach(var l in ClubLights)if(l)l.gameObject.SetActive(LightMode==2);
            Cap.enabled=ShowCap;
            foreach(var r in Hair)
            {
                r.enabled=ShowHair;
                if(r is SkinnedMeshRenderer skin){var i=skin.sharedMesh.GetBlendShapeIndex("capOn");if(i>=0)skin.SetBlendShapeWeight(i,ShowCap?100:0);}
            }
            foreach(var m in Materials)
            {
                RenNprReviewController.SetFaceFrame(m,FaceFrame,faceBounds);
                m.SetFloat("_Unlit",!Lit||GraphicMaterials.Contains(m)?1:0);
            }
            if(SourceCamera)
            {
                ModelCamera.aspect=1.5f;
                ModelCamera.orthographic=true;ModelCamera.orthographicSize=SourceCameraOrthoSize;
                ModelCamera.transform.position=SourceCameraPosition;ModelCamera.transform.LookAt(SourceCameraTarget,SourceCameraUp);
            }
            else
            {
                ModelCamera.aspect=1;
                ModelCamera.orthographic=false;ModelCamera.fieldOfView=FieldOfView;
                ModelCamera.transform.position=Target+Quaternion.Euler(0,Yaw,0)*(Vector3.forward*Distance);ModelCamera.transform.LookAt(Target);
            }
        }
        [Serializable] sealed class Record { public string file,sha256,pose;public bool lit,cap,hair;public int lightMode,frame; }
        [Serializable] sealed class Evidence
        {
            public string status="NEUTRAL_DESIGNER_EYE_V6_MATERIAL_REVIEW_NOT_LIKENESS_APPROVAL",unityVersion,graphicsApi,sourceSha256,importAuditSha256,runtimeSha256;
            public bool neutralOnly=true,oldEyesDisabled=true,outlineOff=true,closedLipPaintCandidate=true,vertexColorConsumedOnce=true;
            public Record[] captures;
            public string limits="New eyes contain no controls. No blink/gaze/lipsync, canonical morph repair, production rig or phone performance result. Original H geometry and historical import remain unchanged. Graphic eye layers retain unlit pigment in lit mode; skin shutters use Tokon skin response with explicit fallback R1/G0/B0/A1, no old eye atlas.";
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();var i=Array.IndexOf(args,"-renDesignerCapture");if(i<0||i+1>=args.Length)yield break;
            var output=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(output);
            var routine=Capture(output);
            while(true)
            {
                object next;try{if(!routine.MoveNext())break;next=routine.Current;}
                catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);Application.Quit(2);yield break;}
                yield return next;
            }
        }
        IEnumerator Capture(string output)
        {
            var records=new List<Record>();
            foreach(var pose in new[]{"front","quarter","profile","profile-opposite","source-camera","cap-off-quarter","hair-hidden-profile","club-front"})
            {
                SourceCamera=pose=="source-camera";Yaw=pose.Contains("quarter")?45:pose=="profile-opposite"?-90:pose.Contains("profile")?90:0;
                ShowCap=!pose.StartsWith("cap-off")&&!pose.StartsWith("hair-hidden");ShowHair=!pose.StartsWith("hair-hidden");LightMode=pose.StartsWith("club")?2:1;
                foreach(var lit in new[]{false,true})
                {
                    Lit=lit;for(var f=0;f<10;f++)yield return null;
                    var path=Path.Combine(output,(lit?"tokon":"unlit")+"--"+pose+".png");SaveCamera(path);
                    records.Add(new Record{file=Path.GetFileName(path),sha256=Hash(path),pose=pose,lit=lit,cap=ShowCap,hair=ShowHair,lightMode=LightMode,frame=Time.frameCount});
                    if(SourceCamera&&Sketch){var paired=Path.Combine(output,(lit?"tokon":"unlit")+"--source-camera-paired.png");SavePaired(paired,Sketch);records.Add(new Record{file=Path.GetFileName(paired),sha256=Hash(paired),pose="source-camera-paired",lit=lit,cap=ShowCap,hair=ShowHair,lightMode=LightMode,frame=Time.frameCount});}
                }
            }
            SourceCamera=false;Yaw=0;ShowHair=ShowCap=Lit=true;LightMode=1;Apply();
            File.WriteAllText(Path.Combine(output,"LiveEvidence.json"),JsonUtility.ToJson(new Evidence{unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),sourceSha256=SourceSha256,importAuditSha256=ImportAuditSha256,runtimeSha256=RuntimeSha256,captures=records.ToArray()},true));
            Debug.Log("REN_DESIGNER_CAPTURE_OK: "+output);Application.Quit(0);
        }
        void SaveCamera(string path)
        {
            var target=RenderTexture.GetTemporary(1536,SourceCamera?1024:1536,24,RenderTextureFormat.ARGB32);var previous=ModelCamera.targetTexture;
            try{ModelCamera.targetTexture=target;RenderPipeline.SubmitRenderRequest(ModelCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});Read(target,path);}
            finally{ModelCamera.targetTexture=previous;RenderTexture.ReleaseTemporary(target);}
        }
        void SavePaired(string path,Texture2D reference)
        {
            var scene=RenderTexture.GetTemporary(1536,1024,24,RenderTextureFormat.ARGB32);var pair=RenderTexture.GetTemporary(3072,1024,0,RenderTextureFormat.ARGB32);
            var active=RenderTexture.active;var previous=ModelCamera.targetTexture;
            try
            {
                ModelCamera.targetTexture=scene;RenderPipeline.SubmitRenderRequest(ModelCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=scene});
                RenderTexture.active=pair;GL.Clear(true,true,new Color(.06f,.06f,.07f));GL.PushMatrix();GL.LoadPixelMatrix(0,3072,1024,0);
                Graphics.DrawTexture(new Rect(0,0,1536,1024),scene);
                var width=1536f;var height=width*reference.height/reference.width;if(height>1024){height=1024;width=height*reference.width/reference.height;}
                Graphics.DrawTexture(new Rect(1536+(1536-width)/2,(1024-height)/2,width,height),reference);GL.PopMatrix();Read(pair,path);
            }
            finally{ModelCamera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(scene);RenderTexture.ReleaseTemporary(pair);}
        }
        static void Read(RenderTexture target,string path)
        {
            var prior=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try{texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{RenderTexture.active=prior;Destroy(texture);}
        }
        void OnGUI()
        {
            if(label==null){label=new GUIStyle(GUI.skin.label){fontSize=23,wordWrap=true};small=new GUIStyle(label){fontSize=16};button=new GUIStyle(GUI.skin.button){fontSize=17};}
            var prior=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*Mathf.Min(Screen.width/1600f,Screen.height/900f));
            GUI.Box(new Rect(0,0,1600,900),GUIContent.none);GUI.Label(new Rect(25,15,1550,42),"Ren / designer-eye v6 neutral construction review",label);
            GUI.Label(new Rect(25,59,1550,32),"Selected Tokon face + cap maps. Closed-lip paint candidate. New eyes have no blink, gaze or speech controls.",small);
            GUI.DrawTexture(new Rect(25,125,710,710),display,ScaleMode.ScaleToFit);if(Artist)GUI.DrawTexture(new Rect(790,125,780,710),Artist,ScaleMode.ScaleToFit);
            GUI.Label(new Rect(790,96,780,26),"Untouched character designer reference",small);
            if(GUI.Button(new Rect(25,93,100,28),"Front",button)){Yaw=0;SourceCamera=false;}
            if(GUI.Button(new Rect(133,93,100,28),"Quarter",button)){Yaw=45;SourceCamera=false;}
            if(GUI.Button(new Rect(241,93,100,28),"Profile",button)){Yaw=90;SourceCamera=false;}
            Lit=GUI.Toggle(new Rect(349,93,115,28),Lit,"Tokon lit",button);ShowCap=GUI.Toggle(new Rect(472,93,100,28),ShowCap,"Cap",button);ShowHair=GUI.Toggle(new Rect(580,93,100,28),ShowHair,"Hair",button);
            GUI.Label(new Rect(25,852,1540,36),"Neutral geometry + material diagnostic only. Artist likeness, expressions and device performance remain unapproved.",small);GUI.matrix=prior;
        }
        void OnDestroy()
        {
            foreach(var entry in originalSlots)if(entry.renderer)entry.renderer.sharedMaterials=entry.source;
            foreach(var m in copies.Values)Destroy(m);
            if(GraphicsSettings.defaultRenderPipeline==Pipeline)GraphicsSettings.defaultRenderPipeline=prior;if(QualitySettings.renderPipeline==Pipeline)QualitySettings.renderPipeline=priorQuality;
            if(display){display.Release();Destroy(display);}
        }
        static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    }
}
