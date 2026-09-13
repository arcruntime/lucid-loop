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
        [Serializable] public class ChatTurn { public string role,content; public int loop; public ChatTurn(string r,string c,int l){role=r;content=c;loop=l;} }
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
        ScrollRect transcriptScroll;
        Text musicNotice;
        string lastDecision="";
        void ShowTypedConversation(CharacterActor actor)
        {
            chatActor=actor;
            choices.pivot=new Vector2(0,1);choices.anchoredPosition=new Vector2(25,-155);
            choices.sizeDelta=new Vector2(620,570);
            GymUI.Label(choices,"Speak in your own words · scroll for earlier messages",20,57,570,35,20,GymUI.Muted);
            var viewport=GymUI.Box(choices,"Conversation scroll",20,105,575,225);
            GymUI.Panel(viewport,new Color(.035f,.04f,.075f,1));viewport.gameObject.AddComponent<RectMask2D>();
            transcriptContent=GymUI.Rect(viewport,"Transcript",new Vector2(0,1),Vector2.one,new Vector2(0,-225),Vector2.zero);
            transcriptContent.pivot=new Vector2(.5f,1);
            chatTranscript=GymUI.Text(transcriptContent,"",22,Color.white);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();transcriptScroll=scroll;scroll.viewport=viewport;scroll.content=transcriptContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            if(!conversations.ContainsKey(actor.Id))conversations[actor.Id]=new List<ChatTurn>();
            ShowHistory(actor.Id);
            var r=GymUI.Box(choices,"Your words",20,350,575,88);GymUI.Panel(r,new Color(.08f,.1f,.16f));
            var t=GymUI.Text(GymUI.Rect(r,"Input text",Vector2.zero,Vector2.one,new Vector2(12,8),new Vector2(-12,-8)),"",23,Color.white);
            chatInput=r.gameObject.AddComponent<InputField>();chatInput.textComponent=t;chatInput.characterLimit=800;chatInput.lineType=InputField.LineType.MultiLineSubmit;
            var hint=GymUI.Text(GymUI.Rect(r,"Placeholder",Vector2.zero,Vector2.one,new Vector2(12,8),new Vector2(-12,-8)),"Ask a question or propose what to do…",21,GymUI.Muted);chatInput.placeholder=hint;
            sendButton=GymUI.Button(GymUI.Box(choices,"Send",20,451,270,55),"Say it",SendChat);
            leaveButton=GymUI.Button(GymUI.Box(choices,"Leave",305,451,290,55),"End conversation",CloseConversation);
            chatNotice=GymUI.Label(choices,"Live AI · local dialogue server required",20,518,575,43,18,GymUI.Muted);
            chatInput.ActivateInputField();
        }
        void ShowHistory(string id)
        {
            var history=conversations[id];var parts=new List<string>();
            int shownLoop=-1;
            foreach(var turn in history)
            {
                if(turn.loop!=shownLoop){shownLoop=turn.loop;parts.Add("— LOOP "+shownLoop+" —");}
                parts.Add((turn.role=="user"?"YOU":id.ToUpperInvariant())+": "+turn.content);
            }
            chatTranscript.text=parts.Count==0?"Tell them what you need. They can agree, question your approach, or refuse.":string.Join("\n\n",parts);
            Canvas.ForceUpdateCanvases();
            transcriptContent.sizeDelta=new Vector2(0,Mathf.Max(225,chatTranscript.preferredHeight+12));
            Canvas.ForceUpdateCanvases();
            transcriptScroll.verticalNormalizedPosition=0;
        }
        ChatTurn[] ModelHistory(string id)
        {
            var current=conversations[id].FindAll(turn=>turn.loop==State.Loop);
            return current.GetRange(Math.Max(0,current.Count-12),Math.Min(12,current.Count)).ToArray();
        }
        void ShowMusicMenu()
        {
            chatActor=Ren;
            choices.pivot=new Vector2(0,1);choices.anchoredPosition=new Vector2(25,-155);
            choices.sizeDelta=new Vector2(620,420);
            GymUI.Label(choices,"Request a track",20,57,575,35,22,GymUI.Muted);
            GymUI.Button(GymUI.Box(choices,"Intimate track",20,110,575,70),"Intimate · warm and spacious",()=>SelectMusic(true));
            GymUI.Button(GymUI.Box(choices,"Aggressive track",20,195,575,70),"Aggressive · driving and intense",()=>SelectMusic(false));
            musicNotice=GymUI.Label(choices,"",20,280,575,50,21,GymUI.Cyan);
            GymUI.Button(GymUI.Box(choices,"Leave DJ",20,345,575,55),"End conversation",CloseConversation);
            UpdateMusicMenu();
        }
        void SelectMusic(bool intimate)
        {
            State.SetMusic(intimate);music.Intimate=State.Intimate;Refresh();UpdateMusicMenu();
        }
        void UpdateMusicMenu()
        {
            if(!modal || chatActor!=Ren || !musicNotice || !music)return;
            musicNotice.text=(music.IsTransitioning?"Ren is mixing into ":"Now playing: ")+(State.Intimate?"Intimate":"Aggressive")+".";
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
            var payload=new ChatRequest{character=actor.Id,message=message,history=ModelHistory(actor.Id),state=new ChatState{
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
                    var history=conversations[actor.Id];history.Add(new ChatTurn("user",message,State.Loop));history.Add(new ChatTurn("assistant",decision.reply,State.Loop));
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
            Capture("04-maya-ai");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-btd-fixture")>=0)
            {
                string first=conversations[Maya.Id][0].content;
                for(int i=0;i<7;i++)yield return RequestDecision(Maya,"Wait right here for history check "+i);
                CloseConversation();OpenConversation(Maya);
                if(conversations[Maya.Id].Count!=16 || !chatTranscript.text.Contains(first) || ModelHistory(Maya.Id).Length!=12)
                    throw new Exception("Full transcript or bounded model history failed");
                transcriptScroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();Capture("06-history-top");
                transcriptScroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();Capture("07-history-bottom");
                // Archive remains readable across a reset, but no old turn reaches the NPC.
                State.Rewind();ShowHistory(Maya.Id);
                if(!chatTranscript.text.Contains(first) || ModelHistory(Maya.Id).Length!=0)throw new Exception("Prior loop history leaked or disappeared");
                State.Wait(true);State.PrepareMaya();
            }
            CloseConversation();
            OpenConversation(Luca);
            yield return RequestDecision(Luca,"Theo may get upset with Maya. Please help us step aside early and calmly before anyone gets physical.");
            if(!State.LucaPrepared)throw new Exception("Luca AI decision failed: "+chatNotice.text);
            CloseConversation();OpenConversation(Ren);
            SelectMusic(true);
            yield return new WaitForSeconds(1.5f);
            if(music.IsTransitioning || !music.IntimateAudible)throw new Exception("DJ transition did not reach audible Intimate track");
            Capture("05-dj-menu");
            if(!State.Intimate)throw new Exception("Ren AI decision failed: "+chatNotice.text);
            CloseConversation();OpenConversation(Maya);
            yield return RequestDecision(Maya,"Come with me now, please. Let's go talk to Theo together.");
            if(State.MayaWaiting)throw new Exception("Follow AI decision failed: "+chatNotice.text);
            CloseConversation();
            Debug.Log("BTD_DIALOGUE_SMOKE_OK: HTTP responses applied to waiting, private approach, intervention, music and following");
        }
    }
}
