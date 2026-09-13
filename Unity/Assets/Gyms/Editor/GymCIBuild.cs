using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LucidLoop.Gyms.Editor
{
    public static class GymCIBuild
    {
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows/LucidLoopGyms.exe");
        public static void MacOS() => Build(BuildTarget.StandaloneOSX, "macOS/LucidLoopGyms.app");

        static void Build(BuildTarget target, string output)
        {
            if (Application.unityVersion != "6000.3.24f1")
                throw new InvalidOperationException("CI requires Unity 6000.3.24f1.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install Unity build support for " + target);
            // Build the versioned scenes, preserving reviewed scene changes.
            var scenes = new[] { "Assets/Gyms/Scenes/CharacterGym.unity", "Assets/Gyms/Scenes/LiveGym.unity" };
            foreach (var scene in scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Missing gym scene", scene);
            string destination = "Builds/" + output;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(scenes, destination, target, BuildOptions.Development);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Gym build failed: " + report.summary.result);
            Debug.Log("GYM_CI_BUILD_OK: " + target);
        }
    }
}
