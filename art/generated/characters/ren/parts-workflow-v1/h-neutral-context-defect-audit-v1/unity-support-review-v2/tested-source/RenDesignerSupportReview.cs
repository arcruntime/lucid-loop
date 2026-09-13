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
    [DefaultExecutionOrder(500)]
    public sealed class RenDesignerSupportReview:MonoBehaviour
    {
        public RenDesignerEyeReview Review;
        public string RuntimeSha256,BuilderSha256,ReferenceSha256;
        public bool CorrectedSupport=true;
        public Renderer OldSupport;
        public Renderer[] NewSupport;
        public string SupportAuditSha256;
        sealed class Binding{public string renderer;public Material material;public float strength,faceStrength;}
        readonly List<Binding> bindings=new List<Binding>();
        void LateUpdate()
        {
            if(bindings.Count==0)
                foreach(var r in new[]{Review.Head,OldSupport}.Concat(NewSupport).Concat(Review.NewEyes.Where(r=>r.name.EndsWith("SkinShutter",StringComparison.Ordinal))))
                    foreach(var m in r.sharedMaterials)bindings.Add(new Binding{renderer=r.name,material=m,strength=m.GetFloat("_ShadowStrength"),faceStrength=m.GetFloat("_FaceCastShadowStrength")});
            OldSupport.enabled=!CorrectedSupport;foreach(var r in NewSupport)r.enabled=CorrectedSupport;
        }
        [Serializable] sealed class Value{public string renderer;public float shadowStrength,faceCastShadowStrength,faceMode,unlit;}
        [Serializable] sealed class Caster{public string renderer,mode;public bool enabled;}
        [Serializable] sealed class Record{public string file,sha256,pose;public bool correctedSupport;public Value[] readback;public Caster[] casters;}
        [Serializable] sealed class Evidence
        {
            public string status="SUPPORT_BRIDGE_GEOMETRY_DIAGNOSTIC_NOT_STYLE_ACCEPTANCE",eyeSha256,headSha256,runtimeSha256,builderSha256,referenceSha256,unityVersion,graphicsApi;
            public bool unchangedEyeGeometry=true,unchangedHistoricalHead=true,outlineOff=true,noSupportBridge=false,referenceNpotNone=true;
            public string supportAuditSha256;
            public float[] sourcePixelCrop={275,25,430,400};public int cropDisplayScale=2;
            public Record[] captures;
            public string limits="Only old support visibility versus independently imported source support+bridge changes. Historical head/eye/hair/cap geometry, material presets and receiving/casting remain baseline. New support pigment is exact source linear color; no source morph restoration. Canonical import/neutral boundary checks live in referenced support audit. No controls or phone performance claim.";
        }
        IEnumerator Start()
        {
            var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,"-renDesignerSupportCapture");if(i<0||i+1>=a.Length)yield break;
            var output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);var routine=Capture(output);
            while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);Application.Quit(2);yield break;}yield return next;}
        }
        IEnumerator Capture(string output)
        {
            var records=new List<Record>();
            foreach(var pose in new[]{"neutral-front","neutral-quarter","neutral-profile","neutral-opposite-profile","hair-hidden-profile","club-front","unlit-front","source-camera"})
            {
                Review.SourceCamera=pose=="source-camera";Review.Yaw=pose.EndsWith("quarter",StringComparison.Ordinal)?45:pose=="neutral-opposite-profile"?-90:pose.EndsWith("profile",StringComparison.Ordinal)?90:0;Review.LightMode=pose.StartsWith("club",StringComparison.Ordinal)?2:1;Review.Lit=pose!="unlit-front";Review.ShowCap=Review.ShowHair=pose!="hair-hidden-profile";
                foreach(var shadows in new[]{true,false})
                {
                    CorrectedSupport=!shadows;for(var frame=0;frame<12;frame++)yield return null;
                    if(OldSupport.enabled==CorrectedSupport||NewSupport.Any(r=>r.enabled!=CorrectedSupport))throw new InvalidOperationException("Stale support variant visibility.");
                    var file=(shadows?"old-support":"support-with-bridge")+"--"+pose+".png";Render(Path.Combine(output,file),false);records.Add(RecordOf(file,pose,output));
                    if(Review.SourceCamera){file=(shadows?"old-support":"support-with-bridge")+"--source-portrait-paired.png";Render(Path.Combine(output,file),true);records.Add(RecordOf(file,"source-portrait-paired",output));}
                }
            }
            File.WriteAllText(Path.Combine(output,"SupportLiveEvidence.json"),JsonUtility.ToJson(new Evidence{eyeSha256=Review.SourceSha256,headSha256="073e47cc05a2daea1db7c25152cc28a0c51b820c0a40153727f767285c4293ad",runtimeSha256=RuntimeSha256,builderSha256=BuilderSha256,referenceSha256=ReferenceSha256,supportAuditSha256=SupportAuditSha256,unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),captures=records.ToArray()},true));
            Debug.Log("REN_DESIGNER_SUPPORT_CAPTURE_OK: "+output);Application.Quit(0);
        }
        Record RecordOf(string file,string pose,string output)=>new Record{file=file,sha256=Hash(Path.Combine(output,file)),pose=pose,correctedSupport=CorrectedSupport,casters=Review.ModelRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Select(r=>new Caster{renderer=r.name,mode=r.shadowCastingMode.ToString(),enabled=r.enabled}).ToArray(),readback=bindings.Select(b=>new Value{renderer=b.renderer,shadowStrength=b.material.GetFloat("_ShadowStrength"),faceCastShadowStrength=b.material.GetFloat("_FaceCastShadowStrength"),faceMode=b.material.GetFloat("_FaceMode"),unlit=b.material.GetFloat("_Unlit")}).ToArray()};
        void Render(string path,bool cropPair)
        {
            var camera=Review.ModelCamera;var scene=RenderTexture.GetTemporary(1536,Review.SourceCamera?1024:1536,24,RenderTextureFormat.ARGB32);var prior=camera.targetTexture;var active=RenderTexture.active;RenderTexture pair=null;
            try
            {
                camera.targetTexture=scene;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=scene});
                if(cropPair)
                {
                    pair=RenderTexture.GetTemporary(1720,800,0,RenderTextureFormat.ARGB32);RenderTexture.active=pair;GL.Clear(true,true,Color.black);GL.PushMatrix();GL.LoadPixelMatrix(0,1720,800,0);
                    var uv=new Rect(275f/1536,1-425f/1024,430f/1536,400f/1024);
                    Graphics.DrawTexture(new Rect(0,0,860,800),scene,uv,0,0,0,0);
                    Graphics.DrawTexture(new Rect(860,0,860,800),Review.Sketch,uv,0,0,0,0);GL.PopMatrix();
                }
                var target=pair?pair:scene;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
            }
            finally{camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(scene);if(pair)RenderTexture.ReleaseTemporary(pair);}
        }
        void OnGUI(){GUI.Label(new Rect(25,Screen.height-35,1500,30),"Support + bridge geometry diagnostic / neutral eyes / not style acceptance");}
        static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    }
}
