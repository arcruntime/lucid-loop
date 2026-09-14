using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LucidLoop.Gyms.Mvp
{
    public partial class FirstLoop : MonoBehaviour
    {
        public NavMeshAgent Player;
        public GymCamera Rig;
        public CharacterActor Maya, Theo, Luca, Ren;
        public Transform Partner;
        public LoopState State { get; private set; } = new LoopState();
        public bool Busy { get; private set; }
        public bool AutoAdvance;
        readonly Dictionary<CharacterActor, NavMeshAgent> agents = new Dictionary<CharacterActor, NavMeshAgent>();
        readonly Dictionary<Transform, Vector3> starts = new Dictionary<Transform, Vector3>();
        readonly Dictionary<Transform, Quaternion> rotations = new Dictionary<Transform, Quaternion>();
        Text status, objective, memories, speaker, line;
        RectTransform dialogue, choices, hud;
        Image curtain;
        Button next;
        bool advance, modal;
        float followAt;
        GameObject phone;
        CharacterActor approaching;
        MoodMusic music;

        public string LastMoveError { get; private set; }

        void Start()
        {
            Application.targetFrameRate = 30;
            Application.runInBackground = true;
            foreach (var a in new[] { Maya, Theo, Luca })
            {
                var obstacle = a.GetComponent<NavMeshObstacle>(); if (obstacle) obstacle.enabled = false;
                var agent = a.GetComponent<NavMeshAgent>();
                if (!agent) agent = a.gameObject.AddComponent<NavMeshAgent>();
                agent.radius = .3f; agent.height = 2; agent.speed = 3.6f;
                agent.acceleration = 18; agent.angularSpeed = 540; agent.stoppingDistance = .15f;
                agents[a] = agent;
            }
            gameObject.AddComponent<ClubArt>().Dress(this);
            foreach (var t in new[] {Player.transform, Maya.transform, Theo.transform, Luca.transform, Ren.transform})
            { starts[t] = t.position; rotations[t] = t.rotation; }
            MakeUI();
            MakePortrait();
            MakeObserver();
            StartCoroutine(LoadStoryAudio());
            music=gameObject.AddComponent<MoodMusic>();
            phone = GameObject.CreatePrimitive(PrimitiveType.Cube); phone.name = "Maya recording phone";
            Destroy(phone.GetComponent<Collider>()); phone.transform.SetParent(Maya.Visual, false);
            phone.transform.localPosition = new Vector3(.42f, 1.5f, .3f); phone.transform.localScale = new Vector3(.13f,.23f,.035f);
            phone.SetActive(false);
            Refresh();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-btd-art-reference")>=0){StartCoroutine(ArtReference());return;}
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-btd-smoke") >= 0)
            { AutoAdvance = true; StartCoroutine(Smoke()); }
            else StartCoroutine(Arrival());
        }
        void MakeUI()
        {
            hud = GymUI.Canvas("Before the Drop checkpoint");
            var header = GymUI.Box(hud,"Header",25,20,790,115); GymUI.Panel(header,GymUI.Ink);
            GymUI.Label(header,"BEFORE THE DROP",20,12,740,36,30);
            status = GymUI.Label(header,"",20,54,740,40,20,GymUI.Cyan);
            var journal = GymUI.Rect(hud,"Memory",new Vector2(1,1),Vector2.one,new Vector2(-560,-170),new Vector2(-25,-20));
            GymUI.Panel(journal,GymUI.Ink); GymUI.Label(journal,"WHAT YOU REMEMBER",18,10,490,30,19,GymUI.Cyan);
            memories = GymUI.Label(journal,"",18,47,490,88,21);
            var foot = GymUI.Rect(hud,"Objective",Vector2.zero,new Vector2(1,0),new Vector2(25,20),new Vector2(-25,112));
            GymUI.Panel(foot,GymUI.Ink); objective = GymUI.Label(foot,"",18,12,1750,68,24);
            choices = GymUI.Box(hud,"Conversation choices",25,155,505,420); GymUI.Panel(choices,GymUI.Ink); choices.gameObject.SetActive(false);
            dialogue = GymUI.Rect(hud,"Story",new Vector2(.15f,0),new Vector2(.85f,0),new Vector2(0,135),new Vector2(0,365));
            GymUI.Panel(dialogue,GymUI.Ink); speaker = GymUI.Label(dialogue,"",24,15,900,36,25,GymUI.Cyan);
            line = GymUI.Label(dialogue,"",24,60,1250,92,28);
            next = GymUI.Button(GymUI.Box(dialogue,"Continue",1040,163,230,48),"Continue →",()=>advance=true);
            dialogue.gameObject.SetActive(false);
            var restart = GymUI.Rect(hud,"Restart",new Vector2(1,0),new Vector2(1,0),new Vector2(-240,122),new Vector2(-25,172));
            GymUI.Button(restart,"Restart demo",Restart);
            var mute=GymUI.Rect(hud,"Music toggle",new Vector2(1,0),new Vector2(1,0),new Vector2(-465,122),new Vector2(-250,172));
            GymUI.Button(mute,"Music on / off",()=>{if(music)music.Muted=!music.Muted;});
            var shade = GymUI.Rect(hud,"Rewind curtain",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            curtain = GymUI.Panel(shade,new Color(.1f,.8f,.85f,0)); curtain.raycastTarget=false;
        }
        void Update()
        {
            UpdateVoice();
            if (!Busy && !modal && !State.Resolved)
            {
                if (Input.GetMouseButtonDown(0) && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                {
                    if (Physics.Raycast(Rig.Camera.ScreenPointToRay(Input.mousePosition),out var hit,150))
                    {
                        var actor = hit.collider.GetComponentInParent<CharacterActor>();
                        if (actor && !actor.IsPlayer) Approach(actor);
                        else Walk(hit.point);
                    }
                }
                if (!State.MayaWaiting && Time.time > followAt)
                {
                    followAt = Time.time + .3f;
                    if (Vector3.Distance(Maya.transform.position,Player.transform.position) > 1.4f)
                        Move(agents[Maya],Player.transform.position + Vector3.left*.85f);
                }
                if (CanRecognizeAffair()) BeginEncounter();
                if (!Busy && approaching && !Player.pathPending && Player.remainingDistance < .35f)
                { var actor=approaching; approaching=null; OpenConversation(actor); }
            }
            if (Input.GetKeyDown(KeyCode.Escape) && modal && !Busy) CloseConversation();
            if (Input.GetKeyDown(KeyCode.Space) && Busy) advance = true;
            UpdateMusicMenu();
            if(music) { music.Intimate=State.Intimate; music.Duck=Busy || (modal && chatActor!=Ren) || (storySpeaker&&storySpeaker.isPlaying); }
        }
        bool CanRecognizeAffair()=>InRecognitionArea(Maya.transform.position) && Vector3.Distance(Theo.transform.position,Partner.position)<1.8f;
        public static bool InRecognitionArea(Vector3 position) => ExpandedClub.Recognition(position);
        public bool Walk(Vector3 point)
        {
            if (Busy || modal || State.Resolved) return false;
            approaching=null;
            // The first entrance route is authored; the second opens the full floor.
            if (State.Loop == 1)
            {
                point = ExpandedClub.ClampOpening(point);
            }
            bool moved=Move(Player,point);
            if(!moved) objective.text=LastMoveError;
            return moved;
        }
        void Approach(CharacterActor actor)
        {
            if(State.Loop==1) { OpenConversation(actor); return; }
            float best=float.MaxValue; Vector3 destination=Vector3.zero; bool found=false;
            for(int i=0;i<16;i++)
            {
                Vector3 candidate=actor.transform.position + Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*1.6f;
                if(!NavMesh.SamplePosition(candidate,out var hit,.8f,NavMesh.AllAreas)) continue;
                var path=new NavMeshPath();
                if(!Player.CalculatePath(hit.position,path) || path.status!=NavMeshPathStatus.PathComplete) continue;
                float length=0; for(int j=1;j<path.corners.Length;j++) length+=Vector3.Distance(path.corners[j-1],path.corners[j]);
                if(length<best) { best=length; destination=hit.position; found=true; }
            }
            if(found && Move(Player,destination)) { approaching=actor; objective.text="Approaching "+actor.DisplayName+"…"; }
            else objective.text="No clear route to "+actor.DisplayName+". Try a different position on the floor.";
        }
        bool Move(NavMeshAgent agent,Vector3 point)
        {
            if (!agent || !agent.isOnNavMesh) { LastMoveError="Character is off the walkable floor."; return false; }
            if (!NavMesh.SamplePosition(point,out var hit,1.7f,NavMesh.AllAreas)) { LastMoveError="Choose a clear point on the floor."; return false; }
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position,path) || path.status != NavMeshPathStatus.PathComplete) { LastMoveError="That route is blocked."; return false; }
            agent.isStopped=false; agent.SetPath(path); LastMoveError=null; return true;
        }
        void Stop(NavMeshAgent a) { if (a && a.isOnNavMesh) { a.ResetPath(); a.isStopped=true; } }
        IEnumerator Arrival()
        {
            Busy=true;
            yield return Say("MAYA","Finally. Come on, let's get closer to the dancefloor.");
            Busy=false; Refresh();
        }
        public void OpenConversation(CharacterActor actor)
        {
            if (Busy || State.Resolved) return;
            CancelChat(); Stop(Player); Stop(agents[Maya]); modal=true;ShowPortrait(actor.Id);
            foreach(Transform child in choices) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            choices.gameObject.SetActive(true);
            GymUI.Label(choices,actor.DisplayName.ToUpperInvariant(),20,15,460,35,26,GymUI.Cyan);
            if(State.Loop>1) { if(actor==Ren) ShowMusicMenu(); else ShowTypedConversation(actor); return; }
            choices.pivot=new Vector2(0,1);choices.anchoredPosition=new Vector2(25,-155);
            choices.sizeDelta=new Vector2(505,420);
            GymUI.Label(choices,"Opening scene",20,57,460,45,18,GymUI.Muted);
            int row=0;
            Choice("Let's find a spot on the floor.",()=> { CloseConversation(); Walk(ExpandedClub.Opening); },ref row);
            Choice("End conversation",CloseConversation,ref row);
        }
        void Choice(string label,UnityEngine.Events.UnityAction action,ref int row)
        { GymUI.Button(GymUI.Box(choices,"Choice "+row,18,112+row*83,469,73),label,action); row++; }
        public void SetWaiting(bool wait)
        { if(State.Wait(wait)) { Stop(agents[Maya]); Refresh(); } }
        void CloseConversation() { if(State.InVip && chatActor==Theo){State.LeaveVip();Move(agents[Theo],starts[Theo.transform]);} StopStoryLine();ShowPortrait(null); CancelChat(); modal=false; choices.gameObject.SetActive(false); Refresh(); }
        IEnumerator Reply(string name,string text) { Busy=true; yield return Say(name,text); Busy=false; Refresh(); }
        IEnumerator Say(string name,string text,bool finishBeforeRewind=false)
        {
            ShowPortrait(name.ToLowerInvariant()=="you"?"player":name.ToLowerInvariant());
            SetActing(name,true);PlayStoryLine(text);
            ObserverScene("Authored story beat",name+": "+text);
            speaker.text=name; line.text=text; dialogue.gameObject.SetActive(true); advance=false;
            if(finishBeforeRewind)
            {
                // This story beat completes even if Continue was pressed during the line.
                if(storySpeaker && storySpeaker.isPlaying)
                    while(storySpeaker.isPlaying) yield return null;
                else if(!AutoAdvance) yield return new WaitForSecondsRealtime(2f);
                yield return new WaitForSecondsRealtime(.55f);
            }
            else if(AutoAdvance) yield return null;
            else { yield return null; while(!advance) yield return null; }
            StopStoryLine();dialogue.gameObject.SetActive(false);SetActing(name,false);ShowPortrait(null);
        }
        public void BeginEncounter()
        {
            if (Busy || !State.Recognize()) return;
            approaching=null;
            StartCoroutine(Encounter());
        }
        IEnumerator Go(NavMeshAgent agent,Vector3 target,bool urgent=false)
        {
            float normalSpeed=agent.speed,normalAcceleration=agent.acceleration;
            try
            {
                if(urgent){agent.speed=8f;agent.acceleration=32f;}
                if(!Move(agent,target)) throw new InvalidOperationException(LastMoveError);
                float until=Time.time+12;
                do { yield return null; } while(Time.time<until && (agent.pathPending || agent.remainingDistance>.3f));
                if(agent.pathPending || agent.remainingDistance>.5f) throw new InvalidOperationException("Story route did not arrive: "+agent.name);
                Stop(agent);
            }
            finally {agent.speed=normalSpeed;agent.acceleration=normalAcceleration;}
        }
        IEnumerator Encounter()
        {
            Busy=true; CloseConversation(); Stop(Player); Stop(agents[Maya]); Refresh();
            yield return Say("MAYA","Wait. That's Theo. He's famous… AND married… and that is not his wife.");
            if(State.CanPrevent)
            {
                yield return Say("MAYA","Theo? Can we talk quietly for a second? My phone's away.");
                yield return Go(agents[Theo],ExpandedClub.Point(.61f,.60f));
                yield return Go(agents[Luca],ExpandedClub.Point(.56f,.60f));
                yield return Say("LUCA","Let's take a little space. Nobody needs an audience.");
                yield return Say("THEO","...Fine. Just stop staring.");
                yield return Say("YOU","The moment passes. Luca is still standing. This time, the music keeps playing.");
                State.Resolve(); Busy=false; Refresh();
                yield break;
            }
            phone.SetActive(!State.PrivateApproach);
            yield return Say("MAYA",State.PrivateApproach?"Theo. We need to talk about what you're doing.":"No way. I'm recording this. Theo, seriously?");
            yield return Go(agents[Theo],ExpandedClub.Point(.60f,.64f));
            yield return Say("THEO",State.PrivateApproach?"Keep your voice down. This is none of your business.":"Put the phone away. Give it to me.");
            yield return Say("MAYA",State.PrivateApproach?"Don't grab me.":"Don't touch my phone.");
            yield return Go(agents[Luca],ExpandedClub.Point(.565f,.635f),true);
            yield return Say("LUCA","Let go. You're done here.");
            yield return Go(agents[Theo],ExpandedClub.Point(.58f,.635f));
            yield return Say("THEO","Get off me!");
            float t=0; var start=Luca.Visual.localRotation;
            while(t<.55f) { t+=Time.deltaTime; Luca.Visual.localRotation=Quaternion.Slerp(start,Quaternion.Euler(0,0,82),t/.55f); yield return null; }
            yield return Say("YOU","Luca hits the floor. He isn't getting up.");
            yield return Say("REN","Not on my dancefloor.",true);
            yield return Rewind();
            Busy=false; Refresh();
            yield return Reply("YOU","Again. The same entrance. Before Maya sees him. Let's go left, toward the bar.");
        }
        IEnumerator Rewind()
        {
            music.Interrupt();
            for(float t=0;t<.6f;t+=Time.deltaTime) { curtain.color=new Color(.18f,.85f,.9f,t/.6f); yield return null; }
            State.Rewind(); ResetActors(true);
            ObserverScene("Rewind · game rule", "Luca collapsed. Ren interrupted the night. Characters return to their starting positions.");
            for(float t=0;t<.6f;t+=Time.deltaTime) { curtain.color=new Color(.18f,.85f,.9f,1-t/.6f); yield return null; }
            curtain.color=Color.clear;
        }
        void ResetActors(bool preserveRewind=false)
        {
            approaching=null;
            if(music)music.RestartTracks(preserveRewind);
            Stop(Player); foreach(var a in agents.Values) Stop(a);
            Player.Warp(starts[Player.transform]);
            foreach(var a in agents) a.Value.Warp(starts[a.Key.transform]);
            foreach(var p in rotations) p.Key.rotation=p.Value;
            Luca.Visual.localRotation=Quaternion.identity; phone.SetActive(false); followAt=0;
        }
        public void Restart()
        {
            StopStoryLine();CancelChat(); conversations.Clear(); StopAllCoroutines(); Busy=false; modal=false; State=new LoopState(); ResetActors();
            choices.gameObject.SetActive(false); dialogue.gameObject.SetActive(false); curtain.color=Color.clear;
            Refresh(); StartCoroutine(Arrival());
        }
        void Refresh()
        {
            if(!status) return;
            status.text="LOOP "+State.Loop+"  ·  "+(State.Intimate?"INTIMATE":"AGGRESSIVE");
            memories.text=State.RemembersRecording?"Maya's recording. Theo reaching for her phone. Luca stepping between them.":"Nothing yet. This is your first time here.";
            objective.text=State.Resolved?"THE NIGHT CONTINUES · You changed the encounter. Luca is safe. Restart to try another approach.":
                State.Loop==1?"Click the floor to walk toward the right side of the dancefloor. Click a character to talk. Space / Continue advances dialogue.":
                "Try a different approach. Click Maya, Luca or Ren to prepare, then walk toward Theo with Maya.  Maya: "+(State.MayaWaiting?"waiting":"following")+".";
        }
        IEnumerator Smoke()
        {
            yield return null;
            Capture("01-opening");
            if(!music.RewindClipReady)throw new Exception("Rewind scratch asset missing");
            music.Interrupt();yield return null;
            if(!music.RewindPlaying)throw new Exception("Rewind scratch did not start");
            music.RestartTracks(true);yield return null;
            if(!music.MusicSuppressedForRewind)throw new Exception("Music overlaps rewind scratch");
            music.RestartTracks();yield return null;
            if(music.RewindPlaying)throw new Exception("Manual restart did not stop scratch");
            if(!music.ClipsReady)throw new Exception("Music assets missing");
            float renStarted=Time.realtimeSinceStartup;
            var renBeat=StartCoroutine(Say("REN","Not on my dancefloor.",true));
            yield return null;advance=true;yield return new WaitForSecondsRealtime(.1f);
            if(!storySpeaker.isPlaying || music.RewindPlaying)throw new Exception("Ren was cut off by Continue or scratch started early");
            float renDuration=storySpeaker.clip.length;
            yield return renBeat;
            if(Time.realtimeSinceStartup-renStarted<renDuration+.5f)throw new Exception("Ren landing pause missing");
            Debug.Log("BTD_REN_LANDING_OK: Continue cannot cut line; complete speech plus landing pause");
            Debug.Log("BTD_AUDIO_SMOKE_OK: scratch import, interrupt, reset carry and manual restart cleanup");
            var lucaAgent=agents[Luca];float walkingSpeed=lucaAgent.speed;
            var sprint=StartCoroutine(Go(lucaAgent,ExpandedClub.Point(.565f,.635f),true));
            yield return null;
            if(lucaAgent.speed<=walkingSpeed*2)throw new Exception("Luca intervention is not a run");
            yield return sprint;
            if(Mathf.Abs(lucaAgent.speed-walkingSpeed)>.01f)throw new Exception("Luca walk speed not restored");
            lucaAgent.Warp(starts[Luca.transform]);
            Debug.Log("BTD_LUCA_RUN_OK: urgent route arrived and normal speed restored");
            foreach(var actor in new[]{Maya,Theo,Luca,Ren}){
                var route=new NavMeshPath();
                if(!NavMesh.SamplePosition(actor.transform.position,out var destination,1.7f,NavMesh.AllAreas)||!Player.CalculatePath(destination.position,route)||route.status!=NavMeshPathStatus.PathComplete)
                    throw new Exception("Expanded club route unavailable: "+actor.Id);
            }
            if(!Walk(ExpandedClub.Opening)) throw new Exception("Opening path failed");
            float timeout=Time.realtimeSinceStartup+45;
            while((State.Loop<2 || Busy) && Time.realtimeSinceStartup<timeout) yield return null;
            if(State.Loop!=2 || Busy || !State.RemembersRecording) throw new Exception("Opening did not rewind");
            Capture("02-rewound");
            SetObserverVisible(true);
            yield return VoiceSmoke();
            SetWaiting(true); var old=Maya.transform.position;
            Walk(ExpandedClub.LeftPreparation); yield return new WaitForSeconds(2);
            if(Vector3.Distance(old,Maya.transform.position)>.2f) throw new Exception("Waiting Maya moved");
            State.SetMusic(true);
            if(State.Resolved || State.CanPrevent) throw new Exception("Music/avoidance incorrectly wins");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-btd-dialogue-smoke")>=0)
            {
                SetWaiting(false); State.SetMusic(false);
                yield return DialogueSmoke();
            }
            else { State.PrepareMaya(); State.PrepareLuca(); SetWaiting(false); }
            if(!Walk(ExpandedClub.Opening)) throw new Exception("Second path failed");
            timeout=Time.realtimeSinceStartup+45;
            while(!State.Resolved && Time.realtimeSinceStartup<timeout) yield return null;
            if(!State.Resolved) throw new Exception("Prepared encounter did not resolve");
            Capture("03-safe-ending");
            yield return null; yield return null; yield return null;
            Debug.Log("BTD_SMOKE_OK: navigation, recognition, catastrophe, rewind, waiting, music-only rejection, prevention");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
        void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var folder=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../.local/captures"));
            System.IO.Directory.CreateDirectory(folder);
            var canvas=hud.GetComponentInParent<Canvas>();
            var target=new RenderTexture(1600,900,24);
            var previous=Rig.Camera.targetTexture; var active=RenderTexture.active;var previousRect=Rig.Camera.rect;Rig.Camera.rect=new Rect(0,0,1,1);
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=Rig.Camera; canvas.planeDistance=1;
            Rig.Camera.targetTexture=target; Canvas.ForceUpdateCanvases(); Rig.Camera.Render(); RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),texture.EncodeToPNG());
            Rig.Camera.targetTexture=previous;Rig.Camera.rect=previousRect; RenderTexture.active=active; canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            Destroy(texture); target.Release(); Destroy(target);
        }
    }
}
