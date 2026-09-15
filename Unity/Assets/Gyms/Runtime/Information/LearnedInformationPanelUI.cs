using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    public sealed class LearnedInformationPanelUI : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public Button CloseButton { get; private set; }
        InformationNotificationUI owner;
        LearnedInformationManager manager;
        RectTransform safe,panel,content,viewport;
        CanvasGroup group;
        ScrollRect scroll;
        readonly HashSet<string> highlighted=new HashSet<string>();
        readonly List<GameObject> rows=new List<GameObject>();
        bool updating;
        float progress,lastWidth=-1;
        public void Build(InformationNotificationUI owner,LearnedInformationManager manager,RectTransform safe)
        {
            this.owner=owner;this.manager=manager;this.safe=safe;
            panel=InformationUIStyle.Rect("Learned information panel",safe,new Vector2(owner.PanelWidth,720));
            InformationUIStyle.Image(panel,owner.PanelColor,true);InformationUIStyle.Border(panel,owner.BorderColor);
            group=panel.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=group.interactable=false;
            var title=InformationUIStyle.Text("Panel title",panel,"LEARNED INFORMATION",owner.Serif?owner.Serif:owner.Sans,owner.HeadingSize,owner.Ivory);
            title.characterSpacing=3;title.rectTransform.anchoredPosition=new Vector2(30,-30);title.rectTransform.sizeDelta=new Vector2(owner.PanelWidth-114,54);
            title.enableAutoSizing=true;title.fontSizeMin=18;title.fontSizeMax=owner.HeadingSize;title.textWrappingMode=TextWrappingModes.NoWrap;
            var close=InformationUIStyle.Rect("Close learned information",panel,new Vector2(80,80));close.anchorMin=close.anchorMax=close.pivot=new Vector2(1,1);
            InformationUIStyle.Image(close,Color.clear,true);CloseButton=InformationUIStyle.Button(close,()=>{owner.ConsumeClick();Close();});
            var x=InformationUIStyle.Text("Close glyph",close,"×",owner.Sans,38,owner.Ivory);InformationUIStyle.Stretch(x.rectTransform,Vector2.zero,Vector2.zero);x.alignment=TextAlignmentOptions.Center;
            var rule=InformationUIStyle.Rect("Panel rule",panel,Vector2.zero);rule.anchoredPosition=new Vector2(30,-96);rule.sizeDelta=new Vector2(owner.PanelWidth-60,1);InformationUIStyle.Image(rule,owner.BorderColor);
            viewport=InformationUIStyle.Rect("Information scroll",panel,Vector2.zero);InformationUIStyle.Stretch(viewport,new Vector2(24,24),new Vector2(-24,-116));
            InformationUIStyle.Image(viewport,Color.clear,true);viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            content=InformationUIStyle.Rect("Entries",viewport,Vector2.zero);content.anchorMax=new Vector2(1,1);content.sizeDelta=Vector2.zero;scroll.content=content;scroll.viewport=viewport;
        }
        public void Open()
        {
            if(!manager || !manager.HasRetained)return;
            IsOpen=true;group.interactable=group.blocksRaycasts=true;highlighted.Clear();RefreshEntries();scroll.verticalNormalizedPosition=1;
        }
        public void Close(){IsOpen=false;group.interactable=group.blocksRaycasts=false;highlighted.Clear();}
        public void RefreshEntries()
        {
            if(updating || !IsOpen)return;
            updating=true;
            try
            {
                var entries=manager.Entries.Where(e=>e.IsRetained).OrderByDescending(e=>e.Order).ToArray();
                foreach(var entry in entries)if(entry.IsUnread)highlighted.Add(entry.Id);
                foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();
                float width=Mathf.Max(180,panel.rect.width-48),y=0;
                foreach(var entry in entries)
                {
                    bool fresh=highlighted.Contains(entry.Id);
                    var row=InformationUIStyle.Rect("Entry "+entry.Id,content,new Vector2(width,80));row.anchoredPosition=new Vector2(0,-y);rows.Add(row.gameObject);
                    var diamond=InformationUIStyle.Rect("Entry diamond",row,new Vector2(40,40));diamond.anchoredPosition=new Vector2(0,-1);
                    var gem=diamond.gameObject.AddComponent<InformationGemGraphic>();gem.color=fresh?owner.Gold:owner.Ivory;gem.raycastTarget=false;
                    var text=InformationUIStyle.Text("Information text",row,entry.Content,owner.Sans,owner.BodySize,fresh?owner.Gold:owner.Ivory);
                    float textWidth=Mathf.Max(90,width-62-(fresh?64:0));
                    float height=Mathf.Max(42,text.GetPreferredValues(entry.Content,textWidth,0).y+8);
                    text.rectTransform.anchoredPosition=new Vector2(52,0);text.rectTransform.sizeDelta=new Vector2(textWidth,height);
                    if(fresh)
                    {
                        var tag=InformationUIStyle.Text("Unread tag",row,"NEW",owner.Sans,19,owner.Gold);
                        tag.rectTransform.anchorMin=tag.rectTransform.anchorMax=tag.rectTransform.pivot=new Vector2(1,1);
                        tag.rectTransform.anchoredPosition=new Vector2(-2,-7);tag.rectTransform.sizeDelta=new Vector2(60,32);tag.characterSpacing=2;
                    }
                    row.sizeDelta=new Vector2(width,height);y+=height+owner.RowSpacing;
                }
                content.sizeDelta=new Vector2(0,Mathf.Max(0,y-owner.RowSpacing));
                manager.MarkRead(entries.Select(e=>e.Id)); // NEW styling stays for this viewing session.
            }
            finally {updating=false;}
        }
        void Update()
        {
            if(!panel)return;
            float width=Mathf.Min(owner.PanelWidth,safe.rect.width-owner.EdgeMargin*2);
            float top=Mathf.Min(150,safe.rect.height*.16f);
            panel.sizeDelta=new Vector2(width,Mathf.Max(180,Mathf.Min(740,safe.rect.height-top-owner.EdgeMargin)));
            progress=Mathf.MoveTowards(progress,IsOpen?1:0,Time.unscaledDeltaTime/Mathf.Max(.05f,owner.AnimationDuration));
            float ease=Mathf.SmoothStep(0,1,progress);group.alpha=ease;panel.anchoredPosition=new Vector2(owner.EdgeMargin-(1-ease)*28,-top);
            var title=(RectTransform)panel.Find("Panel title");title.sizeDelta=new Vector2(width-118,54);
            var rule=(RectTransform)panel.Find("Panel rule");rule.sizeDelta=new Vector2(width-60,1);
            float target=Application.isMobilePlatform?Mathf.Max(80,132/Mathf.Max(.01f,owner.Scale)):80;
            ((RectTransform)CloseButton.transform).sizeDelta=Vector2.one*target;
            if(Mathf.Abs(lastWidth-width)>.5f){lastWidth=width;RefreshEntries();}
        }
    }
}
