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
 public static class RenDesignerForeheadFieldReviewBuilder
 {
  const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerForeheadFieldReview.unity";
  public static void BuildWindowsViewer()
  {
   if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only");
   var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeHairReview.unity",OpenSceneMode.Single);var context=Object.FindFirstObjectByType<RenDesignerEyeHairReview>();var review=context.Review;var support=context.Forehead.Support;
   context.IrisPigment=context.LowerReturn=context.DirectionalHair=true;context.Forehead.ForeheadResponse=true;var candidate=RenDesignerForeheadFieldImport.Add(review,support,Arg("-renDesignerForeheadFieldSource"));review.Materials=review.Materials.Concat(candidate.sharedMaterials).Distinct().ToArray();
   var d=review.gameObject.AddComponent<RenDesignerForeheadFieldReview>();d.Context=context;d.OriginalSupport=support;d.CandidateSupport=candidate;d.GradedField=true;d.ImportAuditSha256=Hash(RenDesignerForeheadFieldImport.AuditPath);d.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerForeheadFieldReview.cs");d.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerForeheadFieldReviewBuilder.cs");
   var shader=candidate.sharedMaterial.shader;if(ShaderUtil.ShaderHasError(shader))throw new InvalidDataException("Field shader compilation error");
   if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("New field scene save failed");var output=Arg("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new player path required");Directory.CreateDirectory(Path.GetDirectoryName(output));
   var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Field build failed");}finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;}
   Debug.Log("REN_DESIGNER_FOREHEAD_FIELD_BUILD_OK: "+output);
  }
  static string Arg(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
