using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenBlinkMaterialRepair
    {
        const string Root="Assets/CharacterArt/Generated/RenLOD0";
        const string Evidence=Root+"/Evidence/body-artifacts";
        [Serializable] sealed class Manifest { public Entry[] materials; }
        [Serializable] sealed class Entry { public string name; public Eyes closedEye; }
        [Serializable] sealed class Eyes { public string color,mask; }
        static Dictionary<string,bool> Assignments(string source,string prefix)
        {
            var json=File.ReadAllText(source);if(json.TrimStart().StartsWith("["))json="{\"materials\":"+json+"}";
            return JsonUtility.FromJson<Manifest>(json).materials.ToDictionary(e=>prefix+e.name,e=>e.closedEye!=null&&!string.IsNullOrWhiteSpace(e.closedEye.color)&&!string.IsNullOrWhiteSpace(e.closedEye.mask));
        }
        static void Configure(Material material,Dictionary<string,bool> assignments)
        {
            string name=material.name.Replace(" (Instance)","").Replace("(Clone)","");
            if(!assignments.TryGetValue(name,out bool eyelids))return;
            material.SetFloat("_FaceLighting",eyelids||name.Contains("LipPatch")?1:0);
            if(eyelids)material.EnableKeyword("_CLOSED_EYE_CORRECTION");
            else
            {
                material.DisableKeyword("_CLOSED_EYE_CORRECTION");
                material.SetTexture("_ClosedEyeMap",null);material.SetTexture("_ClosedEyeMask",null);
                material.SetFloat("_ClosedEyeBlendL",0);material.SetFloat("_ClosedEyeBlendR",0);
            }
            EditorUtility.SetDirty(material);
        }
        [MenuItem("Lucid Loop/Ren LOD0/Repair blink material assignments")]
        public static void Repair()
        {
            var assignments=Assignments("../art/generated/characters/ren/lod0-final-v1/unity-materials.json","RenLOD0-");
            const string legacy="../art/generated/characters/ren/live-gym-face-v1/unity-materials.json";
            if(File.Exists(legacy))foreach(var pair in Assignments(legacy,"LiveRen-"))assignments[pair.Key]=pair.Value;
            var folders=new[]{Root+"/Materials","Assets/CharacterArt/Generated/RenLiveGymFace/Materials"}.Where(AssetDatabase.IsValidFolder).ToArray();
            foreach(var guid in AssetDatabase.FindAssets("t:Material",folders))
                Configure(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)),assignments);
            if(EditorApplication.isPlaying)
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    foreach(var material in renderer.sharedMaterials)if(material)Configure(material,assignments);
            AssetDatabase.SaveAssets();
            int corrected=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(!assignments.TryGetValue(material.name,out bool expected))continue;
                if(material.IsKeywordEnabled("_CLOSED_EYE_CORRECTION")!=expected)throw new InvalidOperationException("Incorrect eyelid mapping: "+material.name);
                if(expected)corrected++;
            }
            if(corrected!=1)throw new InvalidOperationException("Expected exactly one head-skin material with eyelid correction");
            Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/blink-materials.txt","PASS: exactly one authored head-skin material uses eyelid correction; body/clothes/accessories have no eyelid maps or keyword. Optional JSON objects require populated color AND mask paths.\n");
        }
        static RenLOD0Controller actor;static int step;static double next;
        [MenuItem("Lucid Loop/Ren LOD0/Verify blink does not repaint body")]
        static void Verify()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play RenLOD0 first");
            actor=UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>();
            actor.ShowControls=false;actor.IdleActing=false;actor.IdleBlink=false;actor.MovingLights=false;
            actor.HairMotion.MotionEnabled=false;actor.SetPose(0);actor.BlinkL=actor.BlinkR=0;
            actor.HeadTurn=actor.HeadTilt=actor.HeadRoll=0;actor.CharacterLODs.ForceLOD(0);
            actor.ReviewCamera.transform.position=new Vector3(0,.95f,-2.7f);actor.ReviewCamera.transform.LookAt(new Vector3(0,.85f,0));
            step=0;next=EditorApplication.timeSinceStartup+2;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                Directory.CreateDirectory(Evidence);RenLOD0Builder.Capture();
                string[] names={"blink-before-open","blink-before-closed","blink-fixed-open","blink-fixed-closed","blink-fixed-lod1-open","blink-fixed-lod1-closed"};
                string destination=Evidence+"/"+names[step]+".png";
                // Preserve the original reproducer when rerunning the fixed regression.
                if(step>=2||!File.Exists(destination))File.Copy(Root+"/Evidence/ren-lod0.png",destination,true);
                if(step==0)actor.BlinkL=actor.BlinkR=1;
                if(step==1){Repair();actor.BlinkL=actor.BlinkR=0;}
                if(step==2)actor.BlinkL=actor.BlinkR=1;
                if(step==3){actor.CharacterLODs.ForceLOD(1);actor.BlinkL=actor.BlinkR=0;}
                if(step==4)actor.BlinkL=actor.BlinkR=1;
                if(step==5)
                {
                    foreach(string prefix in new[]{"blink-fixed","blink-fixed-lod1"})
                    {
                        var a=new Texture2D(2,2);var b=new Texture2D(2,2);
                        try
                        {
                            a.LoadImage(File.ReadAllBytes(Evidence+"/"+prefix+"-open.png"));b.LoadImage(File.ReadAllBytes(Evidence+"/"+prefix+"-closed.png"));
                            var av=a.GetPixels32();var bv=b.GetPixels32();int pixels=a.width*(int)(a.height*.62f);
                            int changed=Enumerable.Range(0,pixels).Count(i=>!av[i].Equals(bv[i]));
                            if(changed!=0)throw new InvalidOperationException(prefix+": blink changed "+changed+" lower-body pixels");
                        }
                        finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
                    }
                    File.WriteAllText(Evidence+"/blink-body-render.txt","PASS: fixed LOD0 and LOD1 open/closed blink captures have zero changed pixels in the bottom62% (thighs/lower legs included). Full images retained to confirm eyelids still close.\n");
                    Stop();Debug.Log("REN_BLINK_BODY_RENDER_PASS");return;
                }
                step++;next=EditorApplication.timeSinceStartup+1;
            }
            catch(Exception error){Stop();Debug.LogException(error);}
        }
        static void Stop(){EditorApplication.update-=Tick;if(!actor)return;actor.BlinkL=actor.BlinkR=0;actor.IdleActing=true;actor.IdleBlink=true;actor.ShowControls=true;actor.HairMotion.MotionEnabled=true;actor.CharacterLODs.ForceLOD(0);}
    }
}
