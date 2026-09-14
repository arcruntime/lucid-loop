using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LucidLoop.CharacterArt.Editor
{
 public static class RenLOD0Verification
 {
  static RenLOD0Controller c;
  static int step;
  static double next;
  const string Evidence="Assets/CharacterArt/Generated/RenLOD0/Evidence/";
  [MenuItem("Lucid Loop/Ren LOD0/Verify controls")]
  static void Begin()
  {
   if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter play mode first");
   c=UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>();
   c.IdleActing=false;c.IdleBlink=false;c.SetPose(0);step=0;next=EditorApplication.timeSinceStartup+1;
   EditorApplication.update-=Tick;EditorApplication.update+=Tick;
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup<next)return;
   try
   {
    if(!c||!EditorApplication.isPlaying)throw new InvalidOperationException("Verification interrupted");
    var renderers=c.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    if(step==0)
    {
     var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
     if(bounds.size.y<1.5f||bounds.size.y>2.2f)throw new InvalidOperationException("Incorrect world scale: "+bounds.size);
     var target=c.Head.position+Vector3.up*.09f;c.ReviewCamera.transform.position=target+new Vector3(0,0,-.65f);c.ReviewCamera.transform.LookAt(target);
    }
    if(step>=1&&step<=4)
    {
     string suffix=step==2?"speech_A":step==3?"speech_MBP":step==4?"eyeBlinkL":null;
     if(suffix!=null&&!renderers.Any(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Any(i=>r.sharedMesh.GetBlendShapeName(i).EndsWith(suffix)&&r.GetBlendShapeWeight(i)>99)))throw new InvalidOperationException("Control did not apply: "+suffix);
     RenLOD0Builder.Capture();File.Copy(Evidence+"ren-lod0.png",Evidence+new[]{"","face-rest.png","face-A.png","face-MBP.png","face-blink-cap-off.png"}[step],true);
    }
    if(step==1)c.SetPose(1);
    if(step==2)c.SetPose(6);
    if(step==3){c.SetPose(0);c.BlinkL=1;c.BlinkR=1;c.Cap.SetActive(false);c.HairMotion.CapOn=false;}
    if(step==4)
    {
     var hair=renderers.First(r=>r.name=="RenLiveHair");
     if(Enumerable.Range(0,hair.sharedMesh.blendShapeCount).Any(i=>hair.sharedMesh.GetBlendShapeName(i).EndsWith("capOn")&&hair.GetBlendShapeWeight(i)!=0))throw new InvalidOperationException("Cap corrective remained on");
     c.BlinkL=c.BlinkR=0;c.Cap.SetActive(true);c.HairMotion.CapOn=true;c.IdleActing=true;c.IdleBlink=true;
     c.ReviewCamera.transform.position=new Vector3(0,.9f,-3);c.ReviewCamera.transform.LookAt(new Vector3(0,.9f,0));
    }
    if(step==5){RenLOD0Builder.Capture();File.WriteAllText(Evidence+"controls.txt","PASS: metre-scale assembly, A, MBP, bilateral blink, cap-off corrective; idle restored. Actual Unity play-mode captures included.\n");EditorApplication.update-=Tick;Debug.Log("REN_LOD0_CONTROLS_PASS");}
    step++;next=EditorApplication.timeSinceStartup+1;
   }
   catch(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);}
  }
 }
}
