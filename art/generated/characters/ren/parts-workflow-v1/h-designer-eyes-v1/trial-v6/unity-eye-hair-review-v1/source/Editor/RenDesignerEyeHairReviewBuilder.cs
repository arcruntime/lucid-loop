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
 public static class RenDesignerEyeHairReviewBuilder
 {
  const string Root="Assets/CharacterArt/Generated/RenDesignerEyeHairReview";
  const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeHairReview.unity";
  public static void BuildWindowsViewer()
  {
   if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only");
   var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerForeheadReview.unity",OpenSceneMode.Single);var forehead=Object.FindFirstObjectByType<RenDesignerForeheadReview>();var review=forehead.Review;forehead.ForeheadResponse=true;
   var iris=RenDesignerIrisColorImport.Add(review,Arg("-renDesignerIrisTransfer"));var lower=RenDesignerReturnImport.Add(review,Arg("-renDesignerReturnSource"));
   review.Materials=review.Materials.Concat(lower.sharedMaterials).Distinct().ToArray();review.GraphicMaterials=review.GraphicMaterials.Concat(lower.sharedMaterials).Distinct().ToArray();
   var bound=review.ModelRoot.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToHashSet();if(review.Materials.Any(m=>!bound.Contains(m))||review.GraphicMaterials.Any(m=>!bound.Contains(m)))throw new InvalidDataException("Review material list contains unbound entries");
   var d=review.gameObject.AddComponent<RenDesignerEyeHairReview>();d.Review=review;d.Forehead=forehead;d.Irises=iris;d.ApertureReturn=lower;d.BaselineHairBase=forehead.Refinement.CleanHairBase;d.BaselineHairShadow=forehead.Refinement.CleanHairShadow;
   var folder=Arg("-renDesignerHairSource");d.CandidateHairBase=ImportTexture(folder,"Ren_H_Hair_DirectionalBlond_Base_4K.png","5bc80c29f63b3770ca5708560f46f3efa9f256a323481d289a9eb5b5ca3ace76",4096);d.CandidateHairShadow=ImportTexture(folder,"Ren_H_Hair_DirectionalBlond_Shadow_2K.png","3f4553af7a54777827c528df285094dbde2e3df62d38dc0fbdd29aa695b04829",2048);
   d.IrisAuditSha256=Hash(Root+"/Iris/IrisImportAudit.json");d.ReturnAuditSha256=Hash(Root+"/Return/ReturnImportAudit.json");d.HairBaseSha256=Hash(AssetDatabase.GetAssetPath(d.CandidateHairBase));d.HairShadowSha256=Hash(AssetDatabase.GetAssetPath(d.CandidateHairShadow));d.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerEyeHairReview.cs");d.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerEyeHairReviewBuilder.cs");
   d.IrisPigment=d.LowerReturn=d.DirectionalHair=true;
   if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("Eye/hair scene save failed");AssetDatabase.SaveAssets();var output=Arg("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new output required");Directory.CreateDirectory(Path.GetDirectoryName(output));
   var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Eye/hair build failed");}finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
   Debug.Log("REN_DESIGNER_EYE_HAIR_BUILD_OK: "+output);
  }
  static Texture2D ImportTexture(string folder,string name,string expected,int size){var source=Path.Combine(folder,name);if(Hash(source)!=expected)throw new InvalidDataException("Frozen hair pair mismatch");Directory.CreateDirectory(Root+"/Hair");var path=Root+"/Hair/"+name;File.Copy(source,path,true);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;importer.alphaIsTransparency=false;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=size;importer.mipmapEnabled=true;importer.SaveAndReimport();var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture.width!=size||texture.height!=size)throw new InvalidDataException("Hair map import resolution mismatch");return texture;}
  static string Arg(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
