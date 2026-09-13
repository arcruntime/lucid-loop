using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.Gyms.Editor
{
    public static class EncounterMoodSetup
    {
        public static void Configure(GameObject root, EncounterCoordinator coordinator, EncounterVoiceController voice)
        {
            var mood = root.GetComponent<EncounterMoodPresentation>();
            if (!mood) mood = root.AddComponent<EncounterMoodPresentation>();
            mood.Coordinator = coordinator; mood.Voice = voice;
            mood.Club = root.scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ClubLighting>(true)).Single();
            mood.AggressiveLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Gyms/Audio/club_aggressive_120bpm.wav");
            mood.IntimateLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Gyms/Audio/club_intimate_96bpm.wav");
            if (!mood.AggressiveLoop || !mood.IntimateLoop) throw new InvalidOperationException("Import both original club audio loops before binding mood presentation.");
            EditorUtility.SetDirty(mood);
        }

        [MenuItem("Lucid Loop/Encounter/Apply mood presentation to current encounter")]
        public static void ApplyToCurrentEncounter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Finish play/import/compilation before binding mood presentation.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != EncounterSceneBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved BeforeTheDrop scene with no unsaved edits first.");
            var coordinator = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<EncounterCoordinator>(true)).Single();
            var voice = coordinator.GetComponent<EncounterVoiceController>();
            if (!voice) throw new InvalidOperationException("Encounter voice controller is missing.");
            Configure(coordinator.gameObject, coordinator, voice);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save mood presentation binding.");
            Debug.Log("ENCOUNTER_MOOD_BOUND: existing lights and original music loops, no shader keyword changes.");
        }
    }

    // Scoped to these original music assets; no imported character/audio assets are touched.
    public sealed class ClubMusicImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (assetPath != "Assets/Gyms/Audio/club_aggressive_120bpm.wav" &&
                assetPath != "Assets/Gyms/Audio/club_intimate_96bpm.wav") return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false; importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings("iPhone", settings);
        }
    }
}
