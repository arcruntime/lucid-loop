using UnityEditor;
using UnityEngine;
namespace LucidLoop.Gyms.Editor
{
    public static class DialogueBoxPreview
    {
        [MenuItem("Lucid Loop/Dialogue/Preview Theo interface (Play mode)")]
        public static void Preview()
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play mode first.");return;}
            var preview=GameObject.Find("Offline dialogue preview");if(preview)Object.Destroy(preview);var box=new GameObject("Offline dialogue preview").AddComponent<DialogueBoxController>();
            var hud=GameObject.Find("Before the Drop HUD");if(hud){hud.SetActive(false);box.Closed+=()=>{if(hud)hud.SetActive(true);};}
            box.Open("Theo","The Socialite",false);box.SetResponse("You’re asking about the blackout? Funny.\nYou’re not the first.",box.Epoch);
            box.SetStatus("OFFLINE TEST · Send a reply to play the assigned sample speech.");
            box.TestSpeechClip=Resources.Load<AudioClip>("Dialogue/ExampleTheo");
            box.TestText="Keep your voice down. This is none of your business.";
            box.Submitted+=_=>box.PlayTest();
        }
        [MenuItem("Lucid Loop/Dialogue/Capture and validate assigned speech (Play mode)")]
        public static void Capture()
        {if(EditorApplication.isPlaying){Preview();new GameObject("Dialogue speech validation").AddComponent<DialogueSpeechReview>();}}
        public static void Launch()
        {UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Gyms/Scenes/BeforeTheDrop.unity");SessionState.SetBool("Dialogue.Preview",true);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Tick;}
        static void Tick(){if(!SessionState.GetBool("Dialogue.Preview",false)||!EditorApplication.isPlaying||Time.timeSinceLevelLoad<3)return;SessionState.EraseBool("Dialogue.Preview");Preview();}
        [MenuItem("Lucid Loop/Dialogue/Set up main scene")]
        public static void Setup()
        {
            var hud=Object.FindFirstObjectByType<EncounterHud>();if(!hud)return;
            if(!hud.GetComponent<DialogueBoxController>())Undo.AddComponent<DialogueBoxController>(hud.gameObject);
        }
    }
}
