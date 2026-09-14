using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LucidLoop.CharacterArt.Editor
{
 public static class RenLOD0PrefabBuilder
 {
  [MenuItem("Lucid Loop/Ren LOD0/Save gameplay prefab")]
  public static void Build()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before exporting the bind pose");
   var source=UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>();
   if(!source)throw new InvalidOperationException("Open RenLOD0 scene first");
   const string path="Assets/CharacterArt/Generated/RenLOD0/Prefabs/RenLOD0.prefab";
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   var clone=UnityEngine.Object.Instantiate(source.gameObject);
   try
   {
    clone.name="RenLOD0";
    var controller=clone.GetComponent<RenLOD0Controller>();
    controller.ShowControls=false;controller.ShowReference=false;
    controller.ReviewCamera=null;controller.CyanLight=null;controller.MagentaLight=null;
    controller.DesignerReference=null;controller.MovingLights=false;
    clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
    PrefabUtility.SaveAsPrefabAsset(clone,path);
   }
   finally{UnityEngine.Object.DestroyImmediate(clone);}
   var instance=PrefabUtility.LoadPrefabContents(path);
   try
   {
    var c=instance.GetComponent<RenLOD0Controller>();
    if(!c.BodyIdle||!c.BodyIdleRoot||!c.Head||!c.Cap||!c.HairMotion||c.ShowControls||c.ReviewCamera)
     throw new InvalidOperationException("Prefab has missing or scene-dependent bindings");
    foreach(var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
     if(!r.sharedMesh||r.bones.Any(b=>!b||!b.IsChildOf(instance.transform)))throw new InvalidOperationException("Broken skeleton binding: "+r.name);
    File.WriteAllText("Assets/CharacterArt/Generated/RenLOD0/Evidence/prefab.txt","PASS: saved and independently loaded prefab; all skin bones internal; body idle, face, cap and hair references retained; no viewer camera or UI.\n");
   }
   finally{PrefabUtility.UnloadPrefabContents(instance);}
   AssetDatabase.SaveAssets();Debug.Log("REN_LOD0_PREFAB_READY "+path);
  }
 }
}
