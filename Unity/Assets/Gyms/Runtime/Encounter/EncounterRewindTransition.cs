using UnityEngine;
namespace LucidLoop.Gyms
{
    // Compatibility entry point for the existing HUD. The reusable controller owns playback.
    public sealed class EncounterRewindTransition : MonoBehaviour, ITimeLoopSceneProvider
    {
        public bool IsPlaying=>controller && controller.IsTransitioning;
        public float Progress=>controller?controller.Progress:0;
        public const float SpinSeconds=3.7f;
        EncounterCoordinator coordinator;EncounterVoiceController voice;Canvas hudCanvas;
        TimeLoopTransitionController controller;EncounterLoopResetAdapter live;LocalLoopCheckpoint preview;
        public void Initialize(EncounterCoordinator owner,EncounterVoiceController speech,Canvas hud)
        {coordinator=owner;voice=speech;hudCanvas=hud;}
        public void ConfigureTimeLoop(TimeLoopTransitionController target)
        {
            if(target.IsTransitioning)return;
            controller=target;TimeLoopAutoSetup.ConfigureCommon(controller,Camera.main);
            if(hudCanvas)
            {
                var group=TimeLoopAutoSetup.Ensure<CanvasGroup>(hudCanvas.gameObject);
                var groups=new System.Collections.Generic.List<CanvasGroup>(controller.GameplayUI);
                if(!groups.Contains(group))groups.Add(group);controller.GameplayUI=groups.ToArray();
            }
            live=TimeLoopAutoSetup.Ensure<EncounterLoopResetAdapter>(target.gameObject);
            live.Coordinator=coordinator;live.Voice=voice;live.Mood=FindFirstObjectByType<EncounterMoodPresentation>();
            live.CaptureVisualCheckpoint(controller.Actors);controller.ResetAdapter=live;
            if(live.Mood){controller.Music=live.Mood.LoopSources;controller.ReturnMusicVolumes=new[]{live.Mood.MusicVolume,0f};}
        }
        void Ensure()
        {
            if(controller)return;
            var existing=FindFirstObjectByType<TimeLoopTransitionController>();
            if(!existing)existing=new GameObject("Time loop transition").AddComponent<TimeLoopTransitionController>();
            ConfigureTimeLoop(existing);
        }
        public void Request(){Ensure();if(IsPlaying)return;controller.ResetAdapter=live;controller.TriggerLoop();}
        public void Preview()
        {
            Ensure();if(IsPlaying)return;
            // Offline visual rehearsal is explicit: it never sends a server reset.
            if(coordinator && coordinator.IsReady){Debug.LogWarning("Use the live rewind at the end of this encounter; offline preview requires a disconnected game.");return;}
            preview=TimeLoopAutoSetup.Ensure<LocalLoopCheckpoint>(controller.gameObject);
            preview.Actors=controller.Actors;preview.CaptureCheckpoint();controller.ResetAdapter=preview;controller.TriggerLoop();
        }
        void OnDisable(){if(controller)controller.CancelTransition();}
    }
}
