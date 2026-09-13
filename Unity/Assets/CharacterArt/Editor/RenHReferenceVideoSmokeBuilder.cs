using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

namespace LucidLoop.CharacterArt.Editor
{
    /// <summary>Scratch-only, model-free Play-mode decoder verification. Not a character viewer deliverable.</summary>
    [InitializeOnLoad]
    public static class RenHReferenceVideoSmokeBuilder
    {
        const string ActiveKey = "RenHReferenceVideoSmoke.active", OutputKey = "RenHReferenceVideoSmoke.output", DeadlineKey = "RenHReferenceVideoSmoke.deadline", WaitingKey = "RenHReferenceVideoSmoke.waiting";
        const string Root = "Assets/CharacterArt/Generated/RenHTransportSmoke";
        [Serializable] sealed class Result { public bool passed = false; }
        static double settledSince, lastMonitor;
        static RenHReferenceVideoSmokeBuilder() { if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Monitor; }
        public static void Run()
        {
            if (!Application.isBatchMode || Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Run the technical video smoke test only in the authorized scratch batch Editor.");
            var output = Argument("-renHVideoSmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Explicit local smoke output directory is required.");
            output = Path.GetFullPath(output); Directory.CreateDirectory(output);
            if (File.Exists(Path.Combine(output, "transport-smoke.json"))) throw new InvalidOperationException("Use a fresh smoke output directory to preserve previous evidence.");
            var video = AssetDatabase.LoadAssetAtPath<VideoClip>(Root + "/Reference.mp4");
            var timeline = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Timeline.json");
            if (!video || !timeline) throw new InvalidOperationException("Copy/import the actual local reference video and frozen timeline into the scratch smoke folder first.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("Technical video transport smoke — no character model");
            var player = host.AddComponent<VideoPlayer>(); player.source = VideoSource.VideoClip; player.clip = video;
            player.playOnAwake = false; player.renderMode = VideoRenderMode.APIOnly; player.audioOutputMode = VideoAudioOutputMode.Direct;
            var probe = host.AddComponent<RenHReferenceVideoSmokeProbe>();
            probe.Video = player; probe.Timeline = timeline; probe.OutputDirectory = output;
            probe.SourceSha256 = Hash(Root + "/Reference.mp4");
            probe.TransportSha256 = Hash("Assets/CharacterArt/Runtime/RenHReferenceVideoTransport.cs");
            probe.ControllerSha256 = Hash("Assets/CharacterArt/Runtime/RenHReferenceAnimationController.cs");
            var camera = new GameObject("Technical smoke camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Root + "/RenHTransportSmoke.unity");
            SessionState.SetString(OutputKey, output); SessionState.SetString(DeadlineKey, DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
            SessionState.SetBool(WaitingKey, true); settledSince = lastMonitor = 0;
            SessionState.SetBool(ActiveKey, true); EditorApplication.update -= Monitor; EditorApplication.update += Monitor;
        }
        static void Monitor()
        {
            if (!SessionState.GetBool(ActiveKey, false)) { EditorApplication.update -= Monitor; return; }
            if (SessionState.GetBool(WaitingKey, false))
            {
                if (long.TryParse(SessionState.GetString(DeadlineKey, "0"), out var waitDeadline) && DateTime.UtcNow.Ticks > waitDeadline)
                {
                    SessionState.SetBool(ActiveKey, false); EditorApplication.update -= Monitor;
                    Debug.LogError("REN_H_TRANSPORT_IMPORT_SETTLE_TIMEOUT"); EditorApplication.Exit(3); return;
                }
                var now = EditorApplication.timeSinceStartup;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || lastMonitor == 0 || now - lastMonitor > 1) settledSince = now;
                lastMonitor = now;
                if (now - settledSince < 2) return;
                SessionState.SetBool(WaitingKey, false);
                SessionState.SetString(DeadlineKey, DateTime.UtcNow.AddSeconds(100).Ticks.ToString());
                EditorApplication.EnterPlaymode(); return;
            }
            var result = Path.Combine(SessionState.GetString(OutputKey, ""), "transport-smoke.json");
            if (File.Exists(result))
            {
                var passed = JsonUtility.FromJson<Result>(File.ReadAllText(result)).passed;
                SessionState.SetBool(ActiveKey, false); EditorApplication.update -= Monitor;
                Debug.Log("REN_H_TRANSPORT_EDITOR_EXIT: " + (passed ? "passed" : "failed")); EditorApplication.Exit(passed ? 0 : 2);
            }
            else if (long.TryParse(SessionState.GetString(DeadlineKey, "0"), out var ticks) && DateTime.UtcNow.Ticks > ticks)
            {
                SessionState.SetBool(ActiveKey, false); EditorApplication.update -= Monitor;
                Debug.LogError("REN_H_TRANSPORT_EDITOR_TIMEOUT"); EditorApplication.Exit(3);
            }
        }
        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string Argument(string name)
        { var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    }
}
