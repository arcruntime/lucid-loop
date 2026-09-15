using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class EncounterLoopResetAdapter : LoopResetAdapter
    {
        public EncounterCoordinator Coordinator;
        public EncounterVoiceController Voice;
        public EncounterMoodPresentation Mood;
        readonly Dictionary<LoopRewindActor,LoopRewindActor.Pose> startPoses=new Dictionary<LoopRewindActor,LoopRewindActor.Pose>();
        public void CaptureVisualCheckpoint(LoopRewindActor[] actors)
        {if(startPoses.Count>0)return;foreach(var actor in actors)if(actor)startPoses[actor]=actor.Capture();}
        public override bool CanTrigger=>Coordinator && Coordinator.IsReady && !Coordinator.TransitionLocked &&
            (Coordinator.State.Phase=="catastrophe" || Coordinator.State.Phase=="unresolved" || Coordinator.State.Phase=="victory");
        public override int LoopNumber=>Coordinator?Coordinator.State.LoopIndex:1;
        public override void BeginFreeze()
        {
            if(Mood){Mood.TransitionOwnsAudio=true;GetComponent<TimeLoopTransitionController>().ReturnMusicVolumes=new[]{Mood.MusicVolume,0f};}
            if(Voice)Voice.Leave();
            Coordinator.AcquireTransitionLock();
        }
        public override IEnumerator RestoreCheckpoint(LoopResetResult result)
        {
            string oldLoop=Coordinator.State.LoopId;
            // Server owns loop clock, routines, interaction state, knowledge and increment.
            // Await the pause acknowledgement so reset uses the current revision.
            float deadline=Time.realtimeSinceStartup+15;
            while(Coordinator.IsReady && !Coordinator.IsPaused && Time.realtimeSinceStartup<deadline)yield return null;
            if(!Coordinator.IsReady || !Coordinator.IsPaused || !Coordinator.Reset())
            {result.Error="Cannot reach the loop checkpoint server. Cancel and reconnect.";yield break;}
            deadline=Time.realtimeSinceStartup+20;
            while(Coordinator.IsReady && (Coordinator.State.LoopId==oldLoop || !Coordinator.WorldMatchesLoop) && Time.realtimeSinceStartup<deadline)
            {
                if(Coordinator.Status=="reset_unavailable"){result.Error="The server rejected this reset.";yield break;}
                yield return null;
            }
            if(!Coordinator.IsReady || Coordinator.State.LoopId==oldLoop || !Coordinator.WorldMatchesLoop)
            {result.Error="Waiting for a complete checkpoint failed. Cancel and reconnect.";yield break;}
            foreach(var pair in startPoses)if(pair.Key)pair.Key.Apply(pair.Value);
            Coordinator.ApplyCheckpointWorld();
            if(Mood)Mood.RestoreLoopMix();
            result.LoopNumber=LoopNumber;result.Success=true;
        }
        public override void EndFreeze(bool successful)
        {
            if(Coordinator)Coordinator.ReleaseTransitionLock();
            if(Mood)Mood.TransitionOwnsAudio=false;
        }
    }
}
