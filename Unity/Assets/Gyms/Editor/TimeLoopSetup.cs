using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace LucidLoop.Gyms.Editor
{
    public static class TimeLoopSetup
    {
        [MenuItem("Lucid Loop/Time Loop/Set up current scene")]
        public static void Setup()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play mode before running setup.");return;}
            var controller=Object.FindFirstObjectByType<TimeLoopTransitionController>();
            if(!controller){var go=new GameObject("Time loop transition");Undo.RegisterCreatedObjectUndo(go,"Set up time loop");controller=go.AddComponent<TimeLoopTransitionController>();}
            if(!controller.GetComponent<TimeLoopAutoSetup>())controller.gameObject.AddComponent<TimeLoopAutoSetup>();
            controller.GameplayCamera=Camera.main;
            ConfigureRenderer();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
            Debug.Log("TIME_LOOP_SETUP_OK: idempotent scene controller and URP Full Screen Pass after transparents");
        }
        public static void ConfigureRenderer()
        {
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Gyms/Generated/GymRenderer.asset");
            if(!renderer)throw new System.InvalidOperationException("The project's GymRenderer asset is missing.");
            var features=renderer.rendererFeatures.OfType<TimeLoopFullScreenFeature>().ToArray();
            var feature=features.FirstOrDefault();
            if(!feature){feature=ScriptableObject.CreateInstance<TimeLoopFullScreenFeature>();feature.name="Vinyl time loop (after transparents)";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
            foreach(var extra in features.Skip(1)){renderer.rendererFeatures.Remove(extra);Object.DestroyImmediate(extra,true);}
            feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer=true;feature.requirements=ScriptableRenderPassInput.None;feature.bindDepthStencilAttachment=false;feature.SetActive(true);feature.Create();
            EditorUtility.SetDirty(feature);renderer.SetDirty();EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssets();
        }
        [MenuItem("Lucid Loop/Time Loop/Trigger in Play mode")]
        public static void Trigger(){Object.FindFirstObjectByType<TimeLoopTransitionController>()?.TriggerLoop();}
        public static void SetupEncounter()
        {
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");
            int cameras=Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int events=Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            Setup();Setup();
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Gyms/Generated/GymRenderer.asset");
            if(Object.FindObjectsByType<TimeLoopTransitionController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=1 || renderer.rendererFeatures.OfType<TimeLoopFullScreenFeature>().Count()!=1 ||
                Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=cameras ||
                Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=events)
                throw new System.InvalidOperationException("Time-loop setup introduced duplicate scene or renderer objects.");
            if(ShaderUtil.ShaderHasError(Resources.Load<Shader>("Rewind/TimeLoopFullscreen")))throw new System.InvalidOperationException("Time-loop shader compilation failed.");
            Debug.Log("TIME_LOOP_SETUP_VALIDATED: one controller, one feature, unchanged camera/EventSystem counts, shader compiled.");

        }
    }
}
