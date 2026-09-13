namespace LucidLoop.Gyms.Mvp
{
    // Authored checkpoint rules. AI proposes supported actions; these rules validate and apply them.
    public sealed class LoopState
    {
        public int Loop { get; private set; } = 1;
        public bool Recognized { get; private set; }
        public bool MayaWaiting { get; private set; }
        public bool PrivateApproach { get; private set; }
        public bool LucaPrepared { get; private set; }
        public bool Intimate { get; private set; }
        public bool VipInvited { get; private set; }
        public bool InVip { get; private set; }
        public bool ArriveVip(){if(!VipInvited || Recognized || Resolved)return false;InVip=true;VipInvited=false;return true;}
        public void LeaveVip(){InVip=false;VipInvited=false;}
        public bool Resolved { get; private set; }
        public bool RemembersRecording { get; private set; }
        public bool CanPrevent => Loop > 1 && PrivateApproach && LucaPrepared;
        public bool Recognize() { if (Recognized || Resolved) return false; Recognized = true; return true; }
        public bool Wait(bool value) { if (Loop == 1 || Recognized || Resolved) return false; MayaWaiting = value; return true; }
        public void PrepareMaya() { if (Loop > 1 && !Recognized && !Resolved) PrivateApproach = true; }
        public void PrepareLuca() { if (Loop > 1 && !Recognized && !Resolved) LucaPrepared = true; }
        public void SetMusic(bool intimate) { if (Loop > 1 && !Resolved) Intimate = intimate; }
        public bool ApplyDecision(string character, string[] actions)
        {
            if(character!="maya" && character!="luca" && character!="ren" && character!="theo")return false;
            if(Loop<2 || Recognized || Resolved || actions==null || actions.Length<1 || actions.Length>3) return false;
            var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var a in actions)
            {
                if(!seen.Add(a))return false;
                bool valid=a=="none" || character=="maya"&&(a=="wait"||a=="follow"||a=="private_approach")
                    || character=="theo"&&a=="invite_vip"&&!InVip || character=="luca"&&a=="prepare_intervention" || character=="ren"&&(a=="music_intimate"||a=="music_aggressive");
                if(!valid)return false;
            }
            if((seen.Contains("none")&&actions.Length>1)||(seen.Contains("wait")&&seen.Contains("follow"))
                ||(seen.Contains("music_intimate")&&seen.Contains("music_aggressive")))return false;
            foreach(var a in actions)switch(a)
            {
                case "invite_vip":VipInvited=true;break;
                case "wait":Wait(true);break;case "follow":Wait(false);break;
                case "private_approach":PrepareMaya();break;case "prepare_intervention":PrepareLuca();break;
                case "music_intimate":SetMusic(true);break;case "music_aggressive":SetMusic(false);break;
            }
            return true;
        }
        public bool Resolve() { if (!Recognized || !CanPrevent) return false; Resolved = true; return true; }
        public void Rewind()
        {
            RemembersRecording |= Recognized;
            Loop++; Recognized = false; MayaWaiting = false; PrivateApproach = false;
            LucaPrepared = false; Intimate = false; Resolved = false; VipInvited=false;InVip=false;
        }
    }
}
