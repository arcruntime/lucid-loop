using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.CharacterArt.Editor
{
 public static class RenGazeBuilder
 {
  const string Root="Assets/CharacterArt/Generated/RenLOD0";
  public const string MeshPath=Root+"/Models/RenEyesGaze.asset";
  static readonly string[] Names={"gazeLeftL","gazeRightL","gazeUpL","gazeDownL","gazeLeftR","gazeRightR","gazeUpR","gazeDownR"};
  [MenuItem("Lucid Loop/Ren LOD0/Install gaze shapes")]
  public static void Build()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Exit Play and save scene first");
   string donorPath=Root+"/Models/RenEyesGaze.fbx";
   File.Copy("B:/lucid-loop/art/generated/characters/ren/gaze-final-v1/RenEyesGaze.fbx",donorPath,true);
   AssetDatabase.Refresh();
   var importer=(ModelImporter)AssetImporter.GetAtPath(donorPath);importer.importBlendShapes=true;importer.importAnimation=false;
   importer.importNormals=ModelImporterNormals.Import;importer.importBlendShapeNormals=ModelImporterNormals.Calculate;
   importer.isReadable=true;importer.SaveAndReimport();
   var basis=AssetDatabase.LoadAllAssetsAtPath(Root+"/Models/Ren_LOD0.fbx").OfType<Mesh>().Single(m=>m.name=="RenEyesShallow");
   var donor=AssetDatabase.LoadAllAssetsAtPath(donorPath).OfType<Mesh>().Single(m=>m.name=="RenEyesShallow");
   if(basis.vertexCount!=donor.vertexCount)throw new InvalidOperationException("Eye import vertex counts differ");
   var a=basis.vertices;var b=donor.vertices;float error=0;
   for(int i=0;i<a.Length;i++)error=Mathf.Max(error,(a[i]-b[i]).magnitude);
   if(error>1e-6f||!basis.triangles.SequenceEqual(donor.triangles)||!basis.uv.SequenceEqual(donor.uv))
    throw new InvalidOperationException("Eye import topology/UV/order differs; max coordinate error="+error);
   var output=UnityEngine.Object.Instantiate(basis);output.name="RenEyesGaze";
   foreach(var name in Names)
   {
    int index=Enumerable.Range(0,donor.blendShapeCount).Single(i=>donor.GetBlendShapeName(i).EndsWith(name,StringComparison.Ordinal));
    var vertices=new Vector3[a.Length];var normals=new Vector3[a.Length];var tangents=new Vector3[a.Length];
    donor.GetBlendShapeFrameVertices(index,0,vertices,normals,tangents);
    output.AddBlendShapeFrame(name,100,vertices,normals,tangents);
   }
   var old=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
   if(old){EditorUtility.CopySerialized(output,old);UnityEngine.Object.DestroyImmediate(output);output=old;}else AssetDatabase.CreateAsset(output,MeshPath);
   const string prefabPath=Root+"/Prefabs/RenLOD0.prefab";
   var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
   try{Install(prefab,output);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
   foreach(var scenePath in new[]{Root+"/Scenes/RenLOD0.unity","Assets/Gyms/Scenes/LiveGym.unity","Assets/Gyms/Scenes/BeforeTheDrop.unity"})
   {
    var scene=EditorSceneManager.OpenScene(scenePath);
    foreach(var root in scene.GetRootGameObjects())Install(root,output);
    EditorSceneManager.SaveScene(scene);
   }
   AssetDatabase.SaveAssets();
   File.WriteAllText(Root+"/Evidence/gaze-import.txt",$"PASS: {a.Length} ordered vertices, identical triangles/UVs, neutral error {error}; retained original skin bindings and {basis.blendShapeCount} original shapes; added eight per-eye gaze shapes.\n");
   EditorSceneManager.OpenScene(Root+"/Scenes/RenLOD0.unity");Debug.Log("REN_GAZE_IMPORTED");
  }
  static void Install(GameObject root,Mesh mesh)
  {
   foreach(var controller in root.GetComponentsInChildren<RenLOD0Controller>(true))
    foreach(var renderer in controller.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name=="RenEyesShallow"))
    {renderer.sharedMesh=mesh;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
  }
  static RenLOD0Controller review;static int step;static double next;
  [MenuItem("Lucid Loop/Ren LOD0/Verify gaze in Play")]
  static void Verify()
  {
   if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play RenLOD0 viewer first");
   review=UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>();
   if(!review||!review.ReviewCamera)throw new InvalidOperationException("Use RenLOD0 viewer");
   review.IdleActing=false;review.IdleBlink=false;review.SetPose(0);review.SetGaze(Vector2.zero,Vector2.zero);
   var target=review.Head.position+Vector3.up*.09f;review.ReviewCamera.transform.position=target+new Vector3(0,0,-.65f);review.ReviewCamera.transform.LookAt(target);
   step=0;next=EditorApplication.timeSinceStartup+1;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup<next)return;
   try
   {
    var eyes=review.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="RenEyesShallow");
    float Weight(string key)=>eyes.GetBlendShapeWeight(eyes.sharedMesh.GetBlendShapeIndex(key));
    if(step==1&&(Weight("gazeLeftL")<99||Weight("gazeLeftR")<99))throw new InvalidOperationException("Left gaze did not reach renderers");
    if(step==2&&(Weight("gazeRightL")<99||Weight("gazeRightR")<99))throw new InvalidOperationException("Right gaze did not reach renderers");
    if(step==3&&(Weight("gazeRightL")>0||Weight("gazeRightR")>0))throw new InvalidOperationException("Blink did not suppress gaze");
    RenLOD0Builder.Capture();File.Copy(Root+"/Evidence/ren-lod0.png",Root+"/Evidence/gaze-"+new[]{"neutral","left","right","blink"}[step]+".png",true);
    if(step==0)review.SetGaze(Vector2.left,Vector2.left);
    if(step==1)review.SetGaze(Vector2.right,Vector2.right);
    if(step==2){review.BlinkL=review.BlinkR=1;review.SetPose(1);review.SetGaze(Vector2.right,Vector2.right);}
    if(step==3)
    {
     review.SetPose(0);review.SetGaze(Vector2.zero,Vector2.zero);review.BlinkL=review.BlinkR=0;review.IdleActing=true;review.IdleBlink=true;
     review.ReviewCamera.transform.position=new Vector3(0,.9f,-3);review.ReviewCamera.transform.LookAt(new Vector3(0,.9f,0));
     EditorApplication.update-=Tick;File.WriteAllText(Root+"/Evidence/gaze-runtime.txt","PASS: actual Play frames drive left/right gaze and suppress gaze during bilateral blink with speech A; neutral and idle restored.\n");
    }
    step++;next=EditorApplication.timeSinceStartup+1;
   }
   catch(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);}
  }
 }
}
