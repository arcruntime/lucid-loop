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
    public sealed class RenDesignerRefinementReview:MonoBehaviour
    {
        public RenDesignerEyeReview Review;
        public string RuntimeSha256,BuilderSha256,ReferenceSha256;
        public Texture2D CleanHairBase,CleanHairShadow; public Renderer InterfacePatch; public bool CleanHair,PatchVisible; public string PatchAuditSha256,HairBaseSha256,HairShadowSha256; sealed class HairBinding{public Material material;public Texture originalBase,originalShadow;} readonly List<HairBinding> hairBindings=new List<HairBinding>(); public Material[] CandidateVariants; public int Mode; public Shader OriginalShader,CandidateShader; public string CandidateHlslSha256;
        sealed class Binding{public string renderer;public Material material;public float strength,faceStrength;}
        readonly List<Binding> bindings=new List<Binding>();
        void StartBinding()
        {
            if(bindings.Count>0)return;
            foreach(var r in new[]{Review.Head}.Concat(Review.NewEyes.Where(r=>r.name.EndsWith("SkinShutter",StringComparison.Ordinal))))
                foreach(var m in r.sharedMaterials)bindings.Add(new Binding{renderer=r.name,material=m,strength=m.GetFloat("_ShadowStrength"),faceStrength=m.GetFloat("_FaceCastShadowStrength")});
            foreach(var r in Review.Hair)foreach(var m in r.sharedMaterials)if(!hairBindings.Any(h=>h.material==m))hairBindings.Add(new HairBinding{material=m,originalBase=m.GetTexture("_BaseMap"),originalShadow=m.GetTexture("_ShadowMap")});
            if(bindings.Count!=5)throw new InvalidOperationException("Expected three head slots plus two shutters.");
        }
        void LateUpdate()
        {
            StartBinding();foreach(var b in bindings){b.material.shader=Mode==0?OriginalShader:CandidateShader;b.material.SetFloat("_ShadowStrength",b.strength);b.material.SetFloat("_FaceCastShadowStrength",Mode==2?.05f:b.faceStrength);}foreach(var h in hairBindings){h.material.SetTexture("_BaseMap",CleanHair?CleanHairBase:h.originalBase);h.material.SetTexture("_ShadowMap",CleanHair?CleanHairShadow:h.originalShadow);}InterfacePatch.enabled=PatchVisible;
        }
        [Serializable] sealed class Value{public string renderer,shader;public float shadowStrength,faceCastShadowStrength,faceMode,unlit;}
        [Serializable] sealed class Record{public string file,sha256,pose;public int mode;public bool cleanHair,patchVisible;public Value[] readback;public string[] hairBaseMaps,hairShadowMaps;}
        [Serializable] sealed class Evidence
        {
            public string status="INDEPENDENT_REFINEMENT_CANDIDATES_NOT_SHADING_ACCEPTANCE",eyeSha256,headSha256,runtimeSha256,builderSha256,referenceSha256,unityVersion,graphicsApi;
            public bool unchangedEyeGeometry=true,unchangedHistoricalHead=true,outlineOff=true,referenceNpotNone=true;public string candidateHlslSha256,patchAuditSha256,hairBaseSha256,hairShadowSha256;
            public float[] sourcePixelCrop={275,25,430,400};public int cropDisplayScale=2;
            public Record[] captures;
            public string limits="Three modes isolate original shadow classification, R-only classification with original .15 face strength, and R-only classification with selected .05 face strength on head3slots and shutters2. Diffuse response and shader values otherwise unchanged. Hair-only swaps matched clean pigment and derived shadow; patch-only adds verified static outer-eye notch patch; combined turns on all three candidates. Existing meshes, camera, lights, material shader parameters and other maps are fixed. Corrected support v2 remains constant, including its exposed lower-iris slit. Reference-only NPOT=None import fixes aspect; portrait pair samples identical source canvas crop and uniform2x scale, no XY warp. Source camera's existing mouth/chin residual ~18pixels remains. No final style, motion or phone result.";
        }
        IEnumerator Start()
        {
            var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,"-renDesignerRefinementCapture");if(i<0||i+1>=a.Length)yield break;
            var output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);var routine=Capture(output);
            while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Debug.LogException(e);Application.Quit(2);yield break;}yield return next;}
        }
        IEnumerator Capture(string output)
        {
            var records=new List<Record>();
            foreach(var pose in new[]{"neutral-front","neutral-quarter","neutral-profile","club-front","unlit-front","source-camera"})
            {
                Review.SourceCamera=pose=="source-camera";Review.Yaw=pose.EndsWith("quarter",StringComparison.Ordinal)?45:pose.EndsWith("profile",StringComparison.Ordinal)?90:0;Review.LightMode=pose.StartsWith("club",StringComparison.Ordinal)?2:1;Review.Lit=pose!="unlit-front";Review.ShowCap=Review.ShowHair=true;
                foreach(var variant in new[]{0,1,2,3,4,5})
                {
                    Mode=variant==1?1:variant==2||variant==5?2:0;CleanHair=variant==3||variant==5;PatchVisible=variant==4||variant==5;for(var frame=0;frame<12;frame++)yield return null;
                    foreach(var b in bindings)if(b.material.shader!=(Mode==0?OriginalShader:CandidateShader)||b.material.GetFloat("_ShadowStrength")!=b.strength||b.material.GetFloat("_FaceCastShadowStrength")!=(Mode==2?.05f:b.faceStrength))throw new InvalidOperationException("Stale receiver diagnostic state.");
                    var file=new[]{"baseline","receiver-classification","receiver-005","clean-hair","interface-patch","combined"}[variant]+"--"+pose+".png";Render(Path.Combine(output,file),false);records.Add(RecordOf(file,pose,output));
                    if(Review.SourceCamera){file=new[]{"baseline","receiver-classification","receiver-005","clean-hair","interface-patch","combined"}[variant]+"--source-portrait-paired.png";Render(Path.Combine(output,file),true);records.Add(RecordOf(file,"source-portrait-paired",output));}
                }
            }
            File.WriteAllText(Path.Combine(output,"RefinementLiveEvidence.json"),JsonUtility.ToJson(new Evidence{eyeSha256=Review.SourceSha256,headSha256="073e47cc05a2daea1db7c25152cc28a0c51b820c0a40153727f767285c4293ad",runtimeSha256=RuntimeSha256,builderSha256=BuilderSha256,referenceSha256=ReferenceSha256,candidateHlslSha256=CandidateHlslSha256,patchAuditSha256=PatchAuditSha256,hairBaseSha256=HairBaseSha256,hairShadowSha256=HairShadowSha256,unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),captures=records.ToArray()},true));
            Debug.Log("REN_DESIGNER_REFINEMENT_CAPTURE_OK: "+output);Application.Quit(0);
        }
        Record RecordOf(string file,string pose,string output)=>new Record{file=file,sha256=Hash(Path.Combine(output,file)),pose=pose,mode=Mode,cleanHair=CleanHair,patchVisible=InterfacePatch.enabled,hairBaseMaps=hairBindings.Select(h=>h.material.GetTexture("_BaseMap").name).ToArray(),hairShadowMaps=hairBindings.Select(h=>h.material.GetTexture("_ShadowMap").name).ToArray(),readback=bindings.Select(b=>new Value{renderer=b.renderer,shader=b.material.shader.name,shadowStrength=b.material.GetFloat("_ShadowStrength"),faceCastShadowStrength=b.material.GetFloat("_FaceCastShadowStrength"),faceMode=b.material.GetFloat("_FaceMode"),unlit=b.material.GetFloat("_Unlit")}).ToArray()};
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
        void OnGUI(){Mode=GUI.Toolbar(new Rect(25,Screen.height-65,650,30),Mode,new[]{"Original receiver","R classification","Face shadow .05"});CleanHair=GUI.Toggle(new Rect(700,Screen.height-65,220,30),CleanHair,"Clean hair map pair");PatchVisible=GUI.Toggle(new Rect(940,Screen.height-65,220,30),PatchVisible,"Outer-eye patch");GUI.Label(new Rect(25,Screen.height-30,1500,28),"Neutral construction review · independent candidate toggles · no blink/gaze/animation or final style acceptance");}
        static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    }
}
