using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    // Shared Canvas/TMP factories. Copy is always plain text, never interpreted as rich text.
    public static class InformationUIStyle
    {
        public static RectTransform Rect(string name,Transform parent,Vector2 size)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.sizeDelta=size;return r;
        }
        public static void Stretch(RectTransform r,Vector2 min,Vector2 max)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=min;r.offsetMax=max;}
        public static Image Image(RectTransform r,Color color,bool raycast=false)
        {var g=r.gameObject.AddComponent<Image>();g.color=color;g.raycastTarget=raycast;return g;}
        public static TextMeshProUGUI Text(string name,Transform parent,string value,TMP_FontAsset font,float size,Color color)
        {
            var r=Rect(name,parent,Vector2.zero);r.gameObject.SetActive(false);
            var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=value;t.color=color;
            t.richText=false;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;
            r.gameObject.SetActive(true);return t;
        }
        public static Button Button(RectTransform r,UnityEngine.Events.UnityAction action)
        {
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Graphic>();
            var colors=b.colors;colors.highlightedColor=new Color(1,.96f,.84f);colors.pressedColor=new Color(.78f,.70f,.55f);b.colors=colors;
            b.onClick.AddListener(action);return b;
        }
        public static void Border(RectTransform r,Color color)
        {
            var top=Rect("Top hairline",r,Vector2.zero);Stretch(top,new Vector2(0,-1),Vector2.zero);top.anchorMin=new Vector2(0,1);Image(top,color);
            var bottom=Rect("Bottom hairline",r,Vector2.zero);Stretch(bottom,Vector2.zero,new Vector2(0,1));bottom.anchorMax=new Vector2(1,0);Image(bottom,color);
            var left=Rect("Left hairline",r,Vector2.zero);Stretch(left,Vector2.zero,new Vector2(1,0));left.anchorMax=new Vector2(0,1);Image(left,color);
            var right=Rect("Right hairline",r,Vector2.zero);Stretch(right,new Vector2(-1,0),Vector2.zero);right.anchorMin=new Vector2(1,0);Image(right,color);
        }
    }
}
