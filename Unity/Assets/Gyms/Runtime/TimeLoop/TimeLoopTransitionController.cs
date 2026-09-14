using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class TimeLoopTransitionController : MonoBehaviour
    {
        [Serializable] public sealed class Stage
        {
            public string Name; [Min(0)] public float Duration;
            public AnimationCurve Easing=AnimationCurve.EaseInOut(0,0,1,1);
            public Stage(string name,float duration){Name=name;Duration=duration;}
        }
        public Stage[] Stages={new Stage("Freeze",.2f),new Stage("Grooves",.6f),new Stage("Rewind begins",.6f),new Stage("Recent history",1.5f),new Stage("Spiral inward",.8f),new Stage("Loop closes",1),new Stage("Reset cover",.3f),new Stage("Return",1)};
        public Camera GameplayCamera;
        public LoopResetAdapter ResetAdapter;
        public LoopRewindActor[] Actors=Array.Empty<LoopRewindActor>();
        public Behaviour[] WorldActivity=Array.Empty<Behaviour>();
        public TextMesh[] GameplayLabels=Array.Empty<TextMesh>();
        public CanvasGroup[] GameplayUI=Array.Empty<CanvasGroup>();
        public AudioSource[] Music=Array.Empty<AudioSource>();
        public AudioSource[] GameplayAudio=Array.Empty<AudioSource>();
        [Tooltip("Optional explicit music mix; otherwise preserve the pre-transition volumes.")]
        public float[] ReturnMusicVolumes=Array.Empty<float>();
        [Min(0)] public float MusicStartPosition;
        public AudioClip RewindClip,CloseClip;
        [Range(0,1)] public float TransitionVolume=.35f;
        public Sprite EmblemSprite;
        public Font LoopTitleFont;
        public bool ReducedMotion;
        public bool EnableDebugInput=true;
        public KeyCode DebugKey=KeyCode.F8;
        public Vector2 EffectCenter=new Vector2(.5f,.5f);
        public Transform MusicSourceCenter;
        [Range(0,1)] public float GrooveOpacity=1;
        [Range(0,2)] public float Rotation=1,Distortion=1,Blur=1;
        [Range(0,1)] public float SpiralContraction=1,TonearmVisibility=1;
        [Range(0,2)] public float Glow=1;
        [Range(0,.9f)] public float Darkening=.48f;
        public UnityEvent TransitionStarted=new UnityEvent(),CheckpointRestored=new UnityEvent(),TransitionCompleted=new UnityEvent();
        public static TimeLoopTransitionController Active {get;private set;}
        public static bool InputBlocked=>Active && Active.IsTransitioning;
        public bool IsTransitioning {get;private set;}
        public bool LastResetSucceeded {get;private set;}
        public string LastError {get;private set;}
        public int StageIndex {get;private set;}=-1;
        public int SuccessfulResets {get;private set;}
        public float Progress {get;private set;}
        public Material EffectMaterial {get;private set;}
        LoopActivityLease activity;
        readonly Dictionary<LoopRewindActor,LoopRewindActor.Pose> before=new Dictionary<LoopRewindActor,LoopRewindActor.Pose>();
        struct UIState{public CanvasGroup Group;public float Alpha;public bool Interactable,Raycasts;}
        struct SoundState{public AudioSource Source;public bool Playing;public float Volume,Position;}
        readonly Dictionary<TextMesh,Color> labelColors=new Dictionary<TextMesh,Color>();
        readonly Dictionary<Renderer,bool> labelBackings=new Dictionary<Renderer,bool>();
        readonly List<UIState> ui=new List<UIState>();
        readonly List<SoundState> sounds=new List<SoundState>();
        readonly List<Image> viewportBars=new List<Image>();
        Canvas overlay;Text title,subtitle;Image emblem;AudioSource fx;Font fallbackSerif;
        bool frozen,ownsFont,musicStarted;float effectClock;Vector2 center;
        Coroutine running;IEnumerator resetRoutine;LoopResetResult resetResult;
        public void ConfigureRendererMaterial()
        {
            if(EffectMaterial)return;
            var shader=Resources.Load<Shader>("Rewind/TimeLoopFullscreen");
            if(!shader || !shader.isSupported)throw new InvalidOperationException("Time-loop fullscreen shader is unavailable.");
            EffectMaterial=new Material(shader){name="Time loop runtime material",hideFlags=HideFlags.DontSave};
        }
        void Update(){if(EnableDebugInput && DebugKey!=KeyCode.None && Input.GetKeyDown(DebugKey))TriggerLoop();}
        [ContextMenu("Trigger loop transition")]
        public void TriggerLoop()
        {
            if(!Application.isPlaying || IsTransitioning || Active || !ResetAdapter || !ResetAdapter.CanTrigger)return;
            try {ConfigureRendererMaterial();}catch(Exception ex){LastError=ex.Message;Debug.LogError(ex.Message,this);return;}
            if(!GameplayCamera)GameplayCamera=Camera.main;
            if(!GameplayCamera){LastError="Assign a gameplay camera.";return;}
            if(Stages==null || Stages.Length!=8){LastError="Eight stage settings are required.";return;}
            IsTransitioning=true;Active=this;LastResetSucceeded=false;LastError=null;musicStarted=false;effectClock=0;
            running=StartCoroutine(Run());
        }
        IEnumerator Run()
        {
            try
            {
                Freeze();MakeUI();TransitionStarted.Invoke();
                for(int stage=0;stage<8;stage++)
                {
                    StageIndex=stage;
                    if(stage==2 && RewindClip)fx.PlayOneShot(RewindClip,TransitionVolume);
                    if(stage==5)
                    {
                        // Opaque cover first, reset command second. Exactly one command per run.
                        SetEffect(.85f,1);title.text="";subtitle.text="";yield return null;
                        resetResult=new LoopResetResult();
                        try{resetRoutine=ResetAdapter.RestoreCheckpoint(resetResult);}catch(Exception ex){resetResult.Error=ex.Message;}
                        while(resetRoutine!=null)
                        {
                            bool more=false;
                            try{more=resetRoutine.MoveNext();}catch(Exception ex){resetResult.Error=ex.Message;}
                            if(!more)break;
                            yield return resetRoutine.Current;
                        }
                        (resetRoutine as IDisposable)?.Dispose();resetRoutine=null;
                        if(!resetResult.Success)
                        {
                            LastError=resetResult.Error??"Checkpoint restoration did not succeed.";
                            title.text="REWIND PAUSED";subtitle.text=LastError;
                            Debug.LogError(LastError,this);
                            var cancel=new GameObject("Cancel failed rewind",typeof(RectTransform),typeof(Image),typeof(Button));
                            cancel.transform.SetParent(overlay.transform,false);
                            Position((RectTransform)cancel.transform,.08f,.15f);
                            cancel.GetComponent<Image>().color=new Color(.15f,.025f,.07f);
                            cancel.GetComponent<Button>().onClick.AddListener(CancelTransition);
                            var label=MakeText(cancel.transform,"Cancel label",22);label.text="Return to game";Position(label.rectTransform,0,1);
                            // Never reveal a partially restored checkpoint. Cancel is explicit.
                            while(IsTransitioning)yield return null;
                            yield break;
                        }
                        LastResetSucceeded=true;SuccessfulResets++;CheckpointRestored.Invoke();
                        title.text="LOOP "+resetResult.LoopNumber.ToString("D2");subtitle.text="You remember what happened.";
                        title.font=LoopTitleFont?LoopTitleFont:fallbackSerif;title.fontSize=40;
                        Position(title.rectTransform,.24f,.34f);
                        if(CloseClip)fx.PlayOneShot(CloseClip,TransitionVolume*.65f);
                    }
                    if(stage==7)StartMusic();
                    float duration=Mathf.Max(0,Stages[stage].Duration),elapsed=0;
                    do
                    {
                        float raw=duration>0?Mathf.Clamp01(elapsed/duration):1;
                        float eased=Stages[stage].Easing==null?raw:Mathf.Clamp01(Stages[stage].Easing.Evaluate(raw));
                        Animate(stage,eased);
                        yield return null;float dt=Time.unscaledDeltaTime;elapsed+=dt;effectClock+=dt;
                    }while(elapsed<duration);
                    Animate(stage,1);
                }
            }
            finally { Cleanup(); }
            TransitionCompleted.Invoke();
        }
        void Freeze()
        {
            if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            frozen=true;activity=new LoopActivityLease();
            before.Clear();foreach(var actor in Actors)if(actor && !before.ContainsKey(actor)){before.Add(actor,actor.Capture());actor.Pause();}
            foreach(var behaviour in WorldActivity)if(behaviour && behaviour!=this && behaviour!=ResetAdapter)activity.Pause(behaviour);
            labelColors.Clear();labelBackings.Clear();
            foreach(var label in GameplayLabels)if(label)
            {
                labelColors[label]=label.color;
                foreach(var renderer in label.GetComponentsInChildren<Renderer>(true))if(renderer.gameObject!=label.gameObject)
                {labelBackings[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
            }
            ui.Clear();foreach(var group in GameplayUI)if(group){ui.Add(new UIState{Group=group,Alpha=group.alpha,Interactable=group.interactable,Raycasts=group.blocksRaycasts});group.interactable=false;group.blocksRaycasts=false;}
            sounds.Clear();var seen=new HashSet<AudioSource>();
            foreach(var list in new[]{Music,GameplayAudio})foreach(var source in list)if(source && seen.Add(source))
            {sounds.Add(new SoundState{Source=source,Playing=source.isPlaying,Volume=source.volume,Position=source.clip?source.time:0});source.Pause();}
            ResetAdapter.BeginFreeze();
            foreach(var entry in before)if(entry.Key)entry.Key.Apply(entry.Value);
            center=EffectCenter;
            if(MusicSourceCenter){var point=GameplayCamera.WorldToViewportPoint(MusicSourceCenter.position);if(point.z>0)center=new Vector2(Mathf.Clamp01(point.x),Mathf.Clamp01(point.y));}
        }
        void MakeUI()
        {
            var go=new GameObject("Time loop transition UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=32000;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
            var blocker=new GameObject("Input shield",typeof(RectTransform),typeof(Image));blocker.transform.SetParent(go.transform,false);var rt=(RectTransform)blocker.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;blocker.GetComponent<Image>().color=Color.clear;
            // Partial-viewport cameras do not clear the bars outside their rect.
            // Cover those pixels explicitly so faded HUD pixels cannot persist there.
            var viewport=GameplayCamera.rect;
            AddViewportBar(go.transform,"Left viewport cover",Vector2.zero,new Vector2(viewport.xMin,1));
            AddViewportBar(go.transform,"Right viewport cover",new Vector2(viewport.xMax,0),Vector2.one);
            AddViewportBar(go.transform,"Bottom viewport cover",new Vector2(viewport.xMin,0),new Vector2(viewport.xMax,viewport.yMin));
            AddViewportBar(go.transform,"Top viewport cover",new Vector2(viewport.xMin,viewport.yMax),new Vector2(viewport.xMax,1));
            title=MakeText(go.transform,"Rewind title",24);subtitle=MakeText(go.transform,"Loop memory",22);Position(title.rectTransform,.45f,.55f);Position(subtitle.rectTransform,.18f,.25f);
            title.text="";subtitle.text="";
            var emblemGO=new GameObject("Loop emblem",typeof(RectTransform),typeof(Image));emblemGO.transform.SetParent(go.transform,false);emblem=emblemGO.GetComponent<Image>();emblem.sprite=EmblemSprite;emblem.preserveAspect=true;emblem.raycastTarget=false;var er=emblem.rectTransform;er.anchorMin=er.anchorMax=new Vector2(.5f,.595f);er.sizeDelta=new Vector2(190,190);emblem.color=Color.clear;
            fallbackSerif=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","Times","Noto Serif"},40);
            ownsFont=fallbackSerif;
            if(!fallbackSerif)fallbackSerif=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fx=go.AddComponent<AudioSource>();fx.playOnAwake=false;fx.spatialBlend=0;fx.ignoreListenerPause=true;
        }
        void AddViewportBar(Transform parent,string name,Vector2 min,Vector2 max)
        {
            if(max.x-min.x<.0001f || max.y-min.y<.0001f)return;
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.raycastTarget=false;image.color=Color.clear;
            image.rectTransform.anchorMin=min;image.rectTransform.anchorMax=max;image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
            viewportBars.Add(image);
        }
        static Text MakeText(Transform parent,string name,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.color=Color.white;return text;
        }
        static void Position(RectTransform rect,float bottom,float top){rect.anchorMin=new Vector2(.08f,bottom);rect.anchorMax=new Vector2(.92f,top);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static readonly float[] StageProgress={0,.06f,.50f,.60f,.72f,.85f,1,1,1};
        void Animate(int stage,float t)
        {
            foreach(var bar in viewportBars)if(bar)bar.color=new Color(0,0,0,stage==0?t:stage==7?1-t:1);
            float[] boundaries=StageProgress;
            float progress=Mathf.Lerp(boundaries[stage],boundaries[stage+1],t);
            // Stage 5 cover guarantees black even when an external reset takes longer.
            if(stage==5)progress=Mathf.Lerp(.85f,1,t);
            SetEffect(progress,stage==7?1-t:1);
            if(stage==0 || stage==7)foreach(var pair in labelColors)if(pair.Key){var color=pair.Value;color.a*=stage==0?1-t:t;pair.Key.color=color;}
            if(stage==7)foreach(var pair in labelBackings)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            if(stage==0)foreach(var entry in ui)if(entry.Group)entry.Group.alpha=entry.Alpha*(1-t);
            if(stage==3)foreach(var actor in Actors)if(actor)actor.Replay(ReducedMotion?0:t);
            if(stage==2){title.text="R E W I N D I N G . . .";title.color=new Color(1,1,1,t);}
            if(stage==3)title.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)));
            if(stage==4)title.text="";
            if(stage>=5){title.color=new Color(1,.92f,.94f,stage==7?1-t:stage==6?1:Mathf.Clamp01(t*3));subtitle.color=new Color(.8f,.73f,.77f,stage==7?1-t:stage==6?1:Mathf.Clamp01(t*3));}
            if(EmblemSprite){float alpha=stage==5?Mathf.SmoothStep(0,1,t):stage==6?1-t:0;emblem.color=new Color(1,.2f,.55f,alpha);}
            if(stage>=6)EffectMaterial.SetFloat("_HasEmblem",stage==6?t:1); // procedural emblem fades, title stays.
            if(stage==7){foreach(var entry in ui)if(entry.Group)entry.Group.alpha=entry.Alpha*t;FadeMusic(t);}
        }
        void SetEffect(float progress,float opacity)
        {
            Progress=progress;EffectMaterial.SetFloat("_Progress",progress);EffectMaterial.SetFloat("_Opacity",opacity);
            EffectMaterial.SetFloat("_Aspect",GameplayCamera.aspect);EffectMaterial.SetVector("_EffectCenter",new Vector4(center.x,center.y,0,0));
            EffectMaterial.SetFloat("_EffectClock",effectClock);EffectMaterial.SetFloat("_ReducedMotion",ReducedMotion?1:0);
            EffectMaterial.SetFloat("_GrooveOpacity",GrooveOpacity);EffectMaterial.SetFloat("_RotationStrength",Rotation);EffectMaterial.SetFloat("_DistortionStrength",Distortion);EffectMaterial.SetFloat("_BlurStrength",Blur);EffectMaterial.SetFloat("_SpiralStrength",SpiralContraction);EffectMaterial.SetFloat("_TonearmVisibility",TonearmVisibility);EffectMaterial.SetFloat("_Glow",Glow);EffectMaterial.SetFloat("_DimStrength",Darkening);EffectMaterial.SetFloat("_HasEmblem",EmblemSprite?1:0);
        }
        void StartMusic()
        {
            musicStarted=true;
            for(int i=0;i<Music.Length;i++){var source=Music[i];if(!source || !source.clip)continue;bool originallyPlaying=sounds.Exists(x=>x.Source==source&&x.Playing);if(!originallyPlaying)continue;source.Stop();source.time=Mathf.Clamp(MusicStartPosition,0,Mathf.Max(0,source.clip.length-.01f));source.volume=0;source.Play();}
        }
        void FadeMusic(float fraction)
        {
            for(int i=0;i<Music.Length;i++){var source=Music[i];if(!source)continue;float volume=i<ReturnMusicVolumes.Length?ReturnMusicVolumes[i]:sounds.Find(x=>x.Source==source).Volume;source.volume=volume*fraction;}
        }
        [ContextMenu("Cancel transition")]
        public void CancelTransition(){if(!IsTransitioning)return;if(running!=null)StopCoroutine(running);Cleanup();}
        void Cleanup()
        {
            if(!IsTransitioning)return;
            (resetRoutine as IDisposable)?.Dispose();resetRoutine=null;
            foreach(var entry in before)if(entry.Key){if(!LastResetSucceeded)entry.Key.Apply(entry.Value);entry.Key.Resume(LastResetSucceeded);}
            before.Clear();activity?.Dispose();activity=null;
            foreach(var pair in labelColors)if(pair.Key)pair.Key.color=pair.Value;labelColors.Clear();
            foreach(var pair in labelBackings)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;labelBackings.Clear();
            foreach(var entry in ui)if(entry.Group){entry.Group.alpha=entry.Alpha;entry.Group.interactable=entry.Interactable;entry.Group.blocksRaycasts=entry.Raycasts;}ui.Clear();
            foreach(var saved in sounds)if(saved.Source)
            {
                bool isMusic=Array.IndexOf(Music,saved.Source)>=0;
                saved.Source.volume=saved.Volume;
                if(saved.Playing){if(isMusic && LastResetSucceeded){if(!musicStarted){saved.Source.time=MusicStartPosition;saved.Source.Play();}}else saved.Source.UnPause();}
            }
            if(LastResetSucceeded)FadeMusic(1);sounds.Clear();
            if(frozen){frozen=false;try{ResetAdapter.EndFreeze(LastResetSucceeded);}catch(Exception ex){Debug.LogException(ex,this);}}
            if(overlay)Destroy(overlay.gameObject);overlay=null;viewportBars.Clear();
            if(fallbackSerif && ownsFont)Destroy(fallbackSerif);fallbackSerif=null;
            IsTransitioning=false;StageIndex=-1;running=null;if(Active==this)Active=null;
        }
        void OnDisable(){CancelTransition();}
        void OnDestroy(){CancelTransition();if(EffectMaterial)Destroy(EffectMaterial);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ClearActive(){Active=null;}
    }
}
