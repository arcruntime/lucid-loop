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
    public static class RenDesignerShadowReviewBuilder
    {
        const string Root="Assets/CharacterArt/Generated/RenDesignerShadowReview";
        const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerShadowReview.unity";
        public static void BuildWindowsViewer()
        {
            if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only.");
            var referencePath=Root+"/OriginalArtist.png";if(Hash(referencePath)!="6eb977e2d736ccad2b9c6b966117b0f618c478550d8c067026758b2a3d319cfc")throw new InvalidDataException("Original sketch hash mismatch.");
            AssetDatabase.ImportAsset(referencePath,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(referencePath);
            importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.sRGBTexture=true;importer.alphaIsTransparency=false;importer.mipmapEnabled=false;importer.SaveAndReimport();
            var reference=AssetDatabase.LoadAssetAtPath<Texture2D>(referencePath);if(reference.width!=1536||reference.height!=1024)throw new InvalidDataException("Reference canvas dimensions changed.");
            var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeReview.unity",OpenSceneMode.Single);var review=Object.FindFirstObjectByType<RenDesignerEyeReview>();
            review.Artist=review.Sketch=reference;var diagnostic=review.gameObject.AddComponent<RenDesignerShadowReview>();diagnostic.Review=review;diagnostic.ReferenceSha256=Hash(referencePath);diagnostic.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerShadowReview.cs");diagnostic.BuilderSha256=Hash("Assets/CharacterArt/Editor/RenDesignerShadowReviewBuilder.cs");
            if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("Diagnostic scene save failed.");AssetDatabase.SaveAssets();
            var a=Environment.GetCommandLineArgs();var index=Array.IndexOf(a,"-renDesignerPlayerOutput");if(index<0)throw new InvalidOperationException("Explicit new output required.");var output=a[index+1];Directory.CreateDirectory(Path.GetDirectoryName(output));
            var prior=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            try{GraphicsSettings.defaultRenderPipeline=review.Pipeline;QualitySettings.renderPipeline=review.Pipeline;var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Shadow diagnostic player build failed.");}
            finally{GraphicsSettings.defaultRenderPipeline=prior;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
            Debug.Log("REN_DESIGNER_SHADOW_BUILD_OK: "+output);
        }
        static string Hash(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    }
}
