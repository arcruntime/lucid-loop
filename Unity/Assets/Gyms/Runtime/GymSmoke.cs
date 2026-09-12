#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.Gyms
{
    // Opt-in development-player acceptance run. Normal launches never execute this.
    public sealed class GymSmoke : MonoBehaviour
    {
        string directory;
        string fixture="ws://127.0.0.1:8081/live";
        bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-gym-smoke");
            if(index<0 || index+1>=args.Length)return;
            var runner=new GameObject("Development acceptance run").AddComponent<GymSmoke>();
            runner.directory=args[index+1];Directory.CreateDirectory(runner.directory);
            int relay=Array.IndexOf(args,"-gym-fixture");
            if(relay>=0 && relay+1<args.Length)runner.fixture=args[relay+1];
            Application.runInBackground=true;
            DontDestroyOnLoad(runner.gameObject);
        }
        void OnEnable()=>Application.logMessageReceived+=OnLog;
        void OnDisable()=>Application.logMessageReceived-=OnLog;
        void OnLog(string message,string stack,LogType type)
        {
            if(!finished && (type==LogType.Exception || type==LogType.Error || type==LogType.Assert))
            {finished=true;File.WriteAllText(Path.Combine(directory,"failure.txt"),message+"\n"+stack);Application.Quit(1);}
        }
        void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("GYM_SMOKE: "+message);Debug.Log("GYM_CHECK: "+message);}
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            // Explicit render target also works in a hidden development player.
            var camera=FindFirstObjectByType<GymCamera>().Camera;
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach(var canvas in canvases)
            {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            Canvas.ForceUpdateCanvases();
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
            rt.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            RenderTexture.active=previous;Destroy(pixels);rt.Release();Destroy(rt);
            foreach(var canvas in canvases)canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            yield return null;
        }
        IEnumerator Start()
        {
            yield return new WaitForSeconds(2);
            var gym=FindFirstObjectByType<OfflineGym>();
            Check(gym!=null && gym.State!=null,"DI resolves the offline session");
            Check(gym.Player.isOnNavMesh,"Player starts on the baked navigation mesh");
            yield return Capture("nightclub");
            var spawn=gym.Player.transform.position;
            foreach(var actor in gym.Characters)
            {
                gym.EndConversation();gym.Player.Warp(spawn);
                Check(gym.Approach(actor),"A complete path reaches "+actor.DisplayName);
                float deadline=Time.realtimeSinceStartup+25;
                while(!gym.State.IsTalking && Time.realtimeSinceStartup<deadline)yield return null;
                Check(gym.State.Conversation==actor,"Arrival opens "+actor.DisplayName+" conversation");
                Check(Vector3.Distance(gym.Player.transform.position,actor.transform.position)<2.5f,"Conversation starts near "+actor.DisplayName);
                yield return new WaitForSeconds(.8f);
                Check(gym.Player.GetComponentInChildren<Renderer>().forceRenderingOff,"Approach avatar cannot obscure the conversation");
                yield return Capture("conversation-"+actor.Id);
            }
            gym.EndConversation();
            Check(!gym.State.IsTalking && gym.Rig.Target==null,"Return restores navigation and overview");
            Check(!gym.Player.GetComponentInChildren<Renderer>().forceRenderingOff,"Return restores player visibility");
            SceneManager.LoadScene("LiveGym");
            yield return new WaitForSeconds(2);
            Check(FindFirstObjectByType<LiveGym>()!=null,"Separate live studio loads");
            yield return Capture("live-studio");
            using(var connection=new LiveConnection())
            {
                _=connection.Connect(fixture,"maya","");
                float deadline=Time.realtimeSinceStartup+10;
                bool ready=false,audio=false,caption=false,closed=false;
                while(!ready && Time.realtimeSinceStartup<deadline)
                {
                    while(connection.TryRead(out var value))
                    {Check((string)value["status"]!="error","Fixture startup has no errors");if((string)value["type"]=="gym.status" && (string)value["status"]=="ready")ready=true;}
                    yield return null;
                }
                Check(ready,"Unity WebSocket starts a relay session");
                _=connection.SendAudio(new byte[960]);
                deadline=Time.realtimeSinceStartup+5;
                while(!(audio&&caption) && Time.realtimeSinceStartup<deadline)
                {
                    while(connection.TryRead(out var value))
                    {
                        string type=(string)value["type"];
                        if(type=="session.output_audio.delta")audio=Convert.FromBase64String((string)value["delta"]).Length>0;
                        if(type=="session.output_transcript.delta")caption=!string.IsNullOrEmpty((string)value["delta"]);
                    }
                    yield return null;
                }
                Check(audio&&caption,"Relay fixture returns PCM audio and captions to Unity");
                _=connection.Command("session.close");deadline=Time.realtimeSinceStartup+5;
                while(!closed && Time.realtimeSinceStartup<deadline)
                {
                    while(connection.TryRead(out var value))if((string)value["type"]=="session.closed")closed=true;
                    yield return null;
                }
                Check(closed,"Relay confirms graceful session close");
            }
            finished=true;File.WriteAllText(Path.Combine(directory,"success.txt"),"Navigation, DI, cameras, scene transition and Unity relay fixture passed.");
            Debug.Log("GYM_SMOKE_OK");Application.Quit(0);
        }
    }
}
#endif
