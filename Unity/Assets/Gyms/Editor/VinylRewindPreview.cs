using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace LucidLoop.Gyms.Editor
{
    public static class VinylRewindPreview
    {
        [MenuItem("Lucid Loop/Preview vinyl rewind")]
        public static void Preview()
        {
            if(EditorApplication.isPlaying){Object.FindFirstObjectByType<EncounterRewindTransition>()?.Preview();return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");
            TimeLoopSetup.Setup();SessionState.SetBool("VinylRewind.Preview",true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Update;}
        static void Update()
        {
            if(!SessionState.GetBool("VinylRewind.Preview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<.5f)return;
            var transition=Object.FindFirstObjectByType<EncounterRewindTransition>();if(!transition)return;
            SessionState.EraseBool("VinylRewind.Preview");transition.Preview();
        }
        [MenuItem("Lucid Loop/Validate vinyl rewind with encounter")]
        public static void Validate()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");
            GymValidation.RunRewindPlayMode();
        }
    }
}
