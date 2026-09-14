using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LucidLoop.CharacterArt.Editor
{
 public static class RenLOD0Builder
 {
  const string Source="B:/lucid-loop/art/generated/characters/ren/lod0-final-v1";
  const string Root="Assets/CharacterArt/Generated/RenLOD0";
  public const string ScenePath=Root+"/Scenes/RenLOD0.unity";
  [MenuItem("Lucid Loop/Ren LOD0/Build complete character")]
  public static void Build()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit play mode first");
   if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene before building");
   if(!File.Exists(Source+"/Ren_LOD0.fbx"))throw new FileNotFoundException("Complete Ren_LOD0.fbx not exported yet");
   foreach(var dir in new[]{"Models","Textures","Materials","Scenes","Evidence"})Directory.CreateDirectory(Root+"/"+dir);
   const string modelPath=Root+"/Models/Ren_LOD0.fbx";
   File.Copy(Source+"/Ren_LOD0.fbx",modelPath,true);
   foreach(var file in Directory.GetFiles(Source+"/textures","*",SearchOption.AllDirectories))
    if(new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(file).ToLowerInvariant()))File.Copy(file,Root+"/Textures/"+Path.GetFileName(file),true);
   File.Copy("B:/lucid-loop/art/characters/ren-model-sheet.png",Root+"/Textures/DesignerRen.png",true);
   AssetDatabase.Refresh();
   var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
   importer.importBlendShapes=true;importer.importAnimation=false;importer.animationType=ModelImporterAnimationType.Generic;
   importer.importNormals=ModelImporterNormals.Import;importer.importBlendShapeNormals=ModelImporterNormals.Calculate;
   importer.importCameras=false;importer.importLights=false;importer.isReadable=true;
   importer.materialImportMode=ModelImporterMaterialImportMode.ImportViaMaterialDescription;
   importer.ExtractTextures(Root+"/Textures");importer.SaveAndReimport();
   foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Textures"}))
   {
    var ti=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
    ti.maxTextureSize=4096;ti.mipmapEnabled=true;ti.textureCompression=TextureImporterCompression.Uncompressed;
    if(ti.assetPath.Contains("closed-eye-mask"))ti.sRGBTexture=false;
    ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="iPhone",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.ASTC_6x6,compressionQuality=100});
    ti.SaveAndReimport();
   }
   var json=File.ReadAllText(Source+"/unity-materials.json");if(json.TrimStart().StartsWith("["))json="{\"materials\":"+json+"}";
   var manifest=JsonUtility.FromJson<Materials>(json);
   var shader=Shader.Find("LucidLoop/CharacterArt/Ren LOD0 Toon");
   if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Ren NPR shader unavailable");
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var container=new GameObject("Ren LOD0");
   var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
   model.transform.SetParent(container.transform,false);model.transform.localRotation=Quaternion.Euler(0,180,0);
   foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
   var cache=new Dictionary<Material,Material>();
   foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>
   {
    if(!original)throw new InvalidOperationException("Missing source material on "+renderer.name);
    if(cache.TryGetValue(original,out var found))return found;
    var entry=manifest.materials.FirstOrDefault(m=>m.name==original.name);
    if(entry==null)throw new InvalidOperationException("Unmapped material: "+original.name);
    var mat=new Material(shader){name="RenLOD0-"+original.name};
    Texture texture=null;
    if(!string.IsNullOrEmpty(entry.baseColorTexture))texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+Path.GetFileName(entry.baseColorTexture));
    if(!string.IsNullOrEmpty(entry.baseColorTexture)&&!texture)throw new InvalidOperationException("Missing albedo "+entry.baseColorTexture);
    mat.SetTexture("_BaseMap",texture);
    mat.SetColor("_BaseColor",texture?Color.white:entry.baseColor!=null&&entry.baseColor.Length>=3?new Color(entry.baseColor[0],entry.baseColor[1],entry.baseColor[2],1):Color.white);
    if(entry.linearRgbGain!=null&&entry.linearRgbGain.Length>=3)mat.SetVector("_AlbedoGain",new Vector4(entry.linearRgbGain[0],entry.linearRgbGain[1],entry.linearRgbGain[2],1));
    var name=original.name.ToLowerInvariant();bool face=entry.closedEye!=null||name.Contains("headskin")||name.Contains("faceskin")||name.Contains("head_skin");
    mat.SetFloat("_FaceLighting",face?1:0);mat.SetFloat("_AccentStrength",.28f);mat.SetFloat("_AccentCap",.45f);
    mat.SetFloat("_UseWorldFace",1);mat.SetVector("_FaceForwardWorld",Vector3.back);mat.SetVector("_FaceRightWorld",Vector3.left);
    if(name.Contains("cavity"))mat.SetFloat("_Unlit",1);
    if(face){mat.SetTexture("_ClosedEyeMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/closed-eye-color.png"));mat.SetTexture("_ClosedEyeMask",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/closed-eye-mask.png"));mat.EnableKeyword("_CLOSED_EYE_CORRECTION");}
    var path=Root+"/Materials/"+string.Concat(mat.name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c))+".mat";
    var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(mat,old);UnityEngine.Object.DestroyImmediate(mat);mat=old;}else AssetDatabase.CreateAsset(mat,path);
    cache[original]=mat;return mat;
   }).ToArray();
   var bones=model.GetComponentsInChildren<Transform>(true);
   var head=bones.Single(t=>t.name=="Head");var hips=bones.Single(t=>t.name=="Hips");
   var cap=bones.First(t=>t.name=="RenCap_Static");
   var hair=container.AddComponent<RenAssemblyHairMotion>();hair.Head=head;hair.CapOn=true;
   hair.Strands=bones.Where(t=>t.name.StartsWith("Hair_")&&t.name!="Hair_Anchor").Select(t=>new RenAssemblyHairMotion.Strand{Bone=t,LimitDegrees=t.name.EndsWith("_2")?4:3,CapMotion=.2f}).ToArray();
   var camera=new GameObject("Ren Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
   camera.transform.position=new Vector3(0,.9f,-3);camera.transform.LookAt(new Vector3(0,.9f,0));camera.fieldOfView=38;camera.nearClipPlane=.01f;camera.farClipPlane=30;
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.08f,.11f);
   var key=new GameObject("White key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.9f;key.color=Color.white;key.transform.rotation=Quaternion.Euler(25,-20,0);
   Light Point(string name,Color color,Vector3 position){var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=.3f;l.range=3;l.transform.position=position;return l;}
   var cyan=Point("Club cyan",new Color(.1f,.7f,1),new Vector3(-.8f,1.5f,-.5f));var magenta=Point("Club magenta",new Color(1,.1f,.4f),new Vector3(.8f,1.5f,.3f));
   var review=container.AddComponent<RenLOD0Controller>();review.LOD0=model;review.Head=head;review.Neck=bones.FirstOrDefault(t=>t.name=="neck");review.Cap=cap.gameObject;review.HairMotion=hair;review.ReviewCamera=camera;
   review.CyanLight=cyan;review.MagentaLight=magenta;review.DesignerReference=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/DesignerRen.png");
   review.BodyIdle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CharacterArt/Generated/RenBodyV3/Animations/RenObserverIdleFemaleV3.anim");review.BodyIdleRoot=hips.parent.gameObject;
   if(!review.BodyIdle)throw new InvalidOperationException("Verified female-v3 idle missing");
   var missing=AnimationUtility.GetCurveBindings(review.BodyIdle).Where(b=>b.type==typeof(Transform)&&!hips.parent.Find(b.path)).Select(b=>b.path).Distinct().ToArray();
   if(missing.Length>0)throw new InvalidOperationException("Idle skeleton mismatch: "+string.Join(",",missing));
   var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
   long triangles=renderers.Sum(r=>(long)r.sharedMesh.triangles.Length/3)+model.GetComponentsInChildren<MeshFilter>(true).Sum(m=>(long)m.sharedMesh.triangles.Length/3);
   if(triangles>45000||triangles<30000)throw new InvalidOperationException("Unexpected complete LOD0 triangle count: "+triangles);
   EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
   File.WriteAllText(Root+"/Evidence/import.json",JsonUtility.ToJson(new Evidence{triangles=triangles,shapeBindings=renderers.Sum(r=>r.sharedMesh.blendShapeCount),hairBones=hair.Strands.Length,materials=cache.Count},true));
   Debug.Log("REN_LOD0_READY "+triangles+" triangles "+ScenePath);
  }
  [MenuItem("Lucid Loop/Ren LOD0/Play")]
  public static void Play(){if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open RenLOD0 first");EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;}
  [MenuItem("Lucid Loop/Ren LOD0/Capture")]
  public static void Capture()
  {
   var review=UnityEngine.Object.FindFirstObjectByType<RenLOD0Controller>();if(!review)throw new InvalidOperationException("Open RenLOD0");
   var rt=new RenderTexture(1600,900,24);var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);var active=RenderTexture.active;
   try{RenderPipeline.SubmitRenderRequest(review.ReviewCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();File.WriteAllBytes(Root+"/Evidence/ren-lod0.png",tex.EncodeToPNG());}
   finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
  }
  [Serializable] sealed class Materials{public Entry[] materials;}
  [Serializable] sealed class Entry{public string name,baseColorTexture;public float[] baseColor,linearRgbGain;public ClosedEye closedEye;}
  [Serializable] sealed class ClosedEye{public string color,mask;}
  [Serializable] sealed class Evidence{public long triangles;public int shapeBindings,hairBones,materials;}
 }
}
