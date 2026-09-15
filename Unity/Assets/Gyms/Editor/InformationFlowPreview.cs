using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace LucidLoop.Gyms.Editor
{
    public static class InformationFlowPreview
    {
        [MenuItem("Lucid Loop/Information/Set up encounter UI")]
        public static void Setup()
        {
            if(EditorApplication.isPlaying)return;
            var owner=Object.FindFirstObjectByType<EncounterCoordinator>();
            if(!owner){Debug.LogError("Open the encounter scene first.");return;}
            var manager=Ensure<LearnedInformationManager>(owner.gameObject);
            var ui=Ensure<InformationNotificationUI>(owner.gameObject);ui.Manager=manager;
            var bridge=Ensure<EncounterInformationBridge>(owner.gameObject);bridge.Coordinator=owner;bridge.Manager=manager;bridge.UI=ui;
            EditorUtility.SetDirty(ui);EditorUtility.SetDirty(bridge);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);EditorSceneManager.SaveScene(owner.gameObject.scene);
            Selection.activeGameObject=owner.gameObject;
        }
        static T Ensure<T>(GameObject owner) where T:Component => owner.GetComponent<T>()??Undo.AddComponent<T>(owner);
        public static void SetupMain(){EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");Setup();Setup();Debug.Log("INFORMATION_SETUP_OK");}
        [MenuItem("Lucid Loop/Information/Preview three-step flow")]
        public static void Preview()
        {
            if(EditorApplication.isPlaying){Seed();return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");SessionState.SetBool("Information.Preview",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!SessionState.GetBool("Information.Preview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
            SessionState.EraseBool("Information.Preview");Seed();
            if(SessionState.GetBool("Information.Capture",false)){SessionState.EraseBool("Information.Capture");Capture();}
        }
        static void Seed()
        {
            var coordinator=Object.FindFirstObjectByType<EncounterCoordinator>();
            if(coordinator && coordinator.IsReady){Debug.LogWarning("Disconnect before using sample information; real discoveries are not overwritten.");return;}
            var manager=Object.FindFirstObjectByType<LearnedInformationManager>();if(!manager)return;
            var layout=Object.FindFirstObjectByType<EncounterHudLayout>();
            if(layout && layout.Connection)layout.Connection.gameObject.SetActive(false); // Match connected gameplay during offline rehearsal.
            manager.ClearForNewGame();
            manager.AddLearnedInformation("preview-music","Theo becomes more confrontational under aggressive music.");
            manager.AddLearnedInformation("preview-luca","Luca intervenes when they argue.");
            manager.AddLearnedInformation("preview-maya","Maya will confront Theo if she sees him in VIP.");
            manager.MarkRead(manager.Entries.Select(e=>e.Id));
            manager.AddLearnedInformation("preview-affair","Theo is having an affair.","clue",true);manager.NotifyLoopStarted();
            Object.FindFirstObjectByType<InformationNotificationUI>().CloseAll();
        }
        [MenuItem("Lucid Loop/Information/Capture three-step flow")]
        public static void Capture()
        {
            if(!EditorApplication.isPlaying){SessionState.SetBool("Information.Capture",true);Preview();return;}
            Seed();var ui=Object.FindFirstObjectByType<InformationNotificationUI>();
            if(ui && !ui.GetComponent<InformationReviewCapture>())ui.gameObject.AddComponent<InformationReviewCapture>();
        }
    }
}
