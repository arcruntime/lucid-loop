using OsuFramework.Unity.Allocation;
using osu.Framework.Allocation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    public partial class OfflineGym : DependencyBehaviour
    {
        [Resolved] protected GymSession Session { get; private set; }
        public NavMeshAgent Player;
        public GymCamera Rig;
        public CharacterActor[] Characters;
        public Transform Marker;
        readonly HoldGesture gesture=new HoldGesture();
        CharacterActor pressedActor;
        Vector3 ground;
        bool hasGround;
        int finger=-1;
        Text notice,progress,title,dialogue;
        RectTransform conversationPanel;
        RawImage reference;
        float markerUntil;
        public GymSession State => Session;
        void Start()
        {
            var canvas=GymUI.Canvas("Nightclub HUD");
            var header=GymUI.Box(canvas,"Identity",30,26,525,113);GymUI.Panel(header,GymUI.Ink);
            GymUI.Label(header,"LUCID LOOP",24,14,460,45,34);
            GymUI.Label(header,"01 / CHARACTER GYM   ·   OFFLINE",24,64,460,28,18,GymUI.Cyan);
            var switchButton=GymUI.Rect(canvas,"Live gym",Vector2.one,Vector2.one,new Vector2(-265,-83),new Vector2(-30,-27));
            GymUI.Button(switchButton,"Live voice gym  →",()=>SceneManager.LoadScene("LiveGym"));
            var foot=GymUI.Rect(canvas,"Instructions",Vector2.zero,new Vector2(1,0),new Vector2(30,25),new Vector2(-30,98));GymUI.Panel(foot,GymUI.Ink);
            notice=GymUI.Text(GymUI.Rect(foot,"Status",Vector2.zero,Vector2.one,new Vector2(24,12),new Vector2(-24,-12)),Session.Notice,24,Color.white,TextAnchor.MiddleLeft);
            progress=GymUI.Text(GymUI.Rect(canvas,"Hold progress",new Vector2(.35f,.15f),new Vector2(.65f,.2f),Vector2.zero,Vector2.zero),"",24,GymUI.Cyan,TextAnchor.MiddleCenter);
            conversationPanel=GymUI.Rect(canvas,"Conversation",new Vector2(1,0),Vector2.one,new Vector2(-550,120),new Vector2(-30,-120));GymUI.Panel(conversationPanel,GymUI.Ink);
            GymUI.Label(conversationPanel,"OFFLINE CONVERSATION STUDY",26,25,465,30,18,GymUI.Cyan);
            title=GymUI.Label(conversationPanel,"",26,72,460,70,36);
            var rr=GymUI.Box(conversationPanel,"Reference sheet",26,157,468,310);reference=rr.gameObject.AddComponent<RawImage>();reference.raycastTarget=false;
            dialogue=GymUI.Label(conversationPanel,"",26,489,468,154,25);
            var close=GymUI.Rect(conversationPanel,"Leave",Vector2.zero,new Vector2(1,0),new Vector2(26,26),new Vector2(-26,82));
            GymUI.Button(close,"Return to the floor",EndConversation);
            conversationPanel.gameObject.SetActive(false);
        }
        void Update()
        {
            if(Session==null)return;
            if(Input.GetKeyDown(KeyCode.Escape))EndConversation();
            if(!Session.IsTalking) ReadPointer();
            if(Session.Approaching && !Player.pathPending)
            {
                if(Player.pathStatus!=NavMeshPathStatus.PathComplete) {Session.Approaching=null;Session.Notice="That character cannot be reached from here.";}
                else if(Player.remainingDistance <= Player.stoppingDistance+.12f) BeginConversation(Session.Approaching);
            }
            if(Marker)Marker.gameObject.SetActive(Time.time<markerUntil && !Session.IsTalking);
            if(notice)notice.text=Session.Notice;
            if(progress)progress.text=gesture.Progress(Time.unscaledTime)>0 ? "HOLD TO TALK  " + Mathf.RoundToInt(gesture.Progress(Time.unscaledTime)*100)+"%" : "";
            if(Player && Player.velocity.sqrMagnitude>.05f)
            {
                var actor=Player.GetComponent<CharacterActor>();
                if(actor.Visual) actor.Visual.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.time*10)*2);
            }
        }
        void ReadPointer()
        {
            if(Input.touchCount>0)
            {
                // Once a finger owns a gesture, additional fingers cannot replace it.
                for(int i=0;i<Input.touchCount;i++)
                {
                    var t=Input.GetTouch(i);
                    if(t.phase==TouchPhase.Began && finger<0){finger=t.fingerId;Down(t.position,finger);}
                    if(t.fingerId!=finger)continue;
                    if(t.phase==TouchPhase.Canceled){gesture.Cancel();finger=-1;}
                    else if(t.phase==TouchPhase.Ended){Up();finger=-1;}
                    else Move(t.position);
                }
            }
            else if(finger>=0){gesture.Cancel();finger=-1;}
            else
            {
                if(Input.GetMouseButtonDown(0))Down(Input.mousePosition,-1);
                if(Input.GetMouseButton(0))Move(Input.mousePosition);
                if(Input.GetMouseButtonUp(0))Up();
            }
            if(gesture.Tick(Time.unscaledTime)==GestureResult.Hold && pressedActor)Approach(pressedActor);
        }
        void Down(Vector2 pos,int id)
        {
            gesture.Cancel();hasGround=false;pressedActor=null;
            if(EventSystem.current && EventSystem.current.IsPointerOverGameObject(id))return;
            if(!Physics.Raycast(Rig.Camera.ScreenPointToRay(pos),out var hit,150))return;
            pressedActor=hit.collider.GetComponentInParent<CharacterActor>();
            if(pressedActor && pressedActor.IsPlayer)pressedActor=null;
            if(!pressedActor && NavMesh.SamplePosition(hit.point,out var nav,.7f,NavMesh.AllAreas)) {ground=nav.position;hasGround=true;}
            gesture.Begin(pos.x,pos.y,Time.unscaledTime,pressedActor!=null);
        }
        void Move(Vector2 pos) => gesture.Move(pos.x,pos.y,Mathf.Max(14,Screen.height*.018f));
        void Up()
        {
            if(gesture.Release()==GestureResult.Tap && hasGround)Walk(ground);
        }
        public bool Walk(Vector3 destination)
        {
            Session.Approaching=null;
            if(!Player.isOnNavMesh)return false;
            var path=new NavMeshPath();
            if(!Player.CalculatePath(destination,path)||path.status!=NavMeshPathStatus.PathComplete){Session.Notice="Choose a reachable point on the floor.";return false;}
            Player.isStopped=false;Player.SetPath(path);
            if(Marker)Marker.position=destination+Vector3.up*.04f;
            markerUntil=Time.time+1.5f;Session.Notice="Walking · Hold Maya, Ren, Luca or Theo to talk.";return true;
        }
        public bool Approach(CharacterActor actor)
        {
            Session.Approaching=null;
            if(!Player.isOnNavMesh)return false;
            float best=float.MaxValue;NavMeshPath chosen=null;
            for(int i=0;i<12;i++)
            {
                var offset=Quaternion.Euler(0,i*30,0)*actor.transform.forward*1.35f;
                if(!NavMesh.SamplePosition(actor.transform.position+offset,out var hit,.65f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();if(!Player.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float length=0;for(int k=1;k<path.corners.Length;k++)length+=Vector3.Distance(path.corners[k-1],path.corners[k]);
                if(length<best){best=length;chosen=path;}
            }
            if(chosen==null){Session.Notice="No clear path to "+actor.DisplayName+".";return false;}
            Player.isStopped=false;Player.SetPath(chosen);Session.Approaching=actor;Session.Notice="Approaching "+actor.DisplayName+"…";return true;
        }
        public void BeginConversation(CharacterActor actor)
        {
            Session.Approaching=null;Session.Conversation=actor;gesture.Cancel();
            if(Player.isOnNavMesh){Player.isStopped=true;Player.ResetPath();}
            Vector3 dir=actor.transform.position-Player.transform.position;dir.y=0;
            if(dir.sqrMagnitude>.01f){Player.transform.rotation=Quaternion.LookRotation(dir);actor.transform.rotation=Quaternion.LookRotation(-dir);}
            Rig.Present(actor);conversationPanel.gameObject.SetActive(true);title.text=actor.DisplayName+" / "+actor.Role;
            reference.texture=actor.Reference;dialogue.text=actor.Greeting+"\n\nSample dialogue · no API connection";
            Session.Notice="Conversation framing · Escape or Return to leave.";
        }
        public void EndConversation()
        {
            if(Session==null)return;Session.Approaching=null;Session.Conversation=null;gesture.Cancel();
            if(Player && Player.isOnNavMesh){Player.isStopped=false;Player.ResetPath();}
            Rig.Overview();if(conversationPanel)conversationPanel.gameObject.SetActive(false);
            Session.Notice="Tap the floor to walk. Hold a character to talk.";
        }
        void OnApplicationFocus(bool focus){if(!focus){gesture.Cancel();finger=-1;}}
    }
}
