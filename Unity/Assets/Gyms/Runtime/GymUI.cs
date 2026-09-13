using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LucidLoop.Gyms
{
    public static class GymUI
    {
        public static readonly Color Ink = new Color(.035f,.04f,.075f,.96f);
        public static readonly Color Muted = new Color(.64f,.68f,.78f);
        public static readonly Color Cyan = new Color(.22f,.92f,.93f);
        static Font hudFont;
        static bool missingFontReported;
        public static Font Font
        {
            get
            {
                if (hudFont) return hudFont;
                hudFont = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
                if (hudFont) return hudFont;
                if (!missingFontReported)
                {
                    missingFontReported = true;
                    Debug.LogWarning("Bundled HUD font Fonts/NotoSansJP-Regular is missing. Falling back to LegacyRuntime; Japanese glyph coverage is not guaranteed.");
                }
                hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return hudFont;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFontCache() { hudFont = null; missingFontReported = false; }
        public static RectTransform Canvas(string name)
        {
            var go=new GameObject(name,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var c=go.GetComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay;
            var s=go.GetComponent<CanvasScaler>(); s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution=new Vector2(1920,1080); s.matchWidthOrHeight=.5f;
            if (!Object.FindFirstObjectByType<EventSystem>()) new GameObject("Input events",typeof(EventSystem),typeof(StandaloneInputModule));
            var safe=new GameObject("Safe area",typeof(RectTransform),typeof(SafeAreaPanel));
            safe.transform.SetParent(go.transform,false);
            return safe.GetComponent<RectTransform>();
        }
        public static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);
            var r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;return r;
        }
        public static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {
            return Rect(parent,name,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y-h),new Vector2(x+w,-y));
        }
        public static Image Panel(RectTransform r,Color color) {var i=r.gameObject.AddComponent<Image>();i.color=color;return i;}
        public static Text Text(RectTransform r,string value,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            var t=r.gameObject.AddComponent<Text>();t.font=Font;t.text=value;t.fontSize=size;t.color=color;
            t.alignment=align;t.supportRichText=false;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        public static Text Label(Transform parent,string value,float x,float y,float w,float h,int size=24,Color? color=null)
            => Text(Box(parent,value,x,y,w,h),value,size,color??Color.white);
        public static Button Button(RectTransform r,string label,UnityEngine.Events.UnityAction action,Color? color=null)
        {
            var img=Panel(r,color??new Color(.14f,.18f,.25f));var b=r.gameObject.AddComponent<Button>();b.targetGraphic=img;
            var colors=b.colors;colors.highlightedColor=new Color(.75f,1,1);colors.pressedColor=new Color(.4f,.7f,.8f);b.colors=colors;
            Text(Rect(r,"Label",Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4)),label,24,Color.white,TextAnchor.MiddleCenter);
            b.onClick.AddListener(action);return b;
        }
        public static InputField Input(Transform parent,string label,float y,string value,bool secret=false)
        {
            Label(parent,label,26,y,460,28,19,Muted);
            var r=Box(parent,label+" field",26,y+35,468,52);Panel(r,new Color(.08f,.1f,.16f));
            var t=Text(Rect(r,"Value",Vector2.zero,Vector2.one,new Vector2(12,6),new Vector2(-12,-6)),value,20,Color.white,TextAnchor.MiddleLeft);
            var f=r.gameObject.AddComponent<InputField>();f.textComponent=t;f.text=value;f.characterLimit=512;
            if(secret)f.contentType=InputField.ContentType.Password;
            return f;
        }
    }
    public class SafeAreaPanel : MonoBehaviour
    {
        void Update()
        {
            var r=(RectTransform)transform;var a=Screen.safeArea;
            r.anchorMin=new Vector2(a.x/Screen.width,a.y/Screen.height);
            r.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);r.offsetMin=r.offsetMax=Vector2.zero;
        }
    }
}
