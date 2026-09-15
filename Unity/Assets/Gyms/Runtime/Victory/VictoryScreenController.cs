using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    [DisallowMultipleComponent]
    public sealed class VictoryScreenController : MonoBehaviour
    {
        [Header("Editable copy")]
        [TextArea] public string Heading = "THE NIGHT HAS BEEN\nSUCCESSFULLY CHANGED.";
        public string Subtitle = "THE LOOP HAS BEEN BROKEN.";
        public string ContinuePrompt = "PRESS ANY BUTTON";
        [Header("Typography")]
        public TMP_FontAsset HeadingFont;
        public TMP_FontAsset SmallFont;
        [Range(36, 110)] public float HeadingSize = 76;
        [Range(-20, 30)] public float LineSpacing = -5;
        [Range(0, 30)] public float SmallTextTracking = 14;
        public Color Ivory = new Color(.96f, .92f, .86f);
        public Color Magenta = new Color(.72f, .10f, .37f);
        [Header("Composition (1920 × 1080 reference)")]
        public float ContentWidth = 1120;
        public float CircleDiameter = 600;
        public float UpperOrnamentY = 154;
        public float LowerOrnamentY = -130;
        public float SubtitleY = -190;
        [Range(0, 1)] public float DimOpacity = .48f;
        [Range(0, 1)] public float VignetteOpacity = .5f;
        [Range(0, 1)] public float GlowStrength = .22f;
        [Header("Unscaled reveal timing")]
        [Min(.1f)] public float RevealDuration = 1.8f;
        [Min(0)] public float ReadabilityDelay = .7f;
        [Min(1)] public float PromptPulseDuration = 4;
        public UnityEvent Shown = new UnityEvent();
        public UnityEvent Continued = new UnityEvent();
        public UnityEvent Closed = new UnityEvent();
        public bool IsOpen { get; private set; }
        public bool CanContinue { get; private set; }
        public static bool InputBlocked => owners.Count > 0;
        static readonly HashSet<VictoryScreenController> owners = new HashSet<VictoryScreenController>();
        RectTransform root, composition;
        CanvasGroup all;
        Image dim;
        VictoryDecoration vignette, arcs, upper, lower;
        TextMeshProUGUI title, subtitle, prompt;
        float openedAt;
        int openedFrame;
        bool released;
        readonly List<TMP_FontAsset> ownedFonts = new List<TMP_FontAsset>();
        public static bool VictoryPhase(string phase) => phase == "victory";

        [ContextMenu("Show victory screen")]
        public void ShowVictoryScreen()
        {
            if (IsOpen || !isActiveAndEnabled) return;
            Build(); IsOpen = true; CanContinue = false; released = false;
            completing = false; openedAt = Time.unscaledTime; openedFrame = Time.frameCount;
            owners.Add(this); root.gameObject.SetActive(true); Render(0);
            try { Shown.Invoke(); } catch { HideVictoryScreen(); throw; }
        }
        // Also usable by a project-specific input adapter. A release must precede a new press.
        public void SubmitInput(bool held, bool freshPress)
        {
            if (!IsOpen || completing || Time.frameCount <= openedFrame) return;
            if (!CanContinue) { released = false; return; }
            if (!held) released = true;
            if (!released || !freshPress) return;
            released = false; CanContinue = false; completing = true;
            // Keep the blocker until the triggering input has left the current frame.
            StartCoroutine(Complete());
        }
        IEnumerator Complete()
        {
            yield return null;
            if (!IsOpen) yield break;
            try { Continued.Invoke(); } finally { HideVictoryScreen(); }
        }
        public void HideVictoryScreen()
        {
            if (!IsOpen) return;
            IsOpen = false; CanContinue = false; owners.Remove(this);
            if (root) root.gameObject.SetActive(false);
            Closed.Invoke();
        }
        void Update()
        {
            if (!IsOpen) return;
            float elapsed = Time.unscaledTime - openedAt;
            CanContinue = elapsed >= Mathf.Max(.1f, RevealDuration) + ReadabilityDelay && !completing;
            Render(elapsed);
            bool touchHeld = Input.touchCount > 0, touchDown = false;
            for (int i=0; i<Input.touchCount; i++) touchDown |= Input.GetTouch(i).phase == TouchPhase.Began;
            SubmitInput(Input.anyKey || touchHeld, Input.anyKeyDown || touchDown);
        }
        bool completing;
        void Build()
        {
            if (root) return;
            var go = new GameObject("Victory overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root = (RectTransform)go.transform; root.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            all = go.GetComponent<CanvasGroup>();
            dim = Rect("Dim gameplay", root, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            Stretch(dim.rectTransform); dim.raycastTarget = true;
            vignette = Decor("Soft vignette", root, VictoryDecoration.Shape.Vignette, Vector2.zero, Vector2.zero); Stretch(vignette.rectTransform);
            composition = Rect("Victory composition", root, new Vector2(ContentWidth, 650), new Vector2(0, 0));
            arcs = Decor("Broken loop traces", composition, VictoryDecoration.Shape.Arcs, new Vector2(CircleDiameter, CircleDiameter), new Vector2(0,-25));
            upper = Decor("Upper glint and hairlines", composition, VictoryDecoration.Shape.Ornament, new Vector2(ContentWidth,60), new Vector2(0,UpperOrnamentY));
            lower = Decor("Lower glint and hairlines", composition, VictoryDecoration.Shape.Ornament, new Vector2(ContentWidth,60), new Vector2(0,LowerOrnamentY));
            var serif = HeadingFont ? HeadingFont : MakeFont(Resources.Load<Font>("Victory/CormorantGaramond"));
            var sans = SmallFont ? SmallFont : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            title = Text("Victory heading", composition, Heading, serif, HeadingSize, new Vector2(ContentWidth,230), new Vector2(0,13));
            title.lineSpacing = LineSpacing;
            subtitle = Text("Victory subtitle", composition, Subtitle, sans, 23, new Vector2(ContentWidth,60), new Vector2(0,SubtitleY));
            subtitle.characterSpacing = SmallTextTracking;
            prompt = Text("Continue prompt", root, ContinuePrompt, sans, 20, new Vector2(ContentWidth,60), Vector2.zero);
            prompt.characterSpacing = SmallTextTracking;
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(.5f,.15f);
        }
        TMP_FontAsset MakeFont(Font source)
        {
            if (!source) return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var font = TMP_FontAsset.CreateFontAsset(source); ownedFonts.Add(font); return font;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent,false); r.anchorMin = r.anchorMax = new Vector2(.5f,.5f); r.sizeDelta=size; r.anchoredPosition=position; return r;
        }
        static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
        static VictoryDecoration Decor(string name, Transform parent, VictoryDecoration.Shape shape, Vector2 size, Vector2 pos)
        { var graphic=Rect(name,parent,size,pos).gameObject.AddComponent<VictoryDecoration>(); graphic.Kind=shape; graphic.raycastTarget=false; return graphic; }
        TextMeshProUGUI Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Vector2 box, Vector2 pos)
        {
            // Assign before activation to avoid TMP's default-font import prompt.
            var r=Rect(name,parent,box,pos); r.gameObject.SetActive(false);
            var t=r.gameObject.AddComponent<TextMeshProUGUI>(); t.font=font; t.text=value; t.fontSize=size;
            t.alignment=TextAlignmentOptions.Center; t.textWrappingMode=TextWrappingModes.NoWrap;
            t.enableAutoSizing=true; t.fontSizeMin=12; t.fontSizeMax=size; t.raycastTarget=false;
            r.gameObject.SetActive(true); return t;
        }
        static float Fade(float t, float begin, float end) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(begin,end,t));
        void Render(float elapsed)
        {
            title.text=Heading; title.lineSpacing=LineSpacing; title.fontSizeMax=HeadingSize;
            subtitle.text=Subtitle; prompt.text=ContinuePrompt;
            subtitle.characterSpacing=prompt.characterSpacing=SmallTextTracking;
            if(HeadingFont)title.font=HeadingFont;
            if(SmallFont){subtitle.font=SmallFont;prompt.font=SmallFont;}
            arcs.rectTransform.sizeDelta=Vector2.one*CircleDiameter;
            upper.rectTransform.sizeDelta=lower.rectTransform.sizeDelta=new Vector2(ContentWidth,60);
            upper.rectTransform.anchoredPosition=new Vector2(0,UpperOrnamentY);
            lower.rectTransform.anchoredPosition=new Vector2(0,LowerOrnamentY);
            subtitle.rectTransform.anchoredPosition=new Vector2(0,SubtitleY);
            title.rectTransform.sizeDelta=new Vector2(ContentWidth,230);
            float t=elapsed/Mathf.Max(.1f,RevealDuration);
            dim.color=new Color(0,0,0,DimOpacity*Fade(t,0,.3f));
            vignette.color=new Color(0,0,0,VignetteOpacity*Fade(t,0,.4f));
            float width=root.rect.width, height=root.rect.height;
            float scale=Mathf.Min(1, width*.9f/Mathf.Max(1,ContentWidth),height*.72f/650);
            composition.localScale=Vector3.one*scale;
            prompt.rectTransform.localScale=Vector3.one*Mathf.Min(1,width*.85f/Mathf.Max(1,ContentWidth));
            arcs.color=Magenta; arcs.Reveal=Fade(t,.15f,.65f)*(1+.13f*Mathf.Sin(Mathf.Clamp01((t-.3f)/.7f)*Mathf.PI));
            arcs.Clock=elapsed; arcs.Glow=GlowStrength; arcs.SetVerticesDirty();
            foreach(var line in new[]{upper,lower}) { line.color=Ivory; line.Reveal=Fade(t,.18f,.63f); line.SetVerticesDirty(); }
            title.color=Tint(Ivory,Fade(t,.30f,.77f)); subtitle.color=Tint(Ivory,.8f*Fade(t,.52f,.92f));
            prompt.color=Tint(Ivory,CanContinue? (.46f+.17f*Mathf.Sin(elapsed*Mathf.PI*2/Mathf.Max(1,PromptPulseDuration)))*Fade(t,.85f,1):0);
        }
        static Color Tint(Color c,float alpha) { c.a*=alpha; return c; }
        void OnDisable() { StopAllCoroutines(); completing=false; HideVictoryScreen(); }
        void OnDestroy()
        {
            owners.Remove(this); if(root) Destroy(root.gameObject);
            foreach(var f in ownedFonts) if(f) { foreach(var atlas in f.atlasTextures) if(atlas) Destroy(atlas); if(f.material)Destroy(f.material); Destroy(f); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ClearOwners() => owners.Clear();
    }
}
