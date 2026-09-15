using System;
using System.Collections;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class LoopResetResult { public bool Success; public int LoopNumber; public string Error; }
    public abstract class LoopResetAdapter : MonoBehaviour
    {
        public abstract bool CanTrigger { get; }
        public abstract int LoopNumber { get; }
        public virtual void BeginFreeze(){}
        public abstract IEnumerator RestoreCheckpoint(LoopResetResult result);
        public virtual void EndFreeze(bool successful){}
    }
    // Implement this for game-specific routines, clocks, doors or interactables.
    public abstract class LoopCheckpointParticipant : MonoBehaviour
    {
        public abstract object CaptureLoopState();
        public abstract void RestoreLoopState(object state);
    }
}
