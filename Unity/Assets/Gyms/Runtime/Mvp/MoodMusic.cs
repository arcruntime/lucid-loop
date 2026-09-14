using UnityEngine;
namespace LucidLoop.Gyms.Mvp
{
    public sealed class MoodMusic : MonoBehaviour
    {
        AudioSource aggressive,intimate,transitionFx,rewindFx;
        public bool Intimate, Duck, Muted;
        bool previousMood;
        float transition, gain=.36f;
        public bool ClipsReady => aggressive && intimate && aggressive.clip && intimate.clip && transitionFx.clip;
        public bool RewindPlaying => rewindFx && rewindFx.isPlaying;
        public bool RewindClipReady => rewindFx && rewindFx.clip;
        public bool MusicSuppressedForRewind => RewindPlaying && aggressive.volume==0 && intimate.volume==0;
        public bool IsTransitioning => previousMood!=Intimate || Mathf.Abs(transition-(Intimate?1:0))>.001f;
        public bool IntimateAudible => intimate && intimate.isPlaying && intimate.volume>.01f;
        void Awake()
        {
            aggressive=gameObject.AddComponent<AudioSource>();intimate=gameObject.AddComponent<AudioSource>();
            transitionFx=gameObject.AddComponent<AudioSource>();rewindFx=gameObject.AddComponent<AudioSource>();
            aggressive.clip=Resources.Load<AudioClip>("MvpAudio/Aggressive");intimate.clip=Resources.Load<AudioClip>("MvpAudio/Intimate");
            transitionFx.clip=Resources.Load<AudioClip>("MvpAudio/Backspin");
            rewindFx.clip=Resources.Load<AudioClip>("MvpAudio/RecordScratch");
            foreach(var s in new[]{aggressive,intimate,transitionFx,rewindFx}){s.loop=s==aggressive||s==intimate;s.playOnAwake=false;s.spatialBlend=0;s.volume=0;}
            RestartTracks();
        }
        public void RestartTracks(bool preserveRewind=false)
        {
            Intimate=false;previousMood=false;transition=0;
            foreach(var s in new[]{aggressive,intimate}) { if(!s)continue;s.Stop();if(s.clip){s.time=0;s.Play();} }
            if(transitionFx)transitionFx.Stop();
            if(rewindFx&&!preserveRewind)rewindFx.Stop();
        }
        void Update()
        {
            if(Intimate!=previousMood)
            {
                previousMood=Intimate;
                // Cue the incoming track at its opening, not at a random point in a silent loop.
                var incoming=Intimate?intimate:aggressive;
                if(incoming.clip){incoming.time=0;if(!incoming.isPlaying)incoming.Play();}
                if(transitionFx.clip)transitionFx.Play();
            }
            transition=Mathf.MoveTowards(transition,Intimate?1:0,Time.unscaledDeltaTime/1.2f);
            gain=Mathf.MoveTowards(gain,Duck?.14f:.36f,Time.unscaledDeltaTime*.8f);
            float audibleGain=Muted||RewindPlaying?0:gain;
            aggressive.volume=audibleGain*Mathf.Sqrt(1-transition);intimate.volume=audibleGain*Mathf.Sqrt(transition);
            transitionFx.volume=audibleGain*.6f;
            rewindFx.volume=Muted?0:.38f;
        }
        public void Interrupt(){aggressive.Stop();intimate.Stop();transitionFx.Stop();rewindFx.Stop();
            if(rewindFx.clip){rewindFx.volume=Muted?0:.38f;rewindFx.Play();}}
    }
}
