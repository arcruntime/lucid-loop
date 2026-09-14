using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace LucidLoop.CharacterArt.Editor
{
 public static class RenDesignerMouthReviewBuilder
 {
  const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity";
  public static void BuildWindowsViewer()
  {
   if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only");
   var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerForeheadFieldReview.unity",OpenSceneMode.Single);var context=Object.FindFirstObjectByType<RenDesignerForeheadFieldReview>();var review=context.Context.Review;context.GradedField=true;
   var renderers=RenDesignerMouthImport.Add(review,Arg("-renDesignerMouthHandoff"));
   var d=review.gameObject.AddComponent<RenDesignerMouthReview>();d.Context=context;d.Mouth=renderers;d.OpenA=0;d.Seal=1;d.ImportAuditSha256=Hash(RenDesignerMouthImport.AuditPath);d.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerMouthReview.cs");d.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerMouthReviewBuilder.cs");
   var bound=review.ModelRoot.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToHashSet();if(review.Materials.Any(m=>!bound.Contains(m))||review.GraphicMaterials.Any(m=>!bound.Contains(m)))throw new InvalidDataException("Unbound review material membership");
   if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("Mouth scene save failed");var output=Arg("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new player path required");Directory.CreateDirectory(Path.GetDirectoryName(output));
   var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Mouth build failed");}finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;}
   Debug.Log("REN_DESIGNER_MOUTH_BUILD_OK: "+output);
  }
  static string Arg(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
