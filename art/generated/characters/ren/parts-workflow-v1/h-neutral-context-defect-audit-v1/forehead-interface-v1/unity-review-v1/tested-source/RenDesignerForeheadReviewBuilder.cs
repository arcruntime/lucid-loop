using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace LucidLoop.CharacterArt.Editor
{
 public static class RenDesignerForeheadReviewBuilder
 {
  const string Root="Assets/CharacterArt/Generated/RenDesignerForeheadReview";
  const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerForeheadReview.unity";
  public static void BuildWindowsViewer()
  {
   if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only");
   var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerRefinementReview.unity",OpenSceneMode.Single);var refinement=Object.FindFirstObjectByType<RenDesignerRefinementReview>();var review=refinement.Review;
   refinement.Mode=2;refinement.CleanHair=true;refinement.PatchVisible=true;var renderer=RenDesignerForeheadPartition.Add(review,Arg("-renDesignerForeheadPartition"),refinement.CandidateShader);
   var d=review.gameObject.AddComponent<RenDesignerForeheadReview>();d.Review=review;d.Refinement=refinement;d.Support=renderer;d.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerForeheadReview.cs");d.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerForeheadReviewBuilder.cs");d.ReferenceSha256=Hash(AssetDatabase.GetAssetPath(review.Sketch));d.PartitionAuditSha256=Hash(Root+"/PartitionAudit.json");
   if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("Forehead scene save failed");AssetDatabase.SaveAssets();var output=Arg("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new player path required");Directory.CreateDirectory(Path.GetDirectoryName(output));
   var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
   try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Forehead player build failed");}
   finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
   Debug.Log("REN_DESIGNER_FOREHEAD_BUILD_OK: "+output);
  }
  static string Arg(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
  static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
