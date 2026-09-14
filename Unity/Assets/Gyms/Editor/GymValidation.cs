using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.Gyms.Editor
{
    /// <summary>Runs bounded gym/character test scopes inside the already-open Editor.</summary>
    [InitializeOnLoad]
    public static class GymValidation
    {
        static TestRunnerApi runner;
        static RunCallbacks callbacks;

        const string PendingPath = "LucidLoop.Validation.Path";
        const string PendingMarker = "LucidLoop.Validation.Marker";
        const string PendingScope = "LucidLoop.Validation.Scope";

        static GymValidation()
        {
            // PlayMode reloads the domain: re-register completion callbacks using Editor session state.
            string path = SessionState.GetString(PendingPath, "");
            if (path.Length == 0) return;
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new RunCallbacks(path, SessionState.GetString(PendingMarker, "GYM_TESTS"), SessionState.GetString(PendingScope, ""));
            runner.RegisterCallbacks(callbacks);
        }

        [MenuItem("Lucid Loop/Validate encounter EditMode tests")]
        public static void RunEditMode() => Run(TestMode.EditMode, "LucidLoop.Gyms.Tests", "gym-editmode", "GYM_TESTS");

        [MenuItem("Lucid Loop/Validate character EditMode tests")]
        public static void RunCharacterEditMode() => Run(TestMode.EditMode, "LucidLoop.CharacterArt.Tests", "character-editmode", "CHARACTER_EDIT_TESTS");

        [MenuItem("Lucid Loop/Validate character face and speech PlayMode tests")]
        public static void RunCharacterPlayMode() => Run(TestMode.PlayMode, "LucidLoop.CharacterArt.PlayModeTests", "character-playmode", "CHARACTER_PLAY_TESTS", new[]
        {
            @"^LucidLoop\.CharacterArt\.PlayModeTests\.CharacterFaceDriverPlayModeTests\.",
            @"^LucidLoop\.CharacterArt\.PlayModeTests\.CharacterSpeechFramePlayModeTests\."
        });

        [MenuItem("Lucid Loop/Validate integrated encounter PlayMode smoke")]
        public static void RunEncounterPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "encounter-playmode", "ENCOUNTER_PLAY_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterScenePlayModeTests.OpeningMovementCatastropheAndRetainedReset"
        });

        [MenuItem("Lucid Loop/Validate DSP output PlayMode")]
        public static void RunDspOutputPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "dsp-output-playmode", "DSP_OUTPUT_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterDspOutputPlayModeTests.DspOutputConsumesInSmallStepsAndRetiredGenerationStaysSilent"
        });

        [MenuItem("Lucid Loop/Validate Ren real-provider encounter (paid)")]
        public static void RunRenProviderPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "ren-provider-playmode", "REN_PROVIDER_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.RenLiveEncounterPlayModeTests.RealRelayRenConversationDrivesConsumedSpeechAndCloses"
        });

        [MenuItem("Lucid Loop/Validate assembled Ren encounter PlayMode")]
        public static void RunRenEncounterPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "ren-encounter-playmode", "REN_ENCOUNTER_PLAY_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.RenEncounterPresentationPlayModeTests.AssembledRenKeepsSpeechBlinkAndConversationFraming"
        });

        [MenuItem("Lucid Loop/Validate encounter mood PlayMode tests")]
        public static void RunMoodPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "mood-playmode", "MOOD_PLAY_TESTS", new[]
        {
            @"^LucidLoop\.Gyms\.Tests\.EncounterMoodPlayModeTests\."
        });

        [MenuItem("Lucid Loop/Validate phone encounter PlayMode smoke")]
        public static void RunPhoneEncounterPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "phone-encounter-playmode", "PHONE_ENCOUNTER_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterScenePlayModeTests.PhoneOpeningAndExpandableRetainedClues"
        });

        [MenuItem("Lucid Loop/Validate conversation approach PlayMode smoke")]
        public static void RunApproachPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "approach-playmode", "APPROACH_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterScenePlayModeTests.ApproachStartsConversationOnlyAfterServerEligibility"
        });

        [MenuItem("Lucid Loop/Validate audio device PlayMode smoke")]
        public static void RunAudioDevicePlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "audio-device-playmode", "AUDIO_DEVICE_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterAudioDevicePlayModeTests.DeviceChangeStopsPlaybackAndClosesWithoutAutomaticReconnect"
        });

        [MenuItem("Lucid Loop/Validate interaction markers PlayMode smoke")]
        public static void RunMarkersPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "markers-playmode", "MARKERS_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterMarkersPlayModeTests.MarkersFollowAuthoritySelectionAndCameraVisibility"
        });

        [MenuItem("Lucid Loop/Validate paused restart PlayMode")]
        public static void RunRestartPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "restart-playmode", "RESTART_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterPausePlayModeTests.RestartNightFromPauseKeepsNewAttemptPaused"
        });

        [MenuItem("Lucid Loop/Validate pause lifecycle PlayMode smoke")]
        public static void RunPausePlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "pause-playmode", "PAUSE_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterPausePlayModeTests.PauseClosesVoiceAndResynchronizesAcrossReconnect",
            "LucidLoop.Gyms.PlayModeTests.EncounterPausePlayModeTests.PauseMenuBlocksGameplayAndPreservesResumableNight"
        });

        [MenuItem("Lucid Loop/Validate Japanese HUD PlayMode smoke")]
        public static void RunTypographyPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "typography-playmode", "TYPOGRAPHY_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterTypographyPlayModeTests.BundledJapaneseFontRendersPhoneConversation"
        });

        [MenuItem("Lucid Loop/Validate encounter guidance PlayMode smoke")]
        public static void RunGuidancePlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "guidance-playmode", "GUIDANCE_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterGuidancePlayModeTests.GuidanceFollowsPhaseAndDoesNotDeclareEarlyVictory"
        });

        [MenuItem("Lucid Loop/Validate project logo loading")]
        public static void RunLoadingPlayMode() => Run(TestMode.PlayMode, "LucidLoop.Gyms.PlayModeTests", "loading-playmode", "LOADING_TESTS", null, new[]
        {
            "LucidLoop.Gyms.PlayModeTests.EncounterLoadingPlayModeTests.LogoFollowsConnectionAndCancelRestoresTheGame"
        });

        [MenuItem("Lucid Loop/Validate Ren live speech")]
        public static void RunRenLiveSpeech() => Run(TestMode.EditMode, "LucidLoop.LiveSpeech.Tests", "ren-live-speech", "REN_LIVE_SPEECH_TESTS", new[] { "LucidLoop.LiveSpeech.Tests.RenLiveSpeechFaceAdapterTests" });

        [MenuItem("Lucid Loop/Validate Ren gaze and speech mixer")]
        public static void RunRenGazeSpeech() => Run(TestMode.EditMode, "LucidLoop.CharacterArt.Tests", "ren-gaze-speech", "REN_GAZE_SPEECH_TESTS", new[] { "LucidLoop.CharacterArt.Tests.RenGazeMixerTests", "LucidLoop.CharacterArt.Tests.RenLOD0SpeechTests" });

        static void Run(TestMode mode, string assembly, string filename, string marker, string[] groups = null, string[] testNames = null)
        {
            if (runner != null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(marker + "_NOT_STARTED: wait for the Editor to finish compiling/importing and leave Play mode; do not overlap test runs.");
                return;
            }
            if (mode == TestMode.PlayMode)
            {
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    var scene = SceneManager.GetSceneAt(index);
                    if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    {
                        Debug.LogError(marker + "_NOT_STARTED: loaded scene must be saved and clean before PlayMode validation: " + scene.name);
                        return;
                    }
                }
            }
            var repository = Directory.GetParent(Application.dataPath)?.Parent?.FullName;
            if (repository == null) throw new InvalidOperationException("Cannot resolve repository root from Assets.");
            string directory = Path.Combine(repository, ".local", "validation");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, filename + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".xml");
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            SessionState.SetString(PendingPath, path);
            SessionState.SetString(PendingMarker, marker);
            SessionState.SetString(PendingScope, assembly + " " + mode);
            callbacks = new RunCallbacks(path, marker, assembly + " " + mode);
            runner.RegisterCallbacks(callbacks);
            try
            {
                string runId = runner.Execute(new ExecutionSettings(new Filter
                {
                    testMode = mode,
                    assemblyNames = new[] { assembly },
                    groupNames = groups,
                    testNames = testNames
                }) { runSynchronously = false });
                Debug.Log(marker + "_QUEUED: " + runId + " XML=" + path);
            }
            catch (Exception exception)
            {
                Debug.LogError(marker + "_ERROR: " + exception);
                Release();
            }
        }

        static void Release()
        {
            if (runner != null)
            {
                if (callbacks != null) runner.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(runner);
            }
            SessionState.EraseString(PendingPath);
            SessionState.EraseString(PendingMarker);
            SessionState.EraseString(PendingScope);
            runner = null;
            callbacks = null;
        }

        sealed class RunCallbacks : IErrorCallbacks
        {
            readonly string output, marker, scope;
            public RunCallbacks(string output, string marker, string scope) { this.output = output; this.marker = marker; this.scope = scope; }
            public void RunStarted(ITestAdaptor testsToRun) => Debug.Log(marker + "_STARTED: " + scope);
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite && result.ResultState.StartsWith("Failed", StringComparison.Ordinal))
                    Debug.LogError(marker + "_CASE_FAILED: " + result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
            }
            public void OnError(string message)
            {
                Debug.LogError(marker + "_ERROR: " + message);
                Release();
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, output);
                    string summary = "passed=" + result.PassCount + " failed=" + result.FailCount +
                        " skipped=" + result.SkipCount + " inconclusive=" + result.InconclusiveCount + " XML=" + output;
                    if (result.PassCount > 0 && result.FailCount == 0 && result.InconclusiveCount == 0 && result.SkipCount == 0)
                        Debug.Log(marker + "_PASSED: " + summary);
                    else Debug.LogError(marker + "_INCOMPLETE_OR_FAILED: " + summary);
                }
                catch (Exception exception) { Debug.LogError(marker + "_ERROR: result export failed: " + exception); }
                finally { Release(); }
            }
        }
    }
}
