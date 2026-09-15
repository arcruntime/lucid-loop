using System;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public sealed class LoopObjectCheckpoint : LoopCheckpointParticipant
    {
        public GameObject[] Objects=Array.Empty<GameObject>();
        public override object CaptureLoopState(){var state=new bool[Objects.Length];for(int i=0;i<state.Length;i++)state[i]=Objects[i]&&Objects[i].activeSelf;return state;}
        public override void RestoreLoopState(object state){var values=(bool[])state;for(int i=0;i<values.Length;i++)if(Objects[i])Objects[i].SetActive(values[i]);}
    }
}
