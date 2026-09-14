using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using LucidLoop.LiveSpeech;

namespace LucidLoop.Gyms
{
    public class LiveGym : MonoBehaviour
    {
        public GymCamera Rig;
        public CharacterActor[] Characters;
        public AudioSource Speaker;
        public RenLiveSpeechFaceAdapter RenSpeechFace;
        public string DefaultRelay="ws://localhost:8080/live";
        LiveConnection connection;
        readonly ConsumedSpeechPlayback playback=new ConsumedSpeechPlayback(24000*3);
        long speechGeneration;
        AudioClip microphone,outputClip;
        string device;
        int micCursor,selected;
        bool ready,connecting,muted,muteRequested,mutePending,closing,finalUsageReceived;
        float deadline;
        volatile float level;
        InputField address,access;
        Text status,transcript,meter,characterName;
        Button connect,mute;
        string playerText="",actorText="";
        int generation;
        public bool IsReady=>ready;
        public string Status=>status?status.text:"";
        void Start()
        {
            var canvas=GymUI.Canvas("Live voice HUD");
            var header=GymUI.Box(canvas,"Identity",30,26,580,113);GymUI.Panel(header,GymUI.Ink);
            GymUI.Label(header,"LUCID LOOP",24,14,500,45,34);
            GymUI.Label(header,"02 / LIVE VOICE GYM   ·   GPT-LIVE",24,64,525,28,18,GymUI.Cyan);
            var back=GymUI.Rect(canvas,"Back",Vector2.one,Vector2.one,new Vector2(-290,-83),new Vector2(-30,-27));
            GymUI.Button(back,"←  Character gym",()=>{Shutdown();SceneManager.LoadScene("CharacterGym");});
            var panel=GymUI.Rect(canvas,"Live controls",new Vector2(1,0),Vector2.one,new Vector2(-550,26),new Vector2(-30,-112));GymUI.Panel(panel,GymUI.Ink);
            characterName=GymUI.Label(panel,"Maya / voice study",26,22,468,44,32);
            for(int i=0;i<Characters.Length;i++)
            {int n=i;GymUI.Button(GymUI.Box(panel,"Select "+Characters[i].Id,26+i*118,78,110,46),Characters[i].DisplayName,()=>Select(n));}
            address=GymUI.Input(panel,"RELAY ADDRESS",149,DefaultRelay);
            access=GymUI.Input(panel,"GYM ACCESS TOKEN · NOT AN OPENAI KEY",250,"",true);
            connect=GymUI.Button(GymUI.Box(panel,"Connect",26,356,226,56),"Connect",()=>{if(closing)return;if(ready||connecting)Disconnect();else StartCoroutine(Connect());},new Color(.06f,.38f,.4f));
            mute=GymUI.Button(GymUI.Box(panel,"Mute",268,356,226,56),"Mute mic",ToggleMute);
            status=GymUI.Label(panel,"Disconnected · microphone off",26,432,468,70,21,GymUI.Cyan);
            transcript=GymUI.Label(panel,"Connect to speak with this character.\n\nThe project pays for live sessions. No ChatGPT sign-in is used.",26,522,468,235,22);
            meter=GymUI.Label(panel,"PLAYBACK  ────────────",26,785,468,34,19,GymUI.Muted);
            var foot=GymUI.Rect(canvas,"Study note",Vector2.zero,new Vector2(.65f,0),new Vector2(30,26),new Vector2(-10,100));GymUI.Panel(foot,GymUI.Ink);
            GymUI.Text(GymUI.Rect(foot,"Text",Vector2.zero,Vector2.one,new Vector2(20,10),new Vector2(-20,-10)),"Voice & framing lab · English speech face\nUse headphones to avoid microphone feedback.",21,Color.white,TextAnchor.MiddleLeft);
            outputClip=AudioClip.Create("Live output 24 kHz",24000,1,24000,true,ReadAudio);
            Speaker.clip=outputClip;Speaker.loop=true;Speaker.Play();Select(0);
        }
        public void Select(int index)
        {
            if(ready||connecting||closing||index<0||index>=Characters.Length)return;
            ResetPlayback(false);selected=index;
            for(int i=0;i<Characters.Length;i++)Characters[i].gameObject.SetActive(i==index);
            Rig.Present(Characters[index]);characterName.text=Characters[index].DisplayName+" / voice study";
        }
        IEnumerator Connect()
        {
            connecting=true;deadline=Time.unscaledTime+60;int attempt=++generation;status.text="Requesting microphone permission…";SetButton(connect,"Cancel");
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(attempt!=generation)yield break;
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)){Fail("Microphone permission denied.");yield break;}
            if(Microphone.devices.Length==0){Fail("No microphone found. Connect a microphone and retry.");yield break;}
            device=Microphone.devices[0];
            try{microphone=Microphone.Start(device,true,1,24000);}
            catch(Exception){Fail("Could not start microphone.");yield break;}
            if(!microphone||microphone.frequency!=24000){Fail("Microphone could not capture at 24 kHz.");yield break;}
            micCursor=0;playerText=actorText="";ResetPlayback(true);muted=muteRequested=mutePending=closing=finalUsageReceived=false;
            connection=new LiveConnection();deadline=Time.unscaledTime+20;
            status.text="Connecting to relay…";
            _=connection.Connect(address.text.Trim(),Characters[selected].Id,access.text);
            SetButton(connect,"Disconnect");
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))Disconnect();
            var active=connection;
            if(active!=null)
            {
                int budget=80;while(budget-->0&&active.TryRead(out var ev))
                {Handle(ev);if(connection!=active)break;}
            }
            if((connecting||closing)&&Time.unscaledTime>deadline)
            {
                if(closing&&finalUsageReceived)CompleteClose();
                else Fail(closing?"Closed locally · final usage unconfirmed":"Session startup timed out.");
            }
            if(ready&&!closing&&microphone)
            {
                int position=Microphone.GetPosition(device);
                if(position<0){Fail("Microphone disconnected.");return;}
                int available=(position-micCursor+microphone.samples)%microphone.samples;
                // GetData wraps at the clip boundary. Limit each frame's work after stalls.
                if(available>12000){micCursor=(position-480+microphone.samples)%microphone.samples;available=480;}
                while(available>=480)
                {
                    var samples=new float[480];microphone.GetData(samples,micCursor);
                    if(!muteRequested)_=connection.SendAudio(AudioRingBuffer.Encode(samples,480));
                    micCursor=(micCursor+480)%microphone.samples;available-=480;
                }
            }
            if(meter)meter.text="PLAYBACK  "+new string('▰',Mathf.Clamp(Mathf.RoundToInt(level*18),0,12))+"  "+(playback.Count/24)+" ms queued";
            if(UsesRenSpeech)
            {
                playback.Snapshot(out long consumed,out bool starved);
                RenSpeechFace.UpdatePlayback(consumed,speechGeneration,starved);
            }
            else if(Characters.Length>selected)Characters[selected].SetSpeech(Mathf.Clamp01(level*5));
        }
        void Handle(JObject ev)
        {
            string type=(string)ev["type"];
            if(type=="session.started")
            {status.text="Session started · waiting for relay…";}
            else if(type=="session.output_audio.delta")
            {
                if(closing)return;
                try
                {
                    var pcm=Convert.FromBase64String((string)ev["delta"]??"");
                    if(pcm.Length%2!=0||pcm.Length>24000*6)throw new ArgumentException();
                    if(!playback.TryWritePcm16(pcm))
                    {
                        // Never silently discard old samples while retaining their speech timeline.
                        ResetPlayback(true);
                        if(!playback.TryWritePcm16(pcm))throw new ArgumentException();
                        status.text="Live · playback queue reset after overflow";
                    }
                    if(UsesRenSpeech&&RenSpeechFace.IsSupported&&!RenSpeechFace.PushPcm16(pcm,speechGeneration))
                    {
                        string diagnostic=RenSpeechFace.Diagnostic;
                        ResetPlayback(true);status.text="Live · speech reset: "+diagnostic;
                    }
                }
                catch(Exception){Fail("Invalid audio received from relay.");}
            }
            else if(type=="session.input_transcript.delta") {playerText=Tail(playerText+(string)ev["delta"]);ShowTranscript();}
            else if(type=="session.output_transcript.delta") {actorText=Tail(actorText+(string)ev["delta"]);ShowTranscript();}
            else if(type=="session.input_audio.muted")
            {muted=muteRequested=true;mutePending=false;SetButton(mute,"Unmute mic");status.text="Live · microphone muted";}
            else if(type=="session.input_audio.unmuted")
            {muted=muteRequested=false;mutePending=false;SetButton(mute,"Mute mic");status.text="Live · listening and speaking";}
            else if(type=="session.closed")
            {finalUsageReceived=true;ready=false;connecting=false;closing=true;deadline=Time.unscaledTime+2;StopMic();ResetPlayback(false);status.text="Final usage received · closing transport…";}
            else if(type=="gym.transport.closed")
            {if(finalUsageReceived)CompleteClose();else if(ready||connecting||closing)Fail("Connection closed · final usage unconfirmed");}
            else if(type=="gym.status")
            {
                string state=(string)ev["status"];
                if(state=="ready")
                {ready=true;connecting=false;status.text="Live · listening and speaking";micCursor=Microphone.GetPosition(device);}
                else if(state=="closed"&&finalUsageReceived)CompleteClose();
                else if(state=="error")Fail((string)ev["message"]??(string)ev["code"]??"Relay rejected the session.");
            }
            else if(type=="error")
            {
                muteRequested=muted;mutePending=false;SetButton(mute,muted?"Unmute mic":"Mute mic");
                string message=(string)ev["error"]?["message"];
                if(connecting)Fail(message??"OpenAI rejected session startup.");
                else status.text="Live error · "+(message??"The last command was rejected.");
            }
        }
        static string Tail(string s)=>s.Length>420?s.Substring(s.Length-420):s;
        void ShowTranscript(){transcript.text="YOU\n"+playerText+"\n\n"+Characters[selected].DisplayName.ToUpperInvariant()+"\n"+actorText;}
        void ReadAudio(float[] data)
        {level=playback.ReadBlock(data);}
        bool UsesRenSpeech=>RenSpeechFace&&selected>=0&&selected<Characters.Length&&string.Equals(Characters[selected].Id,"ren",StringComparison.OrdinalIgnoreCase);
        void ResetPlayback(bool beginSpeech)
        {
            playback.Clear();level=0;speechGeneration++;
            if(RenSpeechFace)RenSpeechFace.ResetSpeech();
            if(beginSpeech&&UsesRenSpeech&&!RenSpeechFace.BeginStream(speechGeneration)&&status)
                status.text="Speech face unavailable · "+RenSpeechFace.Diagnostic;
        }
        void ToggleMute()
        {
            if(!ready||closing||mutePending)return;muteRequested=!muted;mutePending=true;
            _=connection.Command(muteRequested?"session.input_audio.mute":"session.input_audio.unmute");
            SetButton(mute,muteRequested?"Muting…":"Unmuting…");status.text=muteRequested?"Live · muting microphone…":"Live · unmuting microphone…";
        }
        public void Disconnect()
        {
            if(closing)return;
            if(!ready){Shutdown();if(status)status.text="Disconnected · microphone off";return;}
            closing=true;deadline=Time.unscaledTime+16;StopMic();ResetPlayback(false);status.text="Closing session · waiting for final usage…";
            _=connection.Command("session.close");
        }
        void Fail(string message){Shutdown();if(status)status.text=message;}
        void CompleteClose(){Shutdown();if(status)status.text="Session closed · final usage received";}
        void StopMic(){if(device!=null)Microphone.End(device);if(microphone)Destroy(microphone);microphone=null;device=null;}
        void Shutdown()
        {generation++;StopMic();connection?.Dispose();connection=null;ready=connecting=closing=muted=muteRequested=mutePending=finalUsageReceived=false;ResetPlayback(false);SetButton(connect,"Connect");SetButton(mute,"Mute mic");}
        static void SetButton(Button b,string text){if(b)b.GetComponentInChildren<Text>().text=text;}
        void OnApplicationPause(bool pause){if(pause){Shutdown();if(status)status.text="Paused · disconnected";}}
        void OnDisable(){Shutdown();if(Speaker)Speaker.Stop();if(outputClip)Destroy(outputClip);}
    }
}
