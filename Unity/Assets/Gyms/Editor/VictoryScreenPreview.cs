using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace LucidLoop.Gyms.Editor
{
    public static class VictoryScreenPreview
    {
        [MenuItem("Lucid Loop/Victory/Set up encounter overlay")]
        public static void Setup()
        {
            if(EditorApplication.isPlaying)return;
            var owner=Object.FindFirstObjectByType<EncounterCoordinator>();
            if(!owner){Debug.LogError("Open the BeforeTheDrop encounter scene first.");return;}
            var screen=owner.GetComponent<VictoryScreenController>();
            if(!screen)screen=Undo.AddComponent<VictoryScreenController>(owner.gameObject);
            var adapter=owner.GetComponent<EncounterVictoryPresentation>();
            if(!adapter)adapter=Undo.AddComponent<EncounterVictoryPresentation>(owner.gameObject);
            adapter.Coordinator=owner;adapter.Screen=screen;
            EditorUtility.SetDirty(adapter);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            EditorSceneManager.SaveScene(owner.gameObject.scene);
            Selection.activeGameObject=owner.gameObject;
        }
        public static void SetupMain()
        {
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");Setup();
        }
        [MenuItem("Lucid Loop/Victory/Preview ending")]
        public static void Preview()
        {
            if(EditorApplication.isPlaying) { Show(); return; }
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");
            SessionState.SetBool("Victory.Preview",true);EditorApplication.EnterPlaymode();
        }
        static void Show()
        {
            var screen=Object.FindFirstObjectByType<VictoryScreenController>();
            if(!screen) screen=new GameObject("Victory screen").AddComponent<VictoryScreenController>();
            screen.ShowVictoryScreen();
            screen.gameObject.AddComponent<VictoryReviewCapture>();
        }
        [InitializeOnLoadMethod] static void Register() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!SessionState.GetBool("Victory.Preview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
            SessionState.EraseBool("Victory.Preview");Show();
        }
        public static void ValidateCompile() { Debug.Log("VICTORY_COMPILE_OK"); }
    }
    [CustomEditor(typeof(VictoryScreenController))]
    public sealed class VictoryScreenInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            using(new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if(GUILayout.Button("Show victory screen"))((VictoryScreenController)target).ShowVictoryScreen();
                if(GUILayout.Button("Close preview"))((VictoryScreenController)target).HideVictoryScreen();
            }
        }
    }
}
