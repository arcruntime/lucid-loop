using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class LocalLoopCheckpoint : LoopResetAdapter
    {
        public LoopRewindActor[] Actors=Array.Empty<LoopRewindActor>();
        public LoopCheckpointParticipant[] Participants=Array.Empty<LoopCheckpointParticipant>();
        [SerializeField] int loopNumber=1;
        public float LoopClock;
        public List<string> DiscoveredClueIds=new List<string>();
        public List<string> UnlockedDialogueIds=new List<string>();
        readonly Dictionary<LoopRewindActor,LoopRewindActor.Pose> checkpoint=new Dictionary<LoopRewindActor,LoopRewindActor.Pose>();
        readonly Dictionary<LoopCheckpointParticipant,object> participantStates=new Dictionary<LoopCheckpointParticipant,object>();
        float startClock;bool frozen;
        public override int LoopNumber=>loopNumber;
        public override bool CanTrigger=>checkpoint.Count>0 && !frozen;
        void Start(){CaptureCheckpoint();}
        void Update(){if(!frozen)LoopClock+=Time.deltaTime;}
        [ContextMenu("Capture loop-start checkpoint")]
        public void CaptureCheckpoint()
        {
            if(frozen)return;checkpoint.Clear();participantStates.Clear();
            foreach(var actor in Actors)if(actor)checkpoint[actor]=actor.Capture();
            foreach(var participant in Participants)if(participant)participantStates[participant]=participant.CaptureLoopState();
            startClock=LoopClock;
        }
        public override void BeginFreeze(){frozen=true;}
        public override IEnumerator RestoreCheckpoint(LoopResetResult result)
        {
            var rollback=new Dictionary<LoopCheckpointParticipant,object>();
            try
            {
                foreach(var pair in participantStates){if(!pair.Key)throw new InvalidOperationException("A checkpoint participant was removed.");rollback[pair.Key]=pair.Key.CaptureLoopState();}
                foreach(var pair in participantStates)pair.Key.RestoreLoopState(pair.Value);
                foreach(var pair in checkpoint){if(!pair.Key)throw new InvalidOperationException("A checkpoint actor was removed.");pair.Key.Apply(pair.Value);}
                LoopClock=startClock;loopNumber++;result.Success=true;result.LoopNumber=loopNumber;
            }
            catch(Exception ex)
            {
                foreach(var pair in rollback)try{pair.Key.RestoreLoopState(pair.Value);}catch(Exception rollbackError){Debug.LogException(rollbackError);}
                result.Error=ex.Message;
            }
            yield break;
        }
        public override void EndFreeze(bool successful){frozen=false;}
    }
}
