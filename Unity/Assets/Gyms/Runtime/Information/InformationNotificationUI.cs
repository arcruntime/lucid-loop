using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class InformationNotificationUI : MonoBehaviour
    {
        public LearnedInformationManager Manager;
        public TMP_FontAsset HeadingFont, BodyFont;
        public Sprite UnreadIconSprite, ReadIconSprite;
        public Color Gold=new Color(.94f,.77f,.31f), Ivory=new Color(.93f,.88f,.84f);
        public Color PanelColor=new Color(.025f,.013f,.024f,.94f);
        public Color BorderColor=new Color(.56f,.43f,.23f,.6f);
        [Min(12)] public float HeadingSize=31, BodySize=27;
        [Min(0)] public float EdgeMargin=28, RowSpacing=22;
        [Min(.05f)] public float AnimationDuration=.28f;
        [Min(1)] public float PulseDuration=3.5f;
        [Range(0,1)] public float GlowStrength=.16f;
        public float BannerWidth=640, PanelWidth=610, BannerTop=156;
        public bool BannerOpen { get; private set; }
        public LearnedInformationPanelUI Panel { get; private set; }
        public Button IconButton { get; private set; }
        public Button BannerButton { get; private set; }
        public string BannerText => bannerBody?bannerBody.text:"";
        public bool IndicatorUnread => Manager && Manager.HasUnread;
        public bool IndicatorVisible => Manager && Manager.HasRetained;
        RectTransform root,safe,icon,banner;
        CanvasGroup iconGroup,bannerGroup;
        Graphic gem;
        TextMeshProUGUI bannerBody, bannerTitle;
        RectTransform bannerRule;
        float iconAlpha,bannerAlpha;
        TMP_FontAsset generatedFont;
        bool initialized;
        int suppressUntilFrame=-1;
        public static bool SuppressFloorTap { get; private set; }
        public float Scale => root?root.GetComponent<Canvas>().scaleFactor:1;
        public TMP_FontAsset Serif => HeadingFont?HeadingFont:generatedFont;
        public TMP_FontAsset Sans => BodyFont?BodyFont:Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        public void Initialize(LearnedInformationManager manager)
        {
            if(initialized && Manager==manager)return;
            if(Manager)Manager.Changed-=Refresh;
            Manager=manager;
            if(!root)Build();
            initialized=true;
            if(Manager)Manager.Changed+=Refresh;
            Refresh();
        }
        void Start(){if(!initialized){if(!Manager)Manager=GetComponent<LearnedInformationManager>();if(Manager)Initialize(Manager);}}
        void Build()
        {
            if(!HeadingFont)
            {
                var source=Resources.Load<Font>("Information/CormorantGaramond");
                generatedFont=source?TMP_FontAsset.CreateFontAsset(source):null;
            }
            var go=new GameObject("Learned information canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root=(RectTransform)go.transform;root.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            // Existing scenes retain their EventSystem. Standalone use needs one UI input module.
            if(!FindFirstObjectByType<EventSystem>())new GameObject("Information input events",typeof(EventSystem),typeof(StandaloneInputModule));
            safe=InformationUIStyle.Rect("Safe area",root,Vector2.zero);InformationUIStyle.Stretch(safe,Vector2.zero,Vector2.zero);
            safe.gameObject.AddComponent<SafeAreaPanel>();
            icon=InformationUIStyle.Rect("Unread information indicator",safe,new Vector2(80,80));icon.anchorMin=icon.anchorMax=icon.pivot=new Vector2(1,1);
            InformationUIStyle.Image(icon,Color.clear,true);iconGroup=icon.gameObject.AddComponent<CanvasGroup>();iconGroup.alpha=0;
            var artwork=InformationUIStyle.Rect("Gem",icon,Vector2.zero);InformationUIStyle.Stretch(artwork,Vector2.zero,Vector2.zero);
            if(UnreadIconSprite){var image=InformationUIStyle.Image(artwork,Gold);image.sprite=UnreadIconSprite;image.preserveAspect=true;gem=image;}
            else gem=artwork.gameObject.AddComponent<InformationGemGraphic>();
            gem.raycastTarget=false;IconButton=InformationUIStyle.Button(icon,ClickIcon);
            banner=InformationUIStyle.Rect("New information banner",safe,new Vector2(BannerWidth,148));banner.anchorMin=banner.anchorMax=banner.pivot=new Vector2(.5f,1);
            InformationUIStyle.Image(banner,PanelColor,true);InformationUIStyle.Border(banner,BorderColor);
            bannerGroup=banner.gameObject.AddComponent<CanvasGroup>();bannerGroup.alpha=0;BannerButton=InformationUIStyle.Button(banner,ClickBanner);
            var badge=InformationUIStyle.Rect("Banner diamond",banner,new Vector2(56,56));badge.anchoredPosition=new Vector2(14,-12);
            var badgeGraphic=badge.gameObject.AddComponent<InformationGemGraphic>();badgeGraphic.color=Gold;badgeGraphic.raycastTarget=false;
            var title=InformationUIStyle.Text("Notification title",banner,"NEW INFORMATION",Serif?Serif:Sans,HeadingSize,Gold);
            bannerTitle=title;
            title.rectTransform.anchoredPosition=new Vector2(76,-20);title.rectTransform.sizeDelta=new Vector2(BannerWidth-100,44);title.characterSpacing=4;
            var line=InformationUIStyle.Rect("Banner rule",banner,new Vector2(BannerWidth-102,1));line.anchoredPosition=new Vector2(76,-68);InformationUIStyle.Image(line,BorderColor);bannerRule=line;
            bannerBody=InformationUIStyle.Text("Newest information",banner,"",Sans,BodySize,Ivory);
            bannerBody.rectTransform.anchoredPosition=new Vector2(76,-82);bannerBody.rectTransform.sizeDelta=new Vector2(BannerWidth-104,55);
            bannerBody.overflowMode=TextOverflowModes.Ellipsis;
            Panel=safe.gameObject.AddComponent<LearnedInformationPanelUI>();Panel.Build(this,Manager,safe);
            Refresh();
        }
        public void ClickIcon()
        {
            ConsumeClick();
            if(!Manager || !Manager.HasRetained)return;
            if(!Manager.HasUnread){Panel.Open();BannerOpen=false;return;}
            Panel.Close();BannerOpen=!BannerOpen;Refresh();
        }
        public void ClickBanner()
        {
            ConsumeClick();if(!BannerOpen)return;
            BannerOpen=false;Panel.Open();Refresh();
        }
        public void CloseAll(){BannerOpen=false;if(Panel)Panel.Close();Refresh();}
        public void ConsumeClick(){suppressUntilFrame=Time.frameCount+1;SuppressFloorTap=true;}
        void Refresh()
        {
            if(!root)return;
            if(!Manager || !Manager.HasRetained){BannerOpen=false;if(Panel)Panel.Close();}
            if(BannerOpen && Manager.MostRecentUnread==null)BannerOpen=false;
            if(bannerBody)bannerBody.text=Manager?.MostRecentUnread?.Content??"";
            if(gem is InformationGemGraphic procedural){procedural.Unread=IndicatorUnread;procedural.color=IndicatorUnread?Gold:Ivory;procedural.SetVerticesDirty();}
            else if(gem is Image image){image.sprite=IndicatorUnread?UnreadIconSprite:(ReadIconSprite?ReadIconSprite:UnreadIconSprite);image.color=IndicatorUnread?Gold:new Color(Ivory.r,Ivory.g,Ivory.b,.45f);}
            if(Panel && Panel.IsOpen)Panel.RefreshEntries();
            SetInteraction();
        }
        void SetInteraction()
        {
            iconGroup.blocksRaycasts=iconGroup.interactable=IndicatorVisible;
            bannerGroup.blocksRaycasts=bannerGroup.interactable=BannerOpen;
            IconButton.interactable=IndicatorVisible;BannerButton.interactable=BannerOpen;
        }
        void Update()
        {
            if(!root)return;
            SuppressFloorTap=Time.frameCount<=suppressUntilFrame;
            float step=Time.unscaledDeltaTime/Mathf.Max(.05f,AnimationDuration);
            iconAlpha=Mathf.MoveTowards(iconAlpha,IndicatorVisible?1:0,step);bannerAlpha=Mathf.MoveTowards(bannerAlpha,BannerOpen?1:0,step);
            iconGroup.alpha=iconAlpha*(IndicatorUnread?1-GlowStrength*.5f+GlowStrength*.5f*Mathf.Sin(Time.unscaledTime*2*Mathf.PI/Mathf.Max(1,PulseDuration)):.65f);
            icon.localScale=Vector3.one*Mathf.Lerp(.86f,1,Mathf.SmoothStep(0,1,iconAlpha));bannerGroup.alpha=Mathf.SmoothStep(0,1,bannerAlpha);
            float target=Application.isMobilePlatform?Mathf.Max(80,132/Mathf.Max(.01f,Scale)):80;
            icon.sizeDelta=Vector2.one*target;icon.anchoredPosition=new Vector2(-EdgeMargin,-EdgeMargin);
            float width=Mathf.Max(240,Mathf.Min(BannerWidth,safe.rect.width-EdgeMargin*3-target));
            banner.sizeDelta=new Vector2(width,Mathf.Max(148,target+44));banner.anchoredPosition=new Vector2(-target*.2f,-Mathf.Min(BannerTop,safe.rect.height*.2f)-Mathf.Lerp(-16,0,bannerAlpha));
            bannerBody.rectTransform.sizeDelta=new Vector2(width-104,55);bannerTitle.rectTransform.sizeDelta=new Vector2(width-104,44);
            bannerRule.sizeDelta=new Vector2(width-102,1);
        }
        void OnDisable(){CloseAll();if(root)root.gameObject.SetActive(false);SuppressFloorTap=false;}
        void OnEnable(){if(root)root.gameObject.SetActive(true);}
        void OnDestroy()
        {
            if(Manager)Manager.Changed-=Refresh;if(root)Destroy(root.gameObject);SuppressFloorTap=false;
            if(generatedFont){foreach(var t in generatedFont.atlasTextures)if(t)Destroy(t);if(generatedFont.material)Destroy(generatedFont.material);Destroy(generatedFont);}
        }
    }
}
