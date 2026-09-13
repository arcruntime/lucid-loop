namespace LucidLoop.Gyms.Mvp
{
    // Authored checkpoint rules. AI will later propose actions against these same rules.
    public sealed class LoopState
    {
        public int Loop { get; private set; } = 1;
        public bool Recognized { get; private set; }
        public bool MayaWaiting { get; private set; }
        public bool PrivateApproach { get; private set; }
        public bool LucaPrepared { get; private set; }
        public bool Intimate { get; private set; }
        public bool Resolved { get; private set; }
        public bool RemembersRecording { get; private set; }
        public bool CanPrevent => Loop > 1 && PrivateApproach && LucaPrepared;
        public bool Recognize() { if (Recognized || Resolved) return false; Recognized = true; return true; }
        public bool Wait(bool value) { if (Loop == 1 || Recognized || Resolved) return false; MayaWaiting = value; return true; }
        public void PrepareMaya() { if (Loop > 1 && !Recognized && !Resolved) PrivateApproach = true; }
        public void PrepareLuca() { if (Loop > 1 && !Recognized && !Resolved) LucaPrepared = true; }
        public void SetMusic(bool intimate) { if (Loop > 1 && !Resolved) Intimate = intimate; }
        public bool Resolve() { if (!Recognized || !CanPrevent) return false; Resolved = true; return true; }
        public void Rewind()
        {
            RemembersRecording |= Recognized;
            Loop++; Recognized = false; MayaWaiting = false; PrivateApproach = false;
            LucaPrepared = false; Intimate = false; Resolved = false;
        }
    }
}
