using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
 public static class RenDesignerBlinkMainBuilder
 {
  public const string SourceScene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity";
  public const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerBlinkReview.unity";
  const string Root="Assets/CharacterArt/Generated/RenDesignerBlinkReview";
  const string Art="B:/lucid-loop/art/generated/characters/ren/parts-workflow-v1/";
  const string Output="B:/lucid-loop/.local/ren-main-blink-v1";
  [Serializable] class Proof { public string status,project,sourceSceneSha256,blinkSceneSha256,importAuditSha256,runtimeSha256,builderSha256,importerSha256,shaderSha256,hlslSha256;public bool workingMouthFileUnchanged,shaderHasError;public string[] passes,messages; }
  [MenuItem("Lucid Loop/Prepare Ren blink review")]
  public static void Prepare()
  {
   RequireMain();if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Leave Play mode and wait for compilation before preparing the new scene.");
   for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve the currently modified scene before preparing Ren blink: "+SceneManager.GetSceneAt(i).path);
   Directory.CreateDirectory(Output);var sourceHash=Hash(SourceScene);var source=Art+"h-anime-paint-v1/designer-blink-pigment-v1/";Directory.CreateDirectory(Root+"/Shaders");
   CopyExact(source+"RenDesignerBlinkNPR.shader",Root+"/Shaders/RenDesignerBlinkNPR.shader","b8e1fc754f7ad98fb704af9feaa0e716f09720d0569d3210c6bd128ebef91300");
   CopyExact(source+"RenDesignerBlinkNPR.hlsl",Root+"/Shaders/RenDesignerBlinkNPR.hlsl","91d096f3465d3b979317d2fd5a9a847da335f08687913b24d6b9399f80bea113");
   CopyExact("B:/lucid-loop/.local/ren-blink-main-preparation-v1/ImportAudit.json",Root+"/NeutralFrameAudit.json","3fcdc46f6d20074eed095ee45fbdba77fe14785bf48fddfafde3d449254df361");
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/RenDesignerBlinkNPR.shader");if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidDataException("Blink shader import failed");
   var scene=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Single);var context=Object.FindFirstObjectByType<RenDesignerMouthReview>();var review=context.Context.Context.Review;context.OpenA=0;context.Seal=1;
   if(review.Pipeline.additionalLightsRenderingMode!=LightRenderingMode.PerPixel)throw new InvalidDataException("PerPixel additional lights required");
   var controlled=RenDesignerBlinkImport.Add(review,Art+"h-designer-eyes-v1/blink-study-v1/refined-v4/blink-export-v1",restoreCanonicalPositions:true);
   foreach(var renderer in controlled)foreach(var material in renderer.sharedMaterials.Distinct())
   {
    material.shader=shader;material.SetVector("_BaseColor",Vector4.one);material.SetFloat("_UseVertexColor",1);material.SetFloat("_VertexColorSrgb",0);material.SetFloat("_UseBaseMap",0);material.SetFloat("_UseShadowMap",0);material.SetFloat("_ClosedWeight",0);material.SetFloat("_EyeBlinkWeight",0);
    bool skin=renderer.name.EndsWith("_SkinShutter",StringComparison.Ordinal);material.SetFloat("_Unlit",skin?0:1);material.SetFloat("_HighlightStrength",0);material.SetFloat("_RimStrength",0);if(skin){material.SetFloat("_FaceCastShadowStrength",.05f);material.SetFloat("_ShadowStrength",.8f);}EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
   }
   var d=review.gameObject.AddComponent<RenDesignerBlinkReview>();d.MouthContext=context;d.Controlled=controlled;d.BlinkShader=shader;d.SelectedReceiverShader=context.Context.Context.Forehead.Refinement.CandidateShader;d.BlinkL=d.BlinkR=0;d.IdleBlink=false;d.ImportAuditSha256=Hash(RenDesignerBlinkImport.AuditPath);d.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerBlinkReview.cs");d.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerBlinkMainBuilder.cs");d.BlinkShaderSha256=Hash(Root+"/Shaders/RenDesignerBlinkNPR.shader");d.BlinkHlslSha256=Hash(Root+"/Shaders/RenDesignerBlinkNPR.hlsl");
   var bound=review.ModelRoot.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToHashSet();if(review.Materials.Any(m=>!bound.Contains(m))||review.GraphicMaterials.Any(m=>!bound.Contains(m)))throw new InvalidDataException("Unbound review material membership");
   if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("New blink scene save failed");if(Hash(SourceScene)!=sourceHash)throw new InvalidDataException("Working mouth scene file changed");
   var proof=new Proof{status="MAIN_EDITOR_SCENE_PREPARED_NUMERIC_IMPORT_PASSED_GPU_PENDING",project="B:/lucid-loop/Unity",sourceSceneSha256=sourceHash,blinkSceneSha256=Hash(Scene),importAuditSha256=d.ImportAuditSha256,runtimeSha256=d.RuntimeSha256,builderSha256=d.BuilderSha256,importerSha256=Hash("Assets/CharacterArt/Editor/RenDesignerBlinkImport.cs"),shaderSha256=d.BlinkShaderSha256,hlslSha256=d.BlinkHlslSha256,workingMouthFileUnchanged=true,shaderHasError=ShaderUtil.ShaderHasError(shader),passes=Enumerable.Range(0,controlled[0].sharedMaterial.passCount).Select(controlled[0].sharedMaterial.GetPassName).ToArray(),messages=ShaderUtil.GetShaderMessages(shader).Select(m=>m.severity+": "+m.message+" ("+m.file+":"+m.line+")").ToArray()};
   File.WriteAllText(Output+"/MainPreparationEvidence.json",JsonUtility.ToJson(proof,true));if(proof.shaderHasError)throw new InvalidDataException("Blink shader compile error");Debug.Log("REN_MAIN_BLINK_PREPARED: "+Scene);
  }
  [MenuItem("Lucid Loop/Capture Ren blink review")]
  public static void Capture(){RequireMain();if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode in the prepared blink scene first.");if(SceneManager.GetActiveScene().path!=Scene)throw new InvalidOperationException("Use the separate blink scene.");Object.FindFirstObjectByType<RenDesignerBlinkReview>().BeginCapture(Output+"/live-review");}
  static void RequireMain(){if(!Path.GetFullPath(Application.dataPath).Replace('\\','/').Equals("B:/lucid-loop/Unity/Assets",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Run only inside the existing B:/lucid-loop/Unity Editor.");}
  static void CopyExact(string source,string dest,string hash){if(Hash(source)!=hash)throw new InvalidDataException("Frozen input hash mismatch: "+source);File.Copy(source,dest,true);}
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
