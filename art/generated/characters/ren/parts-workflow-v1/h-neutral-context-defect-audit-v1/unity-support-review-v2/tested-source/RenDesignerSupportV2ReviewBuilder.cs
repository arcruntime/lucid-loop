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
    public static class RenDesignerSupportV2ReviewBuilder
    {
        const string Root="Assets/CharacterArt/Generated/RenDesignerSupportV2Review";
        const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerSupportV2Review.unity";
        public static void BuildWindowsViewer()
        {
            if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only.");
            var referencePath=Root+"/OriginalArtist.png";if(Hash(referencePath)!="6eb977e2d736ccad2b9c6b966117b0f618c478550d8c067026758b2a3d319cfc")throw new InvalidDataException("Original sketch hash mismatch.");
            AssetDatabase.ImportAsset(referencePath,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(referencePath);importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.sRGBTexture=true;importer.alphaIsTransparency=false;importer.mipmapEnabled=false;importer.SaveAndReimport();
            var reference=AssetDatabase.LoadAssetAtPath<Texture2D>(referencePath);if(reference.width!=1536||reference.height!=1024)throw new InvalidDataException("Reference canvas dimensions changed.");
            var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeReview.unity",OpenSceneMode.Single);var review=Object.FindFirstObjectByType<RenDesignerEyeReview>();
            var oldSupport=review.ModelRoot.Find("Source").GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Ren_H_ConcealedScalpBack");
            var folder=Argument("-renDesignerSupportSource");if(string.IsNullOrEmpty(folder))throw new InvalidDataException("Explicit frozen support export folder required.");
            var newSupport=RenDesignerSupportV2Import.Add(review,folder);if(newSupport.Length!=2)throw new InvalidDataException("Expected only new source support and bridge.");
            review.Materials=review.Materials.Concat(newSupport.SelectMany(r=>r.sharedMaterials)).Distinct().ToArray();review.Artist=review.Sketch=reference;
            var diagnostic=review.gameObject.AddComponent<RenDesignerSupportReview>();diagnostic.Review=review;diagnostic.OldSupport=oldSupport;diagnostic.NewSupport=newSupport;diagnostic.SupportAuditSha256=Hash(Root+"/SupportImportAudit.json");diagnostic.ReferenceSha256=Hash(referencePath);diagnostic.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerSupportReview.cs");diagnostic.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerSupportV2ReviewBuilder.cs");
            if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("Support diagnostic scene save failed.");AssetDatabase.SaveAssets();
            var output=Argument("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new player output required.");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Support diagnostic player build failed.");}
            finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
            Debug.Log("REN_DESIGNER_SUPPORT_BUILD_OK: "+output);
        }
        static string Argument(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
        static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    }
}
