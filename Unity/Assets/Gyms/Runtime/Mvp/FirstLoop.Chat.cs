using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace LucidLoop.Gyms.Mvp
{
    public partial class FirstLoop
    {
        [Serializable] public class ChatTurn { public string role,content; public ChatTurn(string r,string c){role=r;content=c;} }
        [Serializable] public class ChatState { public int loop;public bool intimate,waiting,privateApproach,lucaPrepared,recognized,resolved; }
        [Serializable] public class ChatRequest { public string character,message; public ChatState state; public ChatTurn[] history; }
        [Serializable] public class ChatDecision { public string reply,source,model,error; public string[] actions; }
        readonly Dictionary<string,List<ChatTurn>> conversations=new Dictionary<string,List<ChatTurn>>();
        UnityWebRequest activeRequest;
        int conversationEpoch;
        bool sending;
        InputField chatInput;
        Text chatTranscript, chatNotice;
        Button sendButton,leaveButton;
        CharacterActor chatActor;
        RectTransform transcriptContent;
        string lastDecision="";
        void ShowTypedConversation(CharacterActor actor)
        {
            chatActor=actor;
            choices.pivot=new Vector2(0,1);choices.anchoredPosition=new Vector2(25,-155);
            choices.sizeDelta=new Vector2(620,570);
            GymUI.Label(choices,"Speak in your own words",20,57,570,35,20,GymUI.Muted);
            var viewport=GymUI.Box(choices,"Conversation scroll",20,105,575,225);
            GymUI.Panel(viewport,new Color(.035f,.04f,.075f,1));viewport.gameObject.AddComponent<RectMask2D>();
            transcriptContent=GymUI.Rect(viewport,"Transcript",new Vector2(0,1),Vector2.one,new Vector2(0,-225),Vector2.zero);
            transcriptContent.pivot=new Vector2(.5f,1);
            chatTranscript=GymUI.Text(transcriptContent,"",22,Color.white);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=transcriptContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            if(!conversations.ContainsKey(actor.Id))conversations[actor.Id]=new List<ChatTurn>();
            ShowHistory(actor.Id);
            var r=GymUI.Box(choices,"Your words",20,350,575,88);GymUI.Panel(r,new Color(.08f,.1f,.16f));
            var t=GymUI.Text(GymUI.Rect(r,"Input text",Vector2.zero,Vector2.one,new Vector2(12,8),new Vector2(-12,-8)),"",23,Color.white);
            chatInput=r.gameObject.AddComponent<InputField>();chatInput.textComponent=t;chatInput.characterLimit=800;chatInput.lineType=InputField.LineType.MultiLineSubmit;
            var hint=GymUI.Text(GymUI.Rect(r,"Placeholder",Vector2.zero,Vector2.one,new Vector2(12,8),new Vector2(-12,-8)),"Ask a question or propose what to do…",21,GymUI.Muted);chatInput.placeholder=hint;
            sendButton=GymUI.Button(GymUI.Box(choices,"Send",20,451,270,55),"Say it",SendChat);
            leaveButton=GymUI.Button(GymUI.Box(choices,"Leave",305,451,290,55),"Back to floor",CloseConversation);
            chatNotice=GymUI.Label(choices,"Live AI · local dialogue server required",20,518,575,43,18,GymUI.Muted);
            chatInput.ActivateInputField();
        }
        void ShowHistory(string id)
        {
            var history=conversations[id];var parts=new List<string>();
            for(int i=Math.Max(0,history.Count-2);i<history.Count;i++) parts.Add((history[i].role=="user"?"YOU":id.ToUpperInvariant())+": "+history[i].content);
            chatTranscript.text=parts.Count==0?"Tell them what you need. They can agree, question your approach, or refuse.":string.Join("\n\n",parts);
            transcriptContent.sizeDelta=new Vector2(0,Mathf.Max(225,chatTranscript.preferredHeight+12));
            transcriptContent.anchoredPosition=Vector2.zero;
        }
        void SendChat()
        {
            if(sending || !chatInput || string.IsNullOrWhiteSpace(chatInput.text))return;
            StartCoroutine(RequestDecision(chatActor,chatInput.text.Trim()));
        }
        IEnumerator RequestDecision(CharacterActor actor,string message)
        {
            sending=true;sendButton.interactable=false;chatInput.interactable=false;
            chatNotice.text=actor.DisplayName+" is considering what you said…";
            int epoch=conversationEpoch;int loop=State.Loop;
            var payload=new ChatRequest{character=actor.Id,message=message,history=conversations[actor.Id].ToArray(),state=new ChatState{
                loop=State.Loop,intimate=State.Intimate,waiting=State.MayaWaiting,privateApproach=State.PrivateApproach,lucaPrepared=State.LucaPrepared,recognized=State.Recognized,resolved=State.Resolved}};
            string url=Array.IndexOf(Environment.GetCommandLineArgs(),"-btd-fixture")>=0?"http://127.0.0.1:8083/dialogue":"http://127.0.0.1:8082/dialogue";
            using(var request=new UnityWebRequest(url,"POST"))
            {
                activeRequest=request;request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=25;
                yield return request.SendWebRequest();
                if(activeRequest==request)activeRequest=null;
                if(epoch!=conversationEpoch || loop!=State.Loop || !modal || chatActor!=actor)yield break;
                ChatDecision decision=null;
                try{decision=JsonUtility.FromJson<ChatDecision>(request.downloadHandler.text);}catch{}
                if(request.result!=UnityWebRequest.Result.Success || decision==null || decision.source!="openai")
                    chatNotice.text=FailureMessage(decision?.error);
                else if(string.IsNullOrWhiteSpace(decision.reply) || decision.reply.Length>650 || !State.ApplyDecision(actor.Id,decision.actions))
                    chatNotice.text="That response couldn't be applied. Nothing changed; try a different request.";
                else
                {
                    Stop(agents[Maya]);
                    var history=conversations[actor.Id];history.Add(new ChatTurn("user",message));history.Add(new ChatTurn("assistant",decision.reply));
                    while(history.Count>12)history.RemoveRange(0,2);
                    ShowHistory(actor.Id);chatInput.text="";
                    lastDecision=DescribeActions(decision.actions);
                    chatNotice.text="AI response · "+lastDecision;
                    Refresh();
                }
            }
            sending=false;sendButton.interactable=true;chatInput.interactable=true;chatInput.ActivateInputField();
        }
        static string FailureMessage(string code)
        {
            switch(code){
                case "api_key_missing":return "Server has no API key. Start it with your key, then retry.";
                case "api_auth_failed":return "The server's API key was rejected. Check it in Terminal.";
                case "api_rate_or_quota_limit":return "API quota or rate limit reached. Nothing changed; retry later.";
                case "slow_down":return "Too many requests. Wait a moment, then retry.";
                case "model_refusal":return "No usable response. Nothing changed; try rephrasing.";
                default:return "Couldn't get an AI response. Check the dialogue server on port 8082. Nothing changed.";
            }
        }
        static string DescribeActions(string[] actions)
        {
            var words=new List<string>();foreach(var a in actions) switch(a){
                case "wait":words.Add("Maya will wait here");break;case "follow":words.Add("Maya will follow");break;
                case "private_approach":words.Add("Maya agreed to keep her phone away");break;
                case "prepare_intervention":words.Add("Luca will intervene early");break;
                case "music_intimate":words.Add("Ren changes to Intimate");break;case "music_aggressive":words.Add("Ren changes to Aggressive");break;
                default:words.Add("No action agreed yet");break;
            }return string.Join(" · ",words);
        }
        void CancelChat()
        {
            conversationEpoch++; if(activeRequest!=null){activeRequest.Abort();activeRequest=null;} sending=false;
        }
        void OnDisable(){CancelChat();}
        IEnumerator DialogueSmoke()
        {
            if(!music.ClipsReady)throw new Exception("Music clips not loaded");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-btd-fixture")>=0)
            {
                OpenConversation(Maya);StartCoroutine(RequestDecision(Maya,"DELAY_TEST"));
                yield return new WaitForSeconds(.1f);CloseConversation();yield return new WaitForSeconds(2.3f);
                if(State.MayaWaiting || State.PrivateApproach)throw new Exception("Cancelled action was applied");
            }
            OpenConversation(Maya);
            yield return RequestDecision(Maya,"Wait right here near the entrance. When we approach Theo, please keep your phone away and talk to him privately instead of recording him.");
            if(!State.MayaWaiting || !State.PrivateApproach)throw new Exception("Maya AI decision failed: "+chatNotice.text);
            Capture("04-maya-ai");CloseConversation();
            OpenConversation(Luca);
            yield return RequestDecision(Luca,"Theo may get upset with Maya. Please help us step aside early and calmly before anyone gets physical.");
            if(!State.LucaPrepared)throw new Exception("Luca AI decision failed: "+chatNotice.text);
            CloseConversation();OpenConversation(Ren);
            yield return RequestDecision(Ren,"Could you play something intimate and quieter so we can talk?");
            if(!State.Intimate)throw new Exception("Ren AI decision failed: "+chatNotice.text);
            CloseConversation();OpenConversation(Maya);
            yield return RequestDecision(Maya,"Come with me now, please. Let's go talk to Theo together.");
            if(State.MayaWaiting)throw new Exception("Follow AI decision failed: "+chatNotice.text);
            CloseConversation();
            Debug.Log("BTD_DIALOGUE_SMOKE_OK: HTTP responses applied to waiting, private approach, intervention, music and following");
        }
    }
}
