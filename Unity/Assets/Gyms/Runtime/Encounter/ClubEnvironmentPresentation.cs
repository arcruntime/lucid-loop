using System;
using UnityEngine;

namespace LucidLoop.Gyms
{
    /// <summary>Scene-owned material instances keep lighting and emissive art in the same mood.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ClubEnvironmentPresentation : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public Material NeonMaterial;
        public Material ScreenMaterial;
        Material neon, screen;
        public Material DisplayMaterial=>screen?screen:ScreenMaterial;
        Renderer[] renderers;
        Material[][] originals;
        EncounterCoordinator bound;
        float intimacy, target;
        bool paused;

        void OnEnable()
        {
            renderers=GetComponentsInChildren<Renderer>(true);
            originals=new Material[renderers.Length][];
            if(NeonMaterial)neon=new Material(NeonMaterial);
            if(ScreenMaterial)screen=new Material(ScreenMaterial);
            for(int i=0;i<renderers.Length;i++)
            {
                originals[i]=renderers[i].sharedMaterials;
                var copy=(Material[])originals[i].Clone();
                for(int j=0;j<copy.Length;j++)
                {
                    if(copy[j]==NeonMaterial&&neon)copy[j]=neon;
                    if(copy[j]==ScreenMaterial&&screen)copy[j]=screen;
                }
                renderers[i].sharedMaterials=copy;
            }
            Bind();
        }
        void Bind()
        {
            if(bound==Coordinator)return;
            if(bound){bound.MoodChanged-=Mood;bound.PauseChanged-=Pause;}
            bound=Coordinator;
            if(bound){bound.MoodChanged+=Mood;bound.PauseChanged+=Pause;Mood(bound.State.Mood);}
        }
        void Mood(string value)
        {
            if(value=="Intimate")target=1;
            else if(value=="Aggressive")target=0;
        }
        void Pause(bool value)=>paused=value;
        void Update()
        {
            Bind();
            if(!paused)intimacy=Mathf.MoveTowards(intimacy,target,Time.unscaledDeltaTime/2.5f);
            ApplyPalette(neon,screen,intimacy);
        }
        void LateUpdate()
        {
            if(!Coordinator||Coordinator.Characters==null)return;
            foreach(var actor in Coordinator.Characters)
            {
                if(!actor)continue;
                var p=actor.transform.position;p.y=FloorHeight(p.x,p.z);actor.transform.position=p;
            }
        }
        public static float FloorHeight(float x,float z)
        {
            if(z>=6&&z<=11&&Mathf.Abs(x)<=7.5f)return .6f;
            if(z>=4.65f&&z<6&&Mathf.Abs(Mathf.Abs(x)-6)<=1)
                return Mathf.Min(.6f,.15f*(1+Mathf.Floor((z-4.65f)/.3f)));
            if(z>=-5.35f&&z<=3.55f)
            {
                if(x>=7.15f&&x<=13.2f)return .35f;
                if(x>=6.7f&&x<7.15f)return Mathf.Min(.35f,(1+Mathf.Floor((x-6.7f)/.15f))*.35f/3);
            }
            return 0;
        }
        public static void ApplyPalette(Material neon,Material screen,float intimacy)
        {
            var color=Color.Lerp(new Color(1,.018f,.18f),new Color(1,.12f,.39f),intimacy);
            if(neon){neon.SetColor("_BaseColor",color);neon.SetColor("_EmissionColor",color*Mathf.Lerp(3,2.1f,intimacy));}
            if(screen)
            {
                screen.SetColor("_AccentColor",color);
                screen.SetColor("_BaseColor",Color.Lerp(new Color(.24f,.012f,.62f),new Color(.30f,.04f,.49f),intimacy));
                screen.SetFloat("_Energy",Mathf.Lerp(.45f,.32f,intimacy));
            }
        }
        void OnDisable()
        {
            if(bound){bound.MoodChanged-=Mood;bound.PauseChanged-=Pause;bound=null;}
            if(renderers!=null)for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].sharedMaterials=originals[i];
            if(neon)Destroy(neon);if(screen)Destroy(screen);
            neon=null;screen=null;paused=false;
        }
    }
}
