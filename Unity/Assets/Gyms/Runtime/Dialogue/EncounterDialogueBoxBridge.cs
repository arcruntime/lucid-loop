using Newtonsoft.Json.Linq;
using UnityEngine;
namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class EncounterDialogueBoxBridge : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public EncounterVoiceController Voice;
        public DialogueBoxController Box;
        public CanvasGroup LegacyConversation;
        int epoch;string text="";bool nextResponse=true,receivingUser=false;string userText="";
        void Start()
        {
            Coordinator=Coordinator?Coordinator:GetComponent<EncounterCoordinator>();Voice=Voice?Voice:GetComponent<EncounterVoiceController>();
            Box=Box?Box:GetComponent<DialogueBoxController>();if(!Box)Box=gameObject.AddComponent<DialogueBoxController>();
            if(!Coordinator || !Voice){enabled=false;return;}
            Coordinator.ConversationRequested+=Open;Coordinator.ConversationInvalidated+=Invalidate;
            Voice.Ready+=Ready;Voice.StatusChanged+=Status;Voice.TranscriptFragment+=Fragment;
            Voice.StreamStarted+=Begin;Voice.StreamStopped+=Stop;Voice.Pcm16Output+=Push;Voice.PlaybackProgress+=Progress;
            Box.Submitted+=Submit;Box.MicrophoneChanged+=Microphone;Box.Closed+=Close;
        }
        void Open(EncounterConversationRequest request)
        {
            if(!isActiveAndEnabled)return;
            string name=request.CharacterId,role="";
            foreach(var actor in Coordinator.Characters)if(actor && actor.Id==name){name=actor.DisplayName;role=actor.Role;break;}
            Box.Open(name,role,true);epoch=Box.Epoch;userText="";receivingUser=false;text="";nextResponse=true;Box.SetInputAvailable(false);Box.SetStatus("Connecting...");
            if(LegacyConversation){LegacyConversation.alpha=0;LegacyConversation.blocksRaycasts=LegacyConversation.interactable=false;}
        }
        void Ready(){if(Box.IsOpen){Box.SetInputAvailable(true);Box.SetStatus("");}}
        void Submit(string value){receivingUser=false;userText="";nextResponse=true;if(!Voice.SendText(value))Box.SetStatus("Could not send. Please retry when connected.");}
        void Microphone(bool value)=>Voice.EnableMicrophone(value);
        void Fragment(JObject fragment)
        {
            if(!Box.IsOpen)return;
            string type=(string)fragment["type"],role=(string)fragment["role"];
            if(role=="user" || type=="session.input_transcript.delta")
            {
                if(!receivingUser){userText="";receivingUser=true;}
                userText+=(string)fragment["text"]??(string)fragment["delta"]??"";
                if(userText.Length>4000)userText=userText.Substring(userText.Length-4000);
                Box.SetPlayerTranscript(userText,epoch);nextResponse=true;return;
            }
            receivingUser=false;
            if(nextResponse){text="";nextResponse=false;}
            text+=(string)fragment["text"]??(string)fragment["delta"]??"";
            if(text.Length>24000)text=text.Substring(text.Length-24000);
            Box.SetResponse(text,epoch);
        }
        void Begin(int generation)=>Box.Meter.Begin(generation);
        void Push(byte[] pcm,int generation){if(Box.IsOpen)Box.Meter.Push(pcm,generation);}
        void Progress(int generation,long consumed,int rate,bool starved,bool ended){if(Box.IsOpen)Box.Meter.Advance(generation,consumed,ended);}
        void Stop(int generation)=>Box.InterruptSpeech();
        void Status(string value){if(!Box.IsOpen)return;Box.SetMicrophoneState(Voice.MicrophoneEnabled);if(!Voice.IsReady){Box.SetStatus(value.Replace('_',' '));Box.SetInputAvailable(false);}}
        void Invalidate()=>Box.Close();
        void Close(){Voice.EnableMicrophone(false);Voice.Leave();if(LegacyConversation){LegacyConversation.alpha=1;LegacyConversation.blocksRaycasts=LegacyConversation.interactable=true;}}
        void OnDisable(){if(Box)Box.Close();}
        void OnDestroy()
        {
            if(Coordinator){Coordinator.ConversationRequested-=Open;Coordinator.ConversationInvalidated-=Invalidate;}
            if(Voice){Voice.Ready-=Ready;Voice.StatusChanged-=Status;Voice.TranscriptFragment-=Fragment;Voice.StreamStarted-=Begin;Voice.StreamStopped-=Stop;Voice.Pcm16Output-=Push;Voice.PlaybackProgress-=Progress;}
            if(Box){Box.Submitted-=Submit;Box.MicrophoneChanged-=Microphone;Box.Closed-=Close;Box.Close();}
        }
    }
}
