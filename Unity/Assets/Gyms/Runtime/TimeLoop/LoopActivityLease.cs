using System;
using System.Collections.Generic;
using UnityEngine;
namespace LucidLoop.Gyms
{
    // Cooperative ownership: nested pause owners must release their own lease.
    public sealed class LoopActivityLease : IDisposable
    {
        sealed class Entry { public int Count; public bool WasEnabled; }
        static readonly Dictionary<Behaviour,Entry> Owners=new Dictionary<Behaviour,Entry>();
        readonly List<Behaviour> owned=new List<Behaviour>();
        public void Pause(Behaviour component)
        {
            if(!component || owned.Contains(component))return;
            if(!Owners.TryGetValue(component,out var entry))Owners.Add(component,entry=new Entry{WasEnabled=component.enabled});
            entry.Count++;component.enabled=false;owned.Add(component);
        }
        public void Dispose()
        {
            foreach(var component in owned)
            {
                if(!Owners.TryGetValue(component,out var entry))continue;
                if(--entry.Count==0){Owners.Remove(component);if(component && entry.WasEnabled)component.enabled=true;}
            }
            owned.Clear();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear(){Owners.Clear();}
    }
}
