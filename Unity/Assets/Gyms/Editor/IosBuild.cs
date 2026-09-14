using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LucidLoop.Gyms.Editor
{
    /// <summary>Primary iOS target; desktop gym builds remain optional diagnostics.</summary>
    public static class IosBuild
    {
        const string RequiredVersion = "6000.3.24f1";
        public static bool ExportingLocalRelayTestFlight { get; private set; }

        [MenuItem("Lucid Loop/iOS/Configure player settings")]
        public static void Configure()
        {
            RequireSupport();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = "17.0";
            PlayerSettings.iOS.microphoneUsageDescription = "Speak with characters during the game.";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);
            // Keep the existing bundle identifier and signing identity.
            AssetDatabase.SaveAssets();
            Debug.Log("IOS_CONFIGURED: iPhone device, iOS 17+, IL2CPP, landscape. Active target: " + EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("Lucid Loop/iOS/Activate iOS target")]
        public static void Activate()
        {
            Configure();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                if (Application.isBatchMode)
                    throw new InvalidOperationException("Launch batch mode with -buildTarget iOS; target switching requires assembly reload.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                    throw new InvalidOperationException("Unity could not activate iOS.");
            }
            Debug.Log("IOS_TARGET_ACTIVE: " + EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("Lucid Loop/iOS/Export development Xcode project")]
        public static void ExportDevelopment()
            => Export(BuildOptions.Development, "Builds/iOS/Xcode");

        [MenuItem("Lucid Loop/iOS/Export internal TestFlight Xcode project")]
        public static void ExportTestFlight()
        {
            ExportingLocalRelayTestFlight = true;
            try { Export(BuildOptions.None, "Builds/iOS/TestFlightXcode"); }
            finally { ExportingLocalRelayTestFlight = false; }
        }

        static void Export(BuildOptions options, string outputPath)
        {
            RequireSupport();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new InvalidOperationException("Activate iOS first, or launch with -buildTarget iOS.");
            Configure();
            IosRenderingPolicy.ValidateForIosBuild();
            IosRenderingPolicy.EnableBuildReporting();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Enable the intended game scenes in Build Profiles first.");
            foreach (var scene in scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Missing enabled build scene", scene);
            Directory.CreateDirectory("Builds/iOS");
            var report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.iOS, options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("iOS export failed: " + report.summary.result);
            foreach (var filename in new[] { "shader-stripping.json", "compute-shader-stripping.json" })
            {
                string source = Path.Combine("Temp", filename);
                if (File.Exists(source)) File.Copy(source, Path.Combine("Builds/iOS", filename), true);
            }
            Debug.Log("IOS_XCODE_EXPORT_OK: " + outputPath);
        }

        static void RequireSupport()
        {
            if (Application.unityVersion != RequiredVersion)
                throw new InvalidOperationException("Use Unity " + RequiredVersion + " for this project.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new InvalidOperationException("Install iOS Build Support for Unity " + RequiredVersion + " in Unity Hub.");
        }
    }
}
