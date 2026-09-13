using UnityEngine;
namespace LucidLoop.Gyms.Mvp
{
    public sealed class MoodMusic : MonoBehaviour
    {
        AudioSource aggressive,intimate;
        public bool Intimate, Duck, Muted;
        float transition;
        public bool ClipsReady => aggressive && intimate && aggressive.clip && intimate.clip;
        void Awake()
        {
            aggressive=gameObject.AddComponent<AudioSource>();intimate=gameObject.AddComponent<AudioSource>();
            aggressive.clip=Resources.Load<AudioClip>("MvpAudio/Aggressive");intimate.clip=Resources.Load<AudioClip>("MvpAudio/Intimate");
            foreach(var s in new[]{aggressive,intimate}){s.loop=true;s.playOnAwake=false;s.spatialBlend=0;s.volume=0;}
            RestartTracks();
        }
        public void RestartTracks()
        {
            Intimate=false;transition=0;
            foreach(var s in new[]{aggressive,intimate}) { if(!s)continue;s.Stop();if(s.clip){s.time=0;s.Play();} }
        }
        void Update()
        {
            transition=Mathf.MoveTowards(transition,Intimate?1:0,Time.unscaledDeltaTime/1.2f);
            float gain=Muted?0:Duck?.14f:.36f;
            aggressive.volume=gain*Mathf.Sqrt(1-transition);intimate.volume=gain*Mathf.Sqrt(transition);
        }
        public void Interrupt(){aggressive.Stop();intimate.Stop();}
    }
}
