using System;
using System.IO;
using System.Linq;
using LucidLoop.Gyms;
using LucidLoop.LiveSpeech;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.CharacterArt.Editor
{
 public static class RenLOD0LiveGymBuilder
 {
  [MenuItem("Lucid Loop/Ren LOD0/Open Live gym")]
  public static void Open()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)
    throw new InvalidOperationException("Exit Play and save the current scene first");
   EditorSceneManager.OpenScene("Assets/Gyms/Scenes/LiveGym.unity");
   EditorApplication.ExecuteMenuItem("Window/General/Game");
   EditorApplication.isPlaying=true;
  }
  [MenuItem("Lucid Loop/Ren LOD0/Install into Live gym")]
  public static void Build()
  {
   if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Exit Play and save the current scene first");
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterArt/Generated/RenLOD0/Prefabs/RenLOD0.prefab");
   if(!prefab)throw new InvalidOperationException("Save the Ren gameplay prefab first");
   var scene=EditorSceneManager.OpenScene("Assets/Gyms/Scenes/LiveGym.unity");
   var live=UnityEngine.Object.FindFirstObjectByType<LiveGym>();
   var actor=live.Characters.Single(c=>c.Id=="ren");
   if(actor.Visual)actor.Visual.gameObject.SetActive(false);
   foreach(var name in new[]{"Ren live face","Ren complete character"})
   {var old=actor.transform.Find(name);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);}
   var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.name="Ren complete character";
   model.transform.SetParent(actor.transform,false);
   // Actor faces +Z; the viewer prefab's imported model faces -Z.
   model.transform.localRotation=Quaternion.Euler(0,180,0);
   actor.Visual=model.transform;actor.Mouth=null;
   var controller=model.GetComponent<RenLOD0Controller>();controller.ShowControls=false;
   var speech=model.AddComponent<RenLiveSpeechFaceAdapter>();
   speech.Renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);speech.FaceController=controller;
   live.RenSpeechFace=speech;
   var anchor=new GameObject("Conversation face anchor");anchor.transform.SetParent(controller.Head,false);
   anchor.transform.position=controller.Head.position+Vector3.up*.09f;
   actor.ConversationFaceAnchor=anchor.transform;actor.ConversationVerticalOffset=0;
   actor.ConversationSize=.36f;actor.ConversationHorizontalOffset=.16f;
   live.Characters=new[]{actor}.Concat(live.Characters.Where(c=>c!=actor)).ToArray();
   foreach(var character in live.Characters)character.gameObject.SetActive(character==actor);
   live.Rig.Target=actor;live.Rig.Immediate=true;live.Rig.Apply(1);live.Rig.Immediate=false;
   EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   Debug.Log("REN_LOD0_LIVE_GYM_READY");
  }
  static int step;static double next;static LiveGym gym;
  [MenuItem("Lucid Loop/Ren LOD0/Capture Live gym")]
  static void Capture()
  {
   var live=UnityEngine.Object.FindFirstObjectByType<LiveGym>();
   if(!live)throw new InvalidOperationException("Open LiveGym");
   var rt=new RenderTexture(1600,900,24);var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
   try
   {
    RenderPipeline.SubmitRenderRequest(live.Rig.Camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
    RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
    File.WriteAllBytes("Assets/CharacterArt/Generated/RenLOD0/Evidence/live-gym.png",tex.EncodeToPNG());
   }
   finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
  }
  [MenuItem("Lucid Loop/Ren LOD0/Verify Live gym face ownership")]
  static void Verify()
  {
   if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play LiveGym first");
   gym=UnityEngine.Object.FindFirstObjectByType<LiveGym>();
   if(!gym||gym.IsReady)throw new InvalidOperationException("Open disconnected LiveGym");
   var c=gym.RenSpeechFace.FaceController;if(!c)throw new InvalidOperationException("Missing unified face controller");
   c.BlinkL=1;c.BlinkR=1;c.SetPose(14);
   var frame=new float[15];frame[10]=1;gym.RenSpeechFace.ApplyCanonicalWeights(frame);
   step=0;next=EditorApplication.timeSinceStartup+1;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup<next)return;
   try
   {
    var adapter=gym.RenSpeechFace;var c=adapter.FaceController;
    float Weight(string suffix)=>adapter.Renderers.SelectMany(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Where(i=>r.sharedMesh.GetBlendShapeName(i).EndsWith(suffix)).Select(i=>r.GetBlendShapeWeight(i))).DefaultIfEmpty(-1).Max();
    if(step<3&&Weight("eyeBlinkL")<99)throw new InvalidOperationException("Speech overwrote blink");
    if(step==0)
    {
     if(Weight("speech_A")<99)throw new InvalidOperationException("Controller overwrote speech A");
     Capture();
     var frame=new float[15];frame[1]=1;frame[10]=1;adapter.ApplyCanonicalWeights(frame);
    }
    if(step==1)
    {
     if(Weight("speech_A")>0||Weight("speech_MBP")<99||Weight("emotion_Amused")>0)throw new InvalidOperationException("Bilabial closure did not suppress opening/expression");
     adapter.ResetSpeech();
    }
    if(step==2)
    {
     if(Weight("speech_MBP")>0||Weight("speech_A")>0)throw new InvalidOperationException("Reset left a stale speech pose");
     c.BlinkL=c.BlinkR=0;c.SetPose(0);
    }
    if(step==3)
    {
     Capture();
     File.WriteAllText("Assets/CharacterArt/Generated/RenLOD0/Evidence/live-gym-controls.txt","PASS: actual LiveGym Play frames preserve A through controller LateUpdate, blink alongside speech, MBP closure with emotion suppression, and neutral after reset. No provider or microphone test claimed.\n");
     EditorApplication.update-=Tick;Debug.Log("REN_LOD0_LIVE_CONTROLS_PASS");
    }
    step++;next=EditorApplication.timeSinceStartup+1;
   }
   catch(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);}
  }
 }
}
