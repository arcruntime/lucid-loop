using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
namespace LucidLoop.Gyms.Mvp
{
    public partial class FirstLoop
    {
        RectTransform portrait;
        RawImage portraitImage;
        IEnumerator ArtReference()
        {
            Busy=true;
            foreach(var actor in FindObjectsByType<CharacterActor>(FindObjectsSortMode.None))foreach(var r in actor.GetComponentsInChildren<Renderer>())r.enabled=false;
            foreach(var r in Partner.GetComponentsInChildren<Renderer>())r.enabled=false;
            var club=FindFirstObjectByType<ClubLighting>();if(club)foreach(var d in club.Dancers)foreach(var r in d.GetComponentsInChildren<Renderer>())r.enabled=false;
            hud.GetComponentInParent<Canvas>().enabled=false;
            yield return new WaitForSeconds(.5f);Capture("09-art-guide");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
        void MakePortrait()
        {
            portrait=GymUI.Rect(hud,"Illustrated portrait",new Vector2(1,1),Vector2.one,new Vector2(-465,-920),new Vector2(-35,-175));
            portraitImage=portrait.gameObject.AddComponent<RawImage>();portraitImage.texture=Resources.Load<Texture2D>("MvpArt/Portraits");portraitImage.raycastTarget=false;
            portrait.gameObject.SetActive(false);
            // Keep the rewind overlay above art.
            curtain.transform.SetAsLastSibling();
        }
        void ShowPortrait(string id)
        {
            if(!portrait)return;
            int index=Array.IndexOf(new[]{"maya","luca","theo","ren","player"},id);
            portrait.gameObject.SetActive(index>=0);
            if(index>=0)portraitImage.uvRect=new Rect(index*.2f,.48f,.2f,.52f);
        }
        void SetActing(string name,bool speaking)
        {
            foreach(var actor in new[]{Maya,Theo,Luca,Ren})
            {var figure=actor.Visual.GetComponent<CastVisual>();if(figure)figure.Speaking=speaking&&actor.DisplayName.Equals(name,StringComparison.OrdinalIgnoreCase);}
        }
        void BeginVipEscort()
        {
            if(Busy || !State.VipInvited || State.InVip || State.Recognized)return;
            CloseConversation();StartCoroutine(EscortVip());
        }
        IEnumerator EscortVip()
        {
            var goal=new Vector3(10.3f,0,-.85f);var playerGoal=new Vector3(9.0f,0,-.85f);
            if(!Move(agents[Theo],goal) || !Move(Player,playerGoal))
            {Stop(agents[Theo]);Stop(Player);objective.text="The route is blocked. Try approaching VIP from the open side.";yield break;}
            Busy=true;objective.text="Following Theo into VIP…";
            float deadline=Time.time+15,nextFollow=0;
            while(Time.time<deadline)
            {
                if(!State.MayaWaiting && Time.time>nextFollow)
                {
                    nextFollow=Time.time+.25f;
                    Move(agents[Maya],Player.transform.position+Vector3.left*.8f);
                }
                // Escorting never suppresses Maya's recognition trigger.
                if(!State.MayaWaiting && CanRecognizeAffair())
                {Stop(Player);Stop(agents[Theo]);Stop(agents[Maya]);Busy=false;BeginEncounter();yield break;}
                if(!Player.pathPending&&!agents[Theo].pathPending&&Player.remainingDistance<.3f&&agents[Theo].remainingDistance<.3f)break;
                yield return null;
            }
            Stop(Player);Stop(agents[Theo]);Stop(agents[Maya]);Busy=false;
            if(Vector3.Distance(Player.transform.position,playerGoal)>1 || Vector3.Distance(Theo.transform.position,goal)>1)
            {objective.text="We couldn't reach the seating. Try again from the open entrance.";yield break;}
            State.ArriveVip();Refresh();
            var history=conversations[Theo.Id];history.Add(new ChatTurn("assistant","We can talk quietly here.",State.Loop));
            OpenConversation(Theo);
        }
    }
}
