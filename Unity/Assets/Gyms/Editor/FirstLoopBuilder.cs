using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LucidLoop.Gyms.Mvp;
namespace LucidLoop.Gyms.Editor
{
    public static class FirstLoopBuilder
    {
        public const string ScenePath="Assets/Gyms/Scenes/BeforeTheDrop.unity";
        [MenuItem("Lucid Loop/MVP/Create or rebuild first loop")]
        public static void Build()
        {
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/CharacterGym.unity");
            var gym=UnityEngine.Object.FindFirstObjectByType<OfflineGym>();
            var mvp=new GameObject("First playable loop").AddComponent<FirstLoop>();
            mvp.Player=gym.Player; mvp.Rig=gym.Rig;
            foreach(var actor in gym.Characters)
            {
                switch(actor.Id) { case "maya":mvp.Maya=actor;break;case "theo":mvp.Theo=actor;break;case "luca":mvp.Luca=actor;break;case "ren":mvp.Ren=actor;break; }
            }
            mvp.Player.transform.position=new Vector3(0,0,-9);
            mvp.Maya.transform.position=new Vector3(-.9f,0,-9);
            // Camera and scene remain the existing replaceable 3D gym, not a second engine.
            mvp.Rig.OverviewSize=13; mvp.Rig.Pitch=48; mvp.Rig.Yaw=-18;
            var partner=GameObject.CreatePrimitive(PrimitiveType.Capsule);partner.name="Affair partner (non-interactable)";
            partner.transform.position=mvp.Theo.transform.position+new Vector3(.65f,.9f,.35f);partner.transform.localScale=new Vector3(.55f,.9f,.55f);
            UnityEngine.Object.DestroyImmediate(partner.GetComponent<Collider>()); mvp.Partner=partner.transform;
            UnityEngine.Object.DestroyImmediate(gym.gameObject);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            AssetDatabase.SaveAssets(); Debug.Log("BTD_BUILD_OK "+ScenePath);
        }
        public static void Smoke()
        {
            Build();
            Application.logMessageReceived += (message,trace,type)=> { if(type==LogType.Exception) { Debug.LogError("BTD_SMOKE_FAILED "+message); EditorApplication.Exit(1); } };
            EditorApplication.EnterPlaymode();
        }
        public static void PlayCheckpoint()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
