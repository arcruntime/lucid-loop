using Newtonsoft.Json.Linq;
using UnityEngine;
namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class EncounterInformationBridge : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public LearnedInformationManager Manager;
        public InformationNotificationUI UI;
        EncounterClientState observedState;
        string loopId;
        int loopIndex=-1;
        void Start()
        {
            Coordinator=Coordinator?Coordinator:GetComponent<EncounterCoordinator>();
            Manager=Manager?Manager:GetComponent<LearnedInformationManager>();
            if(!Manager)Manager=gameObject.AddComponent<LearnedInformationManager>();
            UI=UI?UI:GetComponent<InformationNotificationUI>();
            if(!UI)UI=gameObject.AddComponent<InformationNotificationUI>();
            UI.Initialize(Manager);
            if(Coordinator){Coordinator.StateChanged+=Observe;Observe(Coordinator.State);}
        }
        void Observe(EncounterClientState state)
        {
            if(!state.HasSnapshot)return;
            if(observedState!=null && observedState!=state)
            { Manager.ClearForNewGame();loopIndex=-1;loopId=null;UI.CloseAll(); }
            observedState=state;
            ObserveSnapshot(state.LoopIndex,state.LoopId,state.PlayerDiscoveries);
        }
        public void ObserveSnapshot(int index,string id,JArray discoveries)
        {
            if(!Manager || string.IsNullOrEmpty(id))return;
            if(loopIndex>=0 && (index<loopIndex || (index==loopIndex && id!=loopId)))
            {Manager.ClearForNewGame();loopIndex=-1;}
            bool restarted=loopIndex>=0 && index>loopIndex;
            if(restarted && UI)UI.CloseAll();
            foreach(var discovery in discoveries)
            {
                if(discovery==null || (discovery.Type!=JTokenType.String && !(discovery is JObject)))continue;
                string text=discovery.Type==JTokenType.String?(string)discovery:(string)discovery["text"];
                string fact=discovery.Type==JTokenType.String?"legacy:"+text:(string)discovery["factId"];
                if(string.IsNullOrWhiteSpace(text))text=fact;
                string learned=discovery is JObject?(string)discovery["learnedInLoop"]:null;
                bool retained=index>1 && learned!=id;
                Manager.AddLearnedInformation(fact,text,"clue",!retained);
            }
            if(restarted)Manager.NotifyLoopStarted();
            loopIndex=index;loopId=id;
        }
        void OnDestroy(){if(Coordinator)Coordinator.StateChanged-=Observe;}
    }
}
