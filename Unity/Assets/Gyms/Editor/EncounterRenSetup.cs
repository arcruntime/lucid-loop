using System;
using System.Linq;
using LucidLoop.CharacterArt;
using LucidLoop.LiveSpeech;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LucidLoop.Gyms.Editor
{
    public static class EncounterRenSetup
    {
        [MenuItem("Lucid Loop/Encounter/Install assembled Ren")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Finish Play/import before installing Ren.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits before installing Ren.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterArt/Generated/RenLOD0/Prefabs/RenLOD0.prefab");
            if (!prefab || !prefab.GetComponent<RenLOD0Controller>())
                throw new InvalidOperationException("Reviewed Ren prefab is missing.");
            var scene = EditorSceneManager.OpenScene(EncounterSceneBuilder.ScenePath);
            var coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            if (!coordinator) throw new InvalidOperationException("Encounter coordinator missing.");
            var actor = coordinator.Characters.Single(a => a && a.Id == "ren");
            var existing = actor.GetComponentInChildren<RenLOD0Controller>(true);
            var model = existing ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (actor.Visual && actor.Visual != model.transform) actor.Visual.gameObject.SetActive(false);
            model.name = "Ren complete character";
            model.transform.SetParent(actor.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            model.transform.localScale = Vector3.one;
            model.SetActive(true);
            var controller = model.GetComponent<RenLOD0Controller>();
            controller.ShowControls = false;
            var speech = model.GetComponent<RenLiveSpeechFaceAdapter>();
            if (!speech) speech = model.AddComponent<RenLiveSpeechFaceAdapter>();
            speech.FaceController = controller;
            speech.Renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            actor.Visual = model.transform; actor.Mouth = null;
            var anchor = controller.Head.Find("Conversation face anchor");
            if (!anchor)
            {
                anchor = new GameObject("Conversation face anchor").transform;
                anchor.SetParent(controller.Head, false);
                anchor.position = controller.Head.position + Vector3.up * .09f;
            }
            actor.ConversationFaceAnchor = anchor;
            actor.ConversationVerticalOffset = 0;
            actor.ConversationSize = .36f;
            actor.ConversationHorizontalOffset = .16f;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save encounter Ren.");
            Debug.Log("ENCOUNTER_REN_INSTALLED: assembled prefab and consumed-PCM speech adapter; other actors and world preserved.");
        }
    }
}
