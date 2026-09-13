using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LucidLoop.LiveSpeech;

namespace LucidLoop.Gyms.Editor
{
    public static class EncounterSceneBuilder
    {
        public const string ScenePath = "Assets/Gyms/Scenes/BeforeTheDrop.unity";
        const string SourcePath = "Assets/Gyms/Scenes/CharacterGym.unity";

        [MenuItem("Lucid Loop/Encounter/Create Before the Drop scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Finish play/import/compilation before building the encounter scene.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard your open scene edits before creating the encounter copy.");

            // Save the copy before modifying anything; never mutate the original gym or its assets.
            var source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, ScenePath, true))
                throw new InvalidOperationException("Could not save the encounter scene copy.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var controller in UnityEngine.Object.FindObjectsByType<OfflineGym>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(controller);

            var actors = UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None).ToList();
            var required = new[] { "player", "maya", "ren", "luca", "theo" };
            foreach (var id in required)
                if (actors.Count(a => a.Id == id) != 1) throw new InvalidOperationException("Missing or duplicate source actor: " + id);
            // Exact existing club placements; world positions use X/Z, Ren's stage height is presentation-owned.
            Place(actors, "player", new Vector3(0, 0, -8));
            Place(actors, "maya", new Vector3(-2.8f, 0, -2));
            Place(actors, "ren", new Vector3(2.5f, .6f, 7.4f));
            Place(actors, "luca", new Vector3(-7, 0, 2));
            Place(actors, "theo", new Vector3(7, 0, -1));

            var partner = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            partner.name = "Affair partner (non-interactable)";
            partner.transform.SetParent(actors.First(a => a.Id == "theo").transform.parent, false);
            partner.transform.position = new Vector3(8, 1, -.8f);
            // Root stays at feet height for the server actor binding.
            var partnerRoot = new GameObject("Affair partner");
            partnerRoot.transform.SetParent(partner.transform.parent, false);
            partnerRoot.transform.position = new Vector3(8, 0, -.8f);
            partner.transform.SetParent(partnerRoot.transform, true);
            var partnerActor = partnerRoot.AddComponent<CharacterActor>();
            partnerActor.Id = "affair_partner"; partnerActor.DisplayName = "Affair partner";
            partnerActor.Role = "Club guest"; partnerActor.Visual = partner.transform;
            actors.Add(partnerActor);

            // All existing agents remain inert; accepted server positions own movement in this scene.
            foreach (var agent in UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))
                agent.enabled = false;
            var rig = UnityEngine.Object.FindFirstObjectByType<GymCamera>();
            if (!rig) throw new InvalidOperationException("Source club camera missing.");
            rig.Overview();
            var root = new GameObject("Before the Drop encounter");
            var coordinator = root.AddComponent<EncounterCoordinator>();
            coordinator.Characters = actors.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
            coordinator.ServerOwnsMovement = true;
            root.AddComponent<EncounterPrimitivePresentation>().Coordinator = coordinator;
            var hud = root.AddComponent<EncounterHud>();
            hud.Coordinator = coordinator; hud.Rig = rig;
            var voice = root.AddComponent<EncounterVoiceController>();
            voice.Coordinator = coordinator;
            var speaker = root.AddComponent<AudioSource>(); speaker.playOnAwake = false; speaker.spatialBlend = 0;
            voice.Output = speaker; hud.Voice = voice;
            var speechAdapter = root.AddComponent<LiveSpeechFaceAdapter>();
            var speechBinding = root.AddComponent<EncounterSpeechBinding>();
            speechBinding.Voice = voice; speechBinding.Coordinator = coordinator; speechBinding.Adapter = speechAdapter;
            EncounterMoodSetup.Configure(root, coordinator, voice);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save encounter configuration.");
            // The primary demo is the only enabled build scene; original scenes remain available in the project/settings.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(entry => entry.path != ScenePath)
                    .Select(entry => new EditorBuildSettingsScene(entry.path, false))).ToArray();
            // Original scene files, navigation bake and shared materials are untouched.
            Debug.Log("ENCOUNTER_SCENE_CREATED: " + ScenePath + "; server movement and Live controller bindings ready for integration review.");
        }

        static void Place(System.Collections.Generic.List<CharacterActor> actors, string id, Vector3 position)
            => actors.First(a => a.Id == id).transform.position = position;

        [MenuItem("Lucid Loop/Encounter/Apply primitive presentation to current scene")]
        public static void ApplyPrimitivePresentationToCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the BeforeTheDrop scene outside Play mode first.");
            var coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            if (!coordinator) throw new InvalidOperationException("Encounter coordinator missing.");
            var adapter = coordinator.GetComponent<EncounterPrimitivePresentation>();
            if (!adapter) adapter = coordinator.gameObject.AddComponent<EncounterPrimitivePresentation>();
            adapter.Coordinator = coordinator;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save primitive presentation binding.");
        }
    }
}
