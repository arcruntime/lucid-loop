using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class EncounterVictoryPresentation : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public VictoryScreenController Screen;
        public bool ReturnToConnectionOnContinue = true;
        public Behaviour[] AdditionalPausedActivity = new Behaviour[0];
        readonly Dictionary<Behaviour,bool> activity = new Dictionary<Behaviour,bool>();
        readonly Dictionary<Canvas,bool> canvases = new Dictionary<Canvas,bool>();
        readonly Dictionary<Renderer,bool> labels = new Dictionary<Renderer,bool>();
        string shownLoop;
        EncounterHud hud;
        bool wired;
        void Start()
        {
            Coordinator = Coordinator ? Coordinator : GetComponent<EncounterCoordinator>();
            hud = GetComponent<EncounterHud>();
            Screen = Screen ? Screen : GetComponent<VictoryScreenController>();
            if (!Screen) Screen = gameObject.AddComponent<VictoryScreenController>();
            Screen.Shown.AddListener(Freeze); Screen.Closed.AddListener(Restore);
            Screen.Continued.AddListener(ReturnToConnection);
            if(Coordinator) Coordinator.StateChanged += CheckState;
            wired=true;
            if(Coordinator) CheckState(Coordinator.State);
        }
        void CheckState(EncounterClientState state)
        {
            ObserveCompletion(state.Phase,state.LoopId);
        }
        public void ObserveCompletion(string phase,string loopId)
        {
            if(!Screen || !VictoryScreenController.VictoryPhase(phase) || shownLoop==loopId) return;
            shownLoop=loopId; Screen.ShowVictoryScreen();
        }
        void Pause(Behaviour b) { if(!b || activity.ContainsKey(b)) return; activity.Add(b,b.enabled); b.enabled=false; }
        void Freeze()
        {
            GetComponent<EncounterVoiceController>()?.Leave();
            if(Coordinator) { Coordinator.CancelPendingConversation(); }
            if(EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(canvas.isRootCanvas && !canvas.GetComponentInParent<VictoryScreenController>())
                { canvases[canvas]=canvas.enabled; canvas.enabled=false; }
            Pause(hud);
            foreach(var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            { var r=text.GetComponent<Renderer>(); if(r) { labels[r]=r.enabled; r.enabled=false; } }
            foreach(var actor in FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)) Pause(actor);
            foreach(var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            { if(agent.enabled && agent.isOnNavMesh) agent.ResetPath(); Pause(agent); }
            foreach(var b in FindObjectsByType<NpcActionExecutor>(FindObjectsSortMode.None)) Pause(b);
            foreach(var b in FindObjectsByType<EncounterPrimitivePresentation>(FindObjectsSortMode.None)) Pause(b);
            foreach(var b in AdditionalPausedActivity) Pause(b);
        }
        void Restore()
        {
            foreach(var pair in activity) if(pair.Key) pair.Key.enabled=pair.Value;
            activity.Clear();
            foreach(var pair in labels) if(pair.Key) pair.Key.enabled=pair.Value;
            labels.Clear();
            foreach(var pair in canvases) if(pair.Key) pair.Key.enabled=pair.Value;
            canvases.Clear();
        }
        void ReturnToConnection()
        {
            if(!ReturnToConnectionOnContinue)return;
            // Existing main-menu equivalent; persistent server knowledge is not reset.
            if(Coordinator) Coordinator.Disconnect();
            if(hud) hud.ShowConnectionSetup();
        }
        void OnDisable() { if(Screen) Screen.HideVictoryScreen(); Restore(); }
        void OnDestroy()
        {
            if(!wired)return;
            if(Coordinator)Coordinator.StateChanged-=CheckState;
            if(Screen) { Screen.Shown.RemoveListener(Freeze); Screen.Closed.RemoveListener(Restore); Screen.Continued.RemoveListener(ReturnToConnection); }
            Restore();
        }
    }
}
