using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenCompleteVerification
    {
        const string Root="Assets/CharacterArt/Generated/RenLOD0/Evidence/";
        static RenLOD0Controller actor;
        static double next;
        static int step;
        static Vector3 initialHand;
        static Transform hand;

        [MenuItem("Lucid Loop/Ren LOD0/Verify complete acting and LODs")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play the RenLOD0 viewer first");
            actor=UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>();
            if(!actor||!actor.ReviewCamera||!actor.CharacterLODs||!actor.GuardedPose)throw new InvalidOperationException("Complete viewer dependencies missing");
            actor.ShowControls=false;actor.IdleActing=false;actor.IdleBlink=false;
            actor.SetPose(0);actor.BlinkL=actor.BlinkR=0;actor.Cap.SetActive(true);
            actor.CharacterLODs.ForceLOD(0);
            var target=new Vector3(0,1.3f,0);
            actor.ReviewCamera.transform.position=target+new Vector3(0,.02f,-1.65f);actor.ReviewCamera.transform.LookAt(target);
            hand=actor.LOD0.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LeftHand");
            step=0;next=EditorApplication.timeSinceStartup+1.6;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                var levels=actor.CharacterLODs.GetLODs();
                if(levels.Length!=2)throw new InvalidOperationException("Expected two LODs");
                foreach(var level in levels)
                {
                    var bounds=new Bounds();bool first=true;
                    foreach(var renderer in level.renderers){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
                    if(bounds.size.y<1.3f||bounds.size.y>2.5f||bounds.size.x>2.5f)throw new InvalidOperationException("Character scale/bounds invalid: "+bounds);
                }
                string label=new[]{"neutral-lod0","guarded-lod0","guarded-lod1","speech-blink-lod1","speech-blink-lod0","restored-lod0"}[step];
                if(step==0)initialHand=hand.position;
                if(step==1||step==2)
                {
                    if(Vector3.Distance(initialHand,hand.position)<.15f)throw new InvalidOperationException("Guarded hand failed to animate");
                    if(Vector3.Distance(hand.position,actor.Head.position)>.45f)throw new InvalidOperationException("Guarded hand is too far from face");
                }
                if(step==3||step==4)
                {
                    int index=step==3?1:0;
                    var skins=levels[index].renderers.OfType<SkinnedMeshRenderer>().ToArray();
                    float Max(string suffix)=>skins.SelectMany(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Where(i=>r.sharedMesh.GetBlendShapeName(i).EndsWith(suffix,StringComparison.OrdinalIgnoreCase)).Select(r.GetBlendShapeWeight)).DefaultIfEmpty(0).Max();
                    if(Max("speech_A")<99||Max("eyeBlinkL")<99)throw new InvalidOperationException("Speech/blink did not reach LOD "+index);
                    if(actor.LOD1Cap.activeSelf)throw new InvalidOperationException("LOD1 cap toggle did not follow LOD0");
                }
                RenLOD0Builder.Capture();File.Copy(Root+"ren-lod0.png",Root+label+".png",true);
                if(step==0){actor.SetPose(18);next=EditorApplication.timeSinceStartup+1.7;}
                if(step==1){actor.CharacterLODs.ForceLOD(1);next=EditorApplication.timeSinceStartup+.5;}
                if(step==2){actor.SetPose(1);actor.BlinkL=1;actor.Cap.SetActive(false);next=EditorApplication.timeSinceStartup+1.7;}
                if(step==3){actor.CharacterLODs.ForceLOD(0);next=EditorApplication.timeSinceStartup+.5;}
                if(step==4){actor.SetPose(0);actor.BlinkL=0;actor.Cap.SetActive(true);next=EditorApplication.timeSinceStartup+1.7;}
                if(step==5)
                {
                    if(Vector3.Distance(initialHand,hand.position)>.001f)throw new InvalidOperationException("Guarded hand did not restore neutral");
                    File.WriteAllText(Root+"complete-acting.txt","PASS: actual Play frames checked both LOD bounds, guarded enter/hold/exit, shared hand motion, speech A + independent blink on both LODs, synchronized cap toggle and exact hand return. Visual captures require separate art review.\n");
                    Restore();EditorApplication.update-=Tick;
                }
                step++;
            }
            catch(Exception e){EditorApplication.update-=Tick;Restore();Debug.LogException(e);}
        }
        static void Restore()
        {
            if(!actor)return;
            actor.SetPose(0);actor.BlinkL=actor.BlinkR=0;actor.Cap.SetActive(true);
            actor.IdleActing=true;actor.IdleBlink=true;actor.ShowControls=true;actor.CharacterLODs.ForceLOD(-1);
            actor.ReviewCamera.transform.position=new Vector3(0,.9f,-3);actor.ReviewCamera.transform.LookAt(new Vector3(0,.9f,0));
        }
    }
}
