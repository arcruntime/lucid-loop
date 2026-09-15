using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    public enum DialogueBoxState { Closed, AwaitingInput, WaitingForResponse, PlayingSpeech }
    [DisallowMultipleComponent]
    public sealed class DialogueBoxController : MonoBehaviour
    {
        public TMP_FontAsset NameFont, BodyFont;
        public Color Gold=new Color(.76f,.62f,.34f), Ivory=new Color(.94f,.90f,.83f), Pink=new Color(1,.12f,.49f), Background=new Color(.018f,.014f,.018f,.985f);
        public float Width=1380, Height=360, BottomMargin=48, Padding=60, FadeDuration=.22f;
        public float Sensitivity=8, Attack=.035f, Release=.18f, Glow=.35f;
        public Sprite BorderArtwork;
        [Header("Latest player transcript")]
        public float PlayerTranscriptHeight=76, PlayerTranscriptFontSize=23;
        public string PlayerTranscriptLabel="You said: ";
        public string PlayerTranscript {get;private set;}="";
        [Header("Offline speech test — assigned clip, no AI request")]
        public AudioClip TestSpeechClip;
        [TextArea] public string TestText="You’re asking about the blackout? Funny.\nYou’re not the first.";
        public DialogueBoxState State {get;private set;}
        static readonly System.Collections.Generic.HashSet<DialogueBoxController> openBoxes=new System.Collections.Generic.HashSet<DialogueBoxController>();
        public static bool InputBlocked=>openBoxes.Count>0;
        public bool IsOpen => State!=DialogueBoxState.Closed;
        public readonly NpcSpeechMeter Meter=new NpcSpeechMeter();
        public event Action<string> Submitted;
        public event Action<bool> MicrophoneChanged;
        public event Action Closed;
        public TMP_InputField Reply {get;private set;}
        public NpcVoiceWaveform Waveform {get;private set;}
        public bool MicrophoneAvailable {get;private set;}
        public int Epoch {get;private set;}
        RectTransform panel,safe,body,replyRow,playerView;
        TextMeshProUGUI playerText;
        ScrollRect playerScroll;
        TextMeshProUGUI nameText,roleText,response,status,hint;
        CanvasGroup group;
        Button mic,send,contextButton;
        Action contextAction;
        DialogueOrnament micGraphic;
        AudioSource testSource;
        readonly float[] output=new float[1024];
        TMP_FontAsset generated;
        bool micOn,waiting,hold,testPlayback,canSubmit=true;
        float visibility;
        string currentText="";
        void Build()
        {
            if(panel)return;
            if(!BodyFont)BodyFont=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if(!NameFont){var font=Resources.Load<Font>("Dialogue/CormorantGaramond");if(font)NameFont=generated=TMP_FontAsset.CreateFontAsset(font);else NameFont=BodyFont;}
            Canvas canvas=GetComponentInParent<Canvas>();
            Transform parent=transform;
            if(!canvas)
            {
                var go=new GameObject("Dialogue UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
                canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
                var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;parent=go.transform;
            }
            else if(!canvas.GetComponent<GraphicRaycaster>())canvas.gameObject.AddComponent<GraphicRaycaster>();
            if(!EventSystem.current)new GameObject("Dialogue events",typeof(EventSystem),typeof(StandaloneInputModule));
            safe=Rect("Dialogue safe area",parent,Vector2.zero);Stretch(safe,Vector2.zero,Vector2.zero);safe.gameObject.AddComponent<SafeAreaPanel>();
            panel=Rect("Black and gold dialogue",safe,new Vector2(Width,Height));panel.anchorMin=panel.anchorMax=new Vector2(.5f,0);panel.pivot=new Vector2(.5f,0);
            group=panel.gameObject.AddComponent<CanvasGroup>();Image(panel,Background,true);
            var border=Rect("Art Deco frame",panel,Vector2.zero);Stretch(border,Vector2.zero,Vector2.zero);
            if(BorderArtwork){var image=Image(border,Gold);image.sprite=BorderArtwork;image.type=UnityEngine.UI.Image.Type.Sliced;}
            else Decor(border);
            var plate=Rect("Champagne nameplate",panel,new Vector2(330,90));plate.anchoredPosition=new Vector2(Padding,-2);plate.pivot=new Vector2(0,.5f);Decor(plate).Nameplate=true;
            nameText=Text("NPC name",plate,"THEO",NameFont,42,Color.black);Stretch(nameText.rectTransform,new Vector2(28,30),new Vector2(-28,-5));nameText.alignment=TextAlignmentOptions.Center;nameText.fontStyle=FontStyles.Bold;
            roleText=Text("NPC role",plate,"The Socialite",BodyFont,22,Color.black);Stretch(roleText.rectTransform,new Vector2(20,10),new Vector2(-20,-53));roleText.alignment=TextAlignmentOptions.Center;roleText.enableAutoSizing=true;roleText.fontSizeMin=12;roleText.fontSizeMax=22;roleText.textWrappingMode=TextWrappingModes.NoWrap;
            var close=Rect("Close dialogue",panel,new Vector2(65,55));close.anchorMin=close.anchorMax=new Vector2(1,1);close.pivot=Vector2.one;close.anchoredPosition=new Vector2(-25,-15);Button(close,"×",Close);
            var context=Rect("Context action",panel,new Vector2(330,42));context.anchorMin=context.anchorMax=new Vector2(1,1);context.pivot=Vector2.one;context.anchoredPosition=new Vector2(-100,-18);contextButton=Button(context,"",()=>contextAction?.Invoke());var contextText=Text("Action label",context,"",BodyFont,20,Ivory);Stretch(contextText.rectTransform,Vector2.zero,Vector2.zero);contextText.alignment=TextAlignmentOptions.Right;context.gameObject.SetActive(false);
            playerView=Rect("Player transcript viewport",panel,Vector2.zero);
            Image(playerView,Color.clear,true);playerView.gameObject.AddComponent<RectMask2D>();
            playerText=Text("Latest player transcript",playerView,"",BodyFont,PlayerTranscriptFontSize,Ivory);
            playerText.rectTransform.anchorMin=new Vector2(0,1);playerText.rectTransform.anchorMax=Vector2.one;playerText.rectTransform.pivot=new Vector2(0,1);
            playerScroll=playerView.gameObject.AddComponent<ScrollRect>();playerScroll.viewport=playerView;playerScroll.content=playerText.rectTransform;playerScroll.horizontal=false;playerScroll.movementType=ScrollRect.MovementType.Clamped;
            var view=Rect("Response viewport",panel,Vector2.zero);Stretch(view,new Vector2(Padding,148),new Vector2(-Padding,-65));Image(view,Color.clear,true);view.gameObject.AddComponent<RectMask2D>();
            body=Rect("Current NPC response",view,Vector2.zero);body.anchorMax=new Vector2(1,1);body.sizeDelta=Vector2.zero;
            response=Text("NPC response",body,"",BodyFont,32,Ivory);Stretch(response.rectTransform,Vector2.zero,Vector2.zero);
            var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.content=body;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            replyRow=Rect("Reply row",panel,Vector2.zero);replyRow.anchorMin=Vector2.zero;replyRow.anchorMax=new Vector2(1,0);replyRow.pivot=Vector2.zero;replyRow.offsetMin=new Vector2(Padding,50);replyRow.offsetMax=new Vector2(-Padding,132);Image(replyRow,new Color(.009f,.007f,.012f,.8f),true);Decor(replyRow).Simple=true;
            var textView=Rect("Typed text viewport",replyRow,Vector2.zero);Stretch(textView,new Vector2(20,10),new Vector2(-340,-10));textView.gameObject.AddComponent<RectMask2D>();
            var inputText=Text("Reply text",textView,"",BodyFont,27,Ivory);Stretch(inputText.rectTransform,Vector2.zero,Vector2.zero);
            var placeholder=Text("Reply placeholder",textView,"Type your reply...",BodyFont,27,new Color(.7f,.67f,.65f));Stretch(placeholder.rectTransform,Vector2.zero,Vector2.zero);
            Reply=replyRow.gameObject.AddComponent<TMP_InputField>();Reply.textViewport=textView;Reply.textComponent=inputText;Reply.placeholder=placeholder;Reply.characterLimit=4000;Reply.lineType=TMP_InputField.LineType.SingleLine;Reply.onSubmit.AddListener(_=>Submit());
            var wave=Rect("NPC speech waveform",replyRow,new Vector2(175,65));wave.anchorMin=wave.anchorMax=new Vector2(1,.5f);wave.pivot=new Vector2(1,.5f);wave.anchoredPosition=new Vector2(-150,0);Waveform=wave.gameObject.AddComponent<NpcVoiceWaveform>();Waveform.Meter=Meter;Waveform.color=Pink;
            var micRect=Rect("Player microphone",replyRow,new Vector2(74,74));micRect.anchorMin=micRect.anchorMax=new Vector2(1,.5f);micRect.pivot=new Vector2(1,.5f);micRect.anchoredPosition=new Vector2(-65,0);mic=Button(micRect,"",()=>SetMicrophone(!micOn));micGraphic=Decor(micRect);micGraphic.MicrophoneIcon=true;
            var sendRect=Rect("Submit reply",replyRow,new Vector2(56,74));sendRect.anchorMin=sendRect.anchorMax=new Vector2(1,.5f);sendRect.pivot=new Vector2(1,.5f);sendRect.anchoredPosition=new Vector2(-6,0);send=Button(sendRect,"›",Submit);
            status=Text("Dialogue status",panel,"",BodyFont,18,Ivory);status.rectTransform.anchoredPosition=new Vector2(Padding,-Height+146);status.rectTransform.sizeDelta=new Vector2(Width-2*Padding,28);
            hint=Text("Desktop input hint",panel,"Enter to send",BodyFont,18,new Color(.65f,.62f,.60f));hint.rectTransform.anchorMin=Vector2.zero;hint.rectTransform.anchorMax=new Vector2(1,0);hint.rectTransform.pivot=Vector2.zero;hint.rectTransform.offsetMin=new Vector2(Padding,20);hint.rectTransform.offsetMax=new Vector2(-Padding,48);hint.alignment=TextAlignmentOptions.Right;
            panel.gameObject.SetActive(false);
        }
        public void Open(string name,string role,bool microphoneAvailable)
        {Build();if(testSource)testSource.Stop();testPlayback=false;openBoxes.Add(this);Epoch++;Meter.Reset();Waveform.ResetWave();nameText.text=name.ToUpperInvariant();roleText.text=role;PlayerTranscript="";playerText.text="";playerView.gameObject.SetActive(false);currentText="";response.text="";Reply.text="";SetContextAction("",null);waiting=false;State=DialogueBoxState.AwaitingInput;visibility=0;MicrophoneAvailable=microphoneAvailable;mic.gameObject.SetActive(microphoneAvailable);panel.gameObject.SetActive(true);group.blocksRaycasts=group.interactable=true;SetInputAvailable(true);}
        public void SetContextAction(string label,Action action){contextAction=action;contextButton.gameObject.SetActive(action!=null);contextButton.GetComponentInChildren<TextMeshProUGUI>().text=label;}
        public void SetInputAvailable(bool value){canSubmit=value;if(Reply)Reply.interactable=value;if(send)send.interactable=value;}
        public void SetResponse(string text,int epoch)
        {if(!IsOpen || epoch!=Epoch)return;currentText=text??"";response.text=currentText;waiting=false;status.text="";}
        // This is recognized text from the existing voice service, never microphone amplitude.
        // Keep it independent of NPC text/status so it remains readable during the answer.
        public void SetPlayerTranscript(string text,int epoch)
        {
            if(!IsOpen || epoch!=Epoch)return;
            PlayerTranscript=text??"";
            if(PlayerTranscript.Length>4000)PlayerTranscript=PlayerTranscript.Substring(PlayerTranscript.Length-4000);
            playerText.text=PlayerTranscript.Length>0?PlayerTranscriptLabel+PlayerTranscript:"";
            playerView.gameObject.SetActive(PlayerTranscript.Length>0);
        }
        public void SetStatus(string text,bool awaitingResponse=false){if(IsOpen){status.text=text;waiting=awaitingResponse;}}
        public void Submit()
        {if(!IsOpen || !canSubmit || string.IsNullOrWhiteSpace(Reply.text))return;string text=Reply.text;SetPlayerTranscript(text,Epoch);Reply.text="";waiting=true;status.text="Thinking...";Submitted?.Invoke(text);}
        public void SetMicrophone(bool active)
        {if(!MicrophoneAvailable || !IsOpen)return;micOn=active;MicrophoneChanged?.Invoke(active);}
        public void SetMicrophoneState(bool active){micOn=active;}
        public void Close()
        {if(!IsOpen)return;State=DialogueBoxState.Closed;openBoxes.Remove(this);Epoch++;waiting=hold=micOn=false;if(testSource)testSource.Stop();Meter.Reset();Waveform.ResetWave();Reply.DeactivateInputField();group.blocksRaycasts=group.interactable=false;Closed?.Invoke();}
        public void InterruptSpeech(){if(testSource)testSource.Stop();Meter.Reset();if(Waveform)Waveform.ResetWave();waiting=false;}
        void Update()
        {
            if(!panel)return;
            visibility=Mathf.MoveTowards(visibility,IsOpen?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,FadeDuration));group.alpha=Mathf.SmoothStep(0,1,visibility);
            panel.gameObject.SetActive(IsOpen || visibility>0);
            float w=Mathf.Min(Width,safe.rect.width-48);float transcriptHeight=PlayerTranscript.Length>0?Mathf.Max(40,PlayerTranscriptHeight):0;
            float totalHeight=Height+transcriptHeight;panel.sizeDelta=new Vector2(w,totalHeight);
            Stretch(playerView,new Vector2(Padding,Height-55),new Vector2(-Padding,-65));
            playerText.fontSize=PlayerTranscriptFontSize;
            playerText.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(1,playerText.GetPreferredValues(playerText.text,Mathf.Max(100,w-2*Padding),0).y));
            // Grow upwards only: the reply row and NPC response retain their existing space.
            body.parent.GetComponent<RectTransform>().offsetMax=new Vector2(-Padding,-65-transcriptHeight);float keyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height/Mathf.Max(.01f,panel.GetComponentInParent<Canvas>().scaleFactor):0;panel.anchoredPosition=new Vector2(0,BottomMargin+keyboard-(1-visibility)*12);
            body.sizeDelta=new Vector2(0,Mathf.Max(70,response.GetPreferredValues(currentText,Mathf.Max(100,w-2*Padding),0).y+8));
            status.rectTransform.anchoredPosition=new Vector2(Padding,-totalHeight+146);status.rectTransform.sizeDelta=new Vector2(w-2*Padding,28);
            Waveform.Sensitivity=Sensitivity;Waveform.Attack=Attack;Waveform.Release=Release;Waveform.Glow=Glow;
            hint.gameObject.SetActive(!Application.isMobilePlatform);hint.text=MicrophoneAvailable?"Enter to send · Hold V to speak":"Enter to send";
            if(!IsOpen)return;
            if(testPlayback && testSource){testSource.GetOutputData(output,0);Meter.Output(output,testSource.isPlaying);}
            State=Meter.Level>.0005f?DialogueBoxState.PlayingSpeech:waiting?DialogueBoxState.WaitingForResponse:DialogueBoxState.AwaitingInput;
            micGraphic.SetIconState(micOn,true);
            // While editing a reply, V remains a letter. Outside the field it is push-to-talk.
            if(!Application.isMobilePlatform && MicrophoneAvailable && !Reply.isFocused && Input.GetKeyDown(KeyCode.V)){hold=true;SetMicrophone(true);}
            if(hold && Input.GetKeyUp(KeyCode.V)){hold=false;SetMicrophone(false);}
            if(Input.GetKeyDown(KeyCode.Escape))Close();
        }
        [ContextMenu("Play assigned speech clip (offline test)")]
        public void PlayTest()
        {
            if(!Application.isPlaying)return;Open("Theo","The Socialite",false);SetResponse(TestText,Epoch);SetStatus("OFFLINE TEST · Assigned NPC speech clip");
            if(!TestSpeechClip){SetStatus("Offline test: assign a speech clip in the Inspector.");return;}
            if(!testSource){var go=new GameObject("Dialogue test speech");go.transform.SetParent(transform,false);testSource=go.AddComponent<AudioSource>();testSource.playOnAwake=false;testSource.spatialBlend=0;}
            testPlayback=true;testSource.clip=TestSpeechClip;testSource.Play();
        }
        void OnDisable(){Close();if(panel)panel.gameObject.SetActive(false);}
        void OnDestroy(){if(safe)Destroy(safe.gameObject);if(generated){foreach(var atlas in generated.atlasTextures)if(atlas)Destroy(atlas);if(generated.material)Destroy(generated.material);Destroy(generated);}}
        DialogueOrnament Decor(RectTransform r){var child=Rect("Decoration",r,Vector2.zero);Stretch(child,Vector2.zero,Vector2.zero);var g=child.gameObject.AddComponent<DialogueOrnament>();g.Gold=Gold;g.raycastTarget=false;return g;}
        static RectTransform Rect(string name,Transform parent,Vector2 size){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.sizeDelta=size;return r;}
        static void Stretch(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=min;r.offsetMax=max;}
        static Image Image(RectTransform r,Color color,bool raycast=false){var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;}
        TextMeshProUGUI Text(string name,Transform parent,string value,TMP_FontAsset font,float size,Color color){var r=Rect(name,parent,Vector2.zero);r.gameObject.SetActive(false);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.richText=false;t.raycastTarget=false;r.gameObject.SetActive(true);return t;}
        Button Button(RectTransform r,string label,UnityEngine.Events.UnityAction action){var image=Image(r,Color.clear,true);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(action);if(label!=""){var t=Text("Button label",r,label,BodyFont,32,Ivory);Stretch(t.rectTransform,Vector2.zero,Vector2.zero);t.alignment=TextAlignmentOptions.Center;}return b;}
    }
}
