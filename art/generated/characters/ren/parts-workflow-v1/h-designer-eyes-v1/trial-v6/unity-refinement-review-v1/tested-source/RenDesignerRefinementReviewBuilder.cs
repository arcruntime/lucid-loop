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
 public static class RenDesignerRefinementReviewBuilder
 {
  const string Root="Assets/CharacterArt/Generated/RenDesignerRefinementReview";
  const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerRefinementReview.unity";
  public static void BuildWindowsViewer()
  {
   if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only.");
   if(Hash(Root+"/Shaders/RenTokonNPR.hlsl")!="1c4a197d5724e638b78205ec05794f97fb5b176ec57623b9707453a0c1f5787b")throw new InvalidDataException("Exact receiver candidate hash mismatch.");
   var candidate=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/RenTokonReceiverNPR.shader");if(!candidate||ShaderUtil.ShaderHasError(candidate))throw new InvalidDataException("Receiver candidate shader compilation failed.");
   var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerSupportV2Review.unity",OpenSceneMode.Single);var review=Object.FindFirstObjectByType<RenDesignerEyeReview>();
   var support=Object.FindFirstObjectByType<RenDesignerSupportReview>();support.OldSupport.enabled=false;foreach(var r in support.NewSupport)r.enabled=true;Object.DestroyImmediate(support);
   var patch=RenDesignerPatchImport.Add(review,Arg("-renDesignerPatchSource"));review.Materials=review.Materials.Concat(patch.sharedMaterials).Distinct().ToArray();
   var diagnostic=review.gameObject.AddComponent<RenDesignerRefinementReview>();diagnostic.Review=review;diagnostic.InterfacePatch=patch;diagnostic.PatchAuditSha256=Hash(Root+"/Patch/PatchImportAudit.json");diagnostic.CleanHairBase=LoadHair(Root+"/Hair/Ren_H_Hair_ClumpPigment_4K.png","c6b2949a8038ded557c38a8b529674229b2e454e4f830aec0c0679acbc57627d",4096);diagnostic.CleanHairShadow=LoadHair(Root+"/Hair/Ren_H_Hair_ClumpPigment_Shadow_2K.png","e18bc57739e0942b81387b079947b9ccd473a898f4443aea97a722d4115b179a",2048);diagnostic.HairBaseSha256=Hash(AssetDatabase.GetAssetPath(diagnostic.CleanHairBase));diagnostic.HairShadowSha256=Hash(AssetDatabase.GetAssetPath(diagnostic.CleanHairShadow));diagnostic.OriginalShader=review.Head.sharedMaterial.shader;diagnostic.CandidateShader=candidate;diagnostic.CandidateHlslSha256=Hash(Root+"/Shaders/RenTokonNPR.hlsl");diagnostic.ReferenceSha256=Hash(AssetDatabase.GetAssetPath(review.Sketch));diagnostic.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerRefinementReview.cs");diagnostic.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerRefinementReviewBuilder.cs");
   var ms=new System.Collections.Generic.List<Material>();
   foreach(var renderer in new[]{review.Head}.Concat(review.NewEyes.Where(r=>r.name.EndsWith("SkinShutter",StringComparison.Ordinal))))foreach(var source in renderer.sharedMaterials){var m=new Material(source){name=source.name+"-ReceiverCandidate",shader=candidate};var path=Root+"/"+m.name+".mat";AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(path));ms.Add(m);}diagnostic.CandidateVariants=ms.ToArray();
   File.WriteAllText(Root+"/ShaderBuildEvidence.json",JsonUtility.ToJson(new BuildEvidence{candidateHlslSha256=diagnostic.CandidateHlslSha256,candidateShaderSha256=Hash(Root+"/Shaders/RenTokonReceiverNPR.shader"),messages=ShaderUtil.GetShaderMessages(candidate).Select(m=>m.message).ToArray()},true));
   if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("New receiver scene save failed.");AssetDatabase.SaveAssets();var output=Arg("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new player path required.");Directory.CreateDirectory(Path.GetDirectoryName(output));
   var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
   try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Receiver build failed.");}
   finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
   Debug.Log("REN_DESIGNER_REFINEMENT_BUILD_OK: "+output);
  }
  [Serializable] class BuildEvidence{public string candidateHlslSha256,candidateShaderSha256;public string[] messages;public string scope="Three head and two shutter materials only; corrected support v2 fixed; no source geometry, pigmentation or diffuse-response changes. Not appearance acceptance.";}
  static Texture2D LoadHair(string path,string hash,int max){if(Hash(path)!=hash)throw new InvalidDataException("Hair map hash mismatch");var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.sRGBTexture=true;imp.alphaIsTransparency=false;imp.npotScale=TextureImporterNPOTScale.None;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=max;imp.mipmapEnabled=true;imp.SaveAndReimport();var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture.width!=max||texture.height!=max)throw new InvalidDataException("Hair resolution mismatch");return texture;}
  static string Arg(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
  static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
}
