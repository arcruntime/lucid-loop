using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms.Mvp
{
    public partial class FirstLoop
    {
        LiveConnection voice;
        readonly AudioRingBuffer voicePlayback=new AudioRingBuffer(24000*3);
        AudioSource voiceSpeaker;
        AudioClip voiceMic,voiceOutput;
        string voiceDevice;
        int voiceCursor,voiceEpoch;
        bool voiceReady,voiceMuted,voiceStarting;
        float voiceDeadline;volatile float voiceLevel;
        Button voiceButton,voiceMuteButton;
        ChatTurn voiceUserTurn,voiceActorTurn;
        readonly System.Collections.Generic.HashSet<string> committedVoiceTurns=new System.Collections.Generic.HashSet<string>();
        ChatState CurrentChatState()=>new ChatState{loop=State.Loop,intimate=State.Intimate,waiting=State.MayaWaiting,privateApproach=State.PrivateApproach,lucaPrepared=State.LucaPrepared,recognized=State.Recognized,resolved=State.Resolved,inVip=State.InVip};
        void VoiceControls()
        {
            choices.sizeDelta=new Vector2(620,chatActor==Theo?700:635);
            voiceButton=GymUI.Button(GymUI.Box(choices,"Start voice",20,570,270,48),"Start voice",()=>{if(voice!=null||voiceStarting)StopVoice();else StartCoroutine(StartVoice());});
            voiceMuteButton=GymUI.Button(GymUI.Box(choices,"Mute voice",305,570,290,48),"Mute mic",()=>{
                if(!voiceReady)return;voiceMuted=!voiceMuted;
                _=voice.Command(voiceMuted?"session.input_audio.mute":"session.input_audio.unmute");
                voiceMuteButton.GetComponentInChildren<Text>().text=voiceMuted?"Unmute mic":"Mute mic";
            });
            voiceMuteButton.interactable=false;
            if(vipButton&&chatActor==Theo)vipButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(307.5f,-660);
        }
        IEnumerator StartVoice()
        {
            if(sending||!modal||!chatActor)yield break;
            voiceStarting=true;int epoch=++voiceEpoch;int loop=State.Loop;var actor=chatActor;
            chatNotice.text="Requesting microphone access… Use headphones.";
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(epoch!=voiceEpoch||!modal||actor!=chatActor||loop!=State.Loop)yield break;
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)||Microphone.devices.Length==0){VoiceFailure("No microphone permission/device. Typing is still available.");yield break;}
            voiceDevice=Microphone.devices[0];
            try{voiceMic=Microphone.Start(voiceDevice,true,1,24000);}catch{VoiceFailure("Could not open microphone. Typing is still available.");yield break;}
            if(!voiceMic||voiceMic.frequency!=24000){VoiceFailure("Microphone must support 24 kHz. Typing is still available.");yield break;}
            if(!voiceSpeaker)voiceSpeaker=gameObject.AddComponent<AudioSource>();
            voiceOutput=AudioClip.Create("MVP voice playback",24000,1,24000,true,data=>{float sum=0;for(int i=0;i<data.Length;i++){data[i]=voicePlayback.Read();sum+=data[i]*data[i];}voiceLevel=Mathf.Sqrt(sum/Mathf.Max(1,data.Length));});
            voiceSpeaker.clip=voiceOutput;voiceSpeaker.loop=true;voiceSpeaker.spatialBlend=0;voiceSpeaker.volume=1;voiceSpeaker.Play();
            voiceCursor=0;voiceReady=false;voiceMuted=false;voiceDeadline=Time.unscaledTime+20;
            voiceUserTurn=voiceActorTurn=null;committedVoiceTurns.Clear();
            voice=new LiveConnection();
            var start=JObject.FromObject(new ChatRequest{character=actor.Id,message="Begin.",history=ModelHistory(actor.Id),state=CurrentChatState()});start["type"]="mvp.start";
            _=voice.Connect("ws://127.0.0.1:8082/mvp-live",start);
            chatInput.interactable=sendButton.interactable=false;
            voiceButton.GetComponentInChildren<Text>().text="Stop voice";
            chatNotice.text="Connecting voice… Microphone starts when ready.";
        }
        void UpdateVoice()
        {
            if(voice==null)return;
            if(!modal||Busy||!chatActor){StopVoice();return;}
            var active=voice;int budget=80;
            while(budget-->0&&active.TryRead(out var e))
            {
                HandleVoice(e);if(voice!=active)return;
            }
            if(!voiceReady&&Time.unscaledTime>voiceDeadline){VoiceFailure("Voice startup timed out. Check the dialogue server, or type instead.");return;}
            SetActing(chatActor.DisplayName,voiceLevel>.015f);
            if(!voiceReady||!voiceMic)return;
            int position=Microphone.GetPosition(voiceDevice);
            if(position<0){VoiceFailure("Microphone disconnected. Typing is still available.");return;}
            int count=(position-voiceCursor+voiceMic.samples)%voiceMic.samples;
            if(count>12000){voiceCursor=(position-480+voiceMic.samples)%voiceMic.samples;count=480;}
            while(count>=480){var samples=new float[480];voiceMic.GetData(samples,voiceCursor);if(!voiceMuted)_=voice.SendAudio(AudioRingBuffer.Encode(samples,480));voiceCursor=(voiceCursor+480)%voiceMic.samples;count-=480;}
        }
        void HandleVoice(JObject e)
        {
            string type=(string)e["type"];
            if(type=="mvp.ready"){
                voiceReady=true;voiceStarting=false;voiceCursor=Microphone.GetPosition(voiceDevice);
                voiceMuteButton.interactable=true;chatNotice.text="Live · listening · you can interrupt. AI-generated character voice.";
            }
            else if(type=="session.output_audio.delta"){
                try{voicePlayback.WritePcm16(Convert.FromBase64String((string)e["delta"]??""));}catch{VoiceFailure("Could not play voice audio. Please retry.");}
            }
            else if(type=="session.input_transcript.delta"||type=="session.output_transcript.delta"){
                bool user=type=="session.input_transcript.delta";
                var history=conversations[chatActor.Id];
                if(user){if(voiceUserTurn==null){voiceUserTurn=new ChatTurn("user","",State.Loop);history.Add(voiceUserTurn);voiceActorTurn=null;}voiceUserTurn.content+=(string)e["delta"]??"";ObserverPending(chatActor,voiceUserTurn.content);}
                else {if(voiceActorTurn==null){voiceActorTurn=new ChatTurn("assistant","",State.Loop);history.Add(voiceActorTurn);}voiceActorTurn.content+=(string)e["delta"]??"";}
                ShowHistory(chatActor.Id);
            }
            else if(type=="mvp.decision"){
                string id=(string)e["id"];
                if(string.IsNullOrEmpty(id)||committedVoiceTurns.Contains(id))return;
                bool valid=(string)e["character"]==chatActor.Id&&(int?)e["loop"]==State.Loop&&(string)e["source"]=="openai";
                string[] actions=null;try{actions=e["actions"]?.ToObject<string[]>();}catch{}
                valid=valid&&actions!=null&&State.ApplyDecision(chatActor.Id,actions);
                if(valid){committedVoiceTurns.Add(id);Stop(agents[Maya]);lastDecision=DescribeActions(actions);chatNotice.text=lastDecision;if(chatActor==Theo&&vipButton)vipButton.gameObject.SetActive(State.VipInvited&&!State.InVip);Refresh();}
                ObserverDecision(chatActor,voiceUserTurn?.content,actions,valid);
                _=voice.Send(new JObject{{"type","mvp.commit"},{"id",id},{"accepted",valid},{"state",JObject.FromObject(CurrentChatState())}});
            }
            else if(type=="mvp.committed"){voiceUserTurn=null;voiceActorTurn=null;}
            else if(type=="mvp.notice")chatNotice.text=(string)e["message"];
            else if(type=="gym.status"&&(string)e["status"]=="error")VoiceFailure("Voice unavailable. Restart the updated dialogue server or type instead.");
            else if(type=="gym.transport.closed"||type=="session.closed")VoiceFailure("Voice session ended · microphone off. Typing is available.");
        }
        void VoiceFailure(string message){ObserverFailed("Voice session ended.");StopVoice();if(chatNotice)chatNotice.text=message;}
        void StopVoice()
        {
            if(voice!=null&&observerResult&&observerResult.text.StartsWith("Interpreting request"))ObserverFailed("Voice request cancelled.");
            voiceEpoch++;voiceStarting=voiceReady=voiceMuted=false;voiceLevel=0;SetActing("",false);
            if(voiceDevice!=null)Microphone.End(voiceDevice);voiceDevice=null;
            if(voiceMic)Destroy(voiceMic);voiceMic=null;
            voice?.Dispose();voice=null;voicePlayback.Clear();
            if(voiceSpeaker){voiceSpeaker.Stop();voiceSpeaker.clip=null;}
            if(voiceOutput)Destroy(voiceOutput);voiceOutput=null;
            voiceUserTurn=voiceActorTurn=null;
            if(voiceButton)voiceButton.GetComponentInChildren<Text>().text="Start voice";
            if(voiceMuteButton){voiceMuteButton.interactable=false;voiceMuteButton.GetComponentInChildren<Text>().text="Mute mic";}
            if(chatInput)chatInput.interactable=true;if(sendButton)sendButton.interactable=!sending;
        }
        IEnumerator VoiceSmoke()
        {
            OpenConversation(Maya);
            Canvas.ForceUpdateCanvases();
            var artBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hud,portrait);
            var chatBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hud,choices);
            if(artBounds.Intersects(chatBounds))throw new System.Exception("Portrait overlaps conversation controls");
            Debug.Log("BTD_PORTRAIT_LAYOUT_OK: portrait and conversation panel do not overlap");
            voice=new LiveConnection();voiceReady=true;
            HandleVoice(new JObject{{"type","session.input_transcript.delta"},{"delta","Please wait here."}});
            var eventData=JObject.Parse("{\"type\":\"mvp.decision\",\"id\":\"fixture-turn\",\"loop\":2,\"character\":\"maya\",\"source\":\"openai\",\"actions\":[\"wait\"]}");
            HandleVoice(eventData);if(!observerResult.text.Contains("APPLIED")||!observerRequest.text.Contains("Please wait here."))throw new Exception("Observer missed committed voice action");
            if(!State.MayaWaiting)throw new Exception("Voice action did not apply");
            Capture("11-observer-applied");
            State.Wait(false);HandleVoice(eventData);if(State.MayaWaiting)throw new Exception("Duplicate voice action applied twice");
            eventData["id"]="old-loop";eventData["loop"]=1;HandleVoice(eventData);if(State.MayaWaiting)throw new Exception("Old loop voice action applied");
            HandleVoice(new JObject{{"type","session.output_transcript.delta"},{"delta","Sure. Right here."}});
            if(!chatTranscript.text.Contains("Please wait here.")||!chatTranscript.text.Contains("Sure. Right here."))throw new Exception("Voice captions were lost");
            Capture("10-voice-controls");CloseConversation();
            if(voice!=null||voiceMic||voiceReady)throw new Exception("Voice survived closing conversation");
            conversations[Maya.Id].Clear();
            yield return null;
            Debug.Log("BTD_VOICE_UI_SMOKE_OK: controls, captions, committed action, duplicate/old-loop rejection and cleanup; fixture only, not microphone or live API");
        }
        void OnApplicationPause(bool paused){if(paused)StopVoice();}
    }
}
