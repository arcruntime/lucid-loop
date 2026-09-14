using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LucidLoop.Gyms.Editor
{
    // Build-time resizing/matting of the approved logo; no additional runtime assets.
    public sealed class IosBrandingPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 950;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.iOS) WriteIcons(report.summary.outputPath);
        }

        public static void WriteIcons(string exportPath)
        {
            string sourcePath = "Assets/Gyms/Resources/Branding/BeforeTheDrop.png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(sourcePath)))
                    throw new BuildFailedException("Could not load the approved project logo.");
                string icons = Path.Combine(exportPath, "Unity-iPhone/Images.xcassets/AppIcon.appiconset");
                Directory.CreateDirectory(icons);
                foreach (int size in new[] { 120, 180, 1024 })
                    WriteIcon(source, size, Path.Combine(icons, "BeforeTheDrop-" + size + ".png"));
                File.WriteAllText(Path.Combine(icons, "Contents.json"),
                    "{\"images\":[" +
                    "{\"filename\":\"BeforeTheDrop-120.png\",\"idiom\":\"iphone\",\"scale\":\"2x\",\"size\":\"60x60\"}," +
                    "{\"filename\":\"BeforeTheDrop-180.png\",\"idiom\":\"iphone\",\"scale\":\"3x\",\"size\":\"60x60\"}," +
                    "{\"filename\":\"BeforeTheDrop-1024.png\",\"idiom\":\"ios-marketing\",\"scale\":\"1x\",\"size\":\"1024x1024\"}]," +
                    "\"info\":{\"author\":\"xcode\",\"version\":1}}");
                Debug.Log("IOS_BRANDING_OK: opaque app icons including 1024px marketing icon");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        static void WriteIcon(Texture2D source, int size, string path)
        {
            var output = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                float scale = size * .86f / Mathf.Max(source.width, source.height);
                float width = source.width * scale, height = source.height * scale;
                var background = new Color(.015f, .012f, .025f, 1);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f - (size - width) * .5f) / width;
                    float v = (y + .5f - (size - height) * .5f) / height;
                    var color = background;
                    if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
                    {
                        var foreground = source.GetPixelBilinear(u, v);
                        color = Color.Lerp(background, foreground, foreground.a);
                    }
                    color.a = 1;
                    pixels[y * size + x] = color;
                }
                output.SetPixels(pixels);
                output.Apply();
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(output); }
        }
    }
}
