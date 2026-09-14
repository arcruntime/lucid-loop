using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms.Mvp
{
    public partial class FirstLoop
    {
        RectTransform observer;
        Text observerContext, observerRequest, observerResult, observerToggle;
        bool observerVisible;
        string observerStage = "Authored opening";
        void MakeObserver()
        {
            observer = GymUI.Rect(hud,"AI action observer",Vector2.one,Vector2.one,new Vector2(-560,-620),new Vector2(-25,-185));
            GymUI.Panel(observer,GymUI.Ink);
            GymUI.Label(observer,"AI ACTION OBSERVER · DEMO VIEW",18,14,499,32,23,GymUI.Cyan);
            observerContext=GymUI.Label(observer,"",18,58,499,85,22);
            observerRequest=GymUI.Label(observer,"",18,150,499,125,22);
            observerResult=GymUI.Label(observer,"",18,285,499,133,22,GymUI.Cyan);
            var toggle=GymUI.Button(GymUI.Box(hud,"Observer toggle",835,20,290,50),"Show AI observer",()=>SetObserverVisible(!observerVisible));
            observerToggle=toggle.GetComponentInChildren<Text>();
            SetObserverVisible(false);
            ObserverScene("Authored opening", "Explore the club. The first encounter introduces the rewind.");
        }
        void SetObserverVisible(bool visible)
        {
            observerVisible=visible;
            if(observer)observer.gameObject.SetActive(visible);
            if(observerToggle)observerToggle.text=visible?"Hide AI observer":"Show AI observer";
            if(visible && memoryPanel)memoryPanel.gameObject.SetActive(false);
            PositionObserver();
        }
        void PositionObserver()
        {
            if(!observer)return;
            // During authored world events, leave Theo and the VIP area visible.
            bool worldBeat=dialogue && dialogue.gameObject.activeSelf;
            observer.anchorMin=observer.anchorMax=worldBeat?new Vector2(0,1):Vector2.one;
            observer.offsetMin=worldBeat?new Vector2(25,-590):new Vector2(-560,-620);
            observer.offsetMax=worldBeat?new Vector2(560,-155):new Vector2(-25,-185);
        }
        static string ObserverExcerpt(string text,int limit=210)
        {
            if(string.IsNullOrWhiteSpace(text))return "Waiting for a player request.";
            text=text.Replace("\r"," ").Replace("\n"," ");
            return text.Length>limit?text.Substring(0,limit-1)+"…":text;
        }
        void ObserverPending(CharacterActor actor,string request)
        {
            if(!observer)return;
            observerStage="AI conversation";
            observerContext.text=actor.DisplayName+" · "+(State.Intimate?"Intimate":"Aggressive")+" music\nCurrent conversation + character knowledge";
            observerRequest.text="PLAYER REQUEST\n"+ObserverExcerpt(request);
            observerResult.text="Interpreting request…\nNo new action applied yet.";
        }
        void ObserverDecision(CharacterActor actor,string request,string[] actions,bool applied)
        {
            ObserverPending(actor,request);
            observerResult.text=(applied?"MODEL-SELECTED ACTIONS · APPLIED\n":"MODEL RESULT · NOT APPLIED\n")+
                (applied?DescribeActions(actions):"Rejected or stale response. Nothing changed.");
        }
        void ObserverFailed(string message)
        {
            if(observer&&observerStage=="AI conversation")observerResult.text=message+"\nNo new action applied.";
        }
        void ObserverScene(string stage,string description)
        {
            if(!observer)return;
            observerStage=stage;
            observerContext.text="LOOP "+State.Loop+" · "+(State.Intimate?"Intimate":"Aggressive")+" music\n"+stage;
            observerRequest.text=description;
            observerResult.text="Game rules / authored scene\nNo model decision controls this event.";
        }
    }
}
