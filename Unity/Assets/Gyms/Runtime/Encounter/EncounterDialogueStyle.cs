using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    // Runs after the existing safe-area/keyboard layout. Existing controls and
    // voice callbacks retain ownership of conversation state and input.
    [DefaultExecutionOrder(100)]
    public sealed class EncounterDialogueStyle : MonoBehaviour
    {
        EncounterHud hud;
        EncounterHudLayout layout;
        RectTransform panel, frame, plaque;
        DecoDialogueFrame microphoneIcon, sendIcon;
        Text title, subtitle;
        float lastKeyboardInset, keyboardHoldUntil;
        static readonly Color Gold = new Color(.93f,.77f,.39f);
        static readonly Color Black = new Color(.018f,.022f,.02f,.97f);
        public void Initialize(EncounterHud owner, EncounterHudLayout responsive)
        {
            hud=owner;layout=responsive;panel=layout.Conversation;
            frame=Decoration(panel,"Dialogue gold frame");
            plaque=Decoration(panel,"Character nameplate");plaque.GetComponent<DecoDialogueFrame>().Nameplate=true;
            title=GymUI.Label(plaque,"",20,9,260,35,30,new Color(.065f,.05f,.02f));title.alignment=TextAnchor.MiddleCenter;
            title.verticalOverflow=VerticalWrapMode.Overflow;
            subtitle=GymUI.Label(plaque,"",20,43,260,25,18,new Color(.065f,.05f,.02f));subtitle.alignment=TextAnchor.MiddleCenter;
            subtitle.verticalOverflow=VerticalWrapMode.Overflow;
            Decoration(Child("Reply area"),"Input gold trim").GetComponent<DecoDialogueFrame>().Simple=true;
            foreach(var button in panel.GetComponentsInChildren<Button>(true))
            {
                if(button.targetGraphic)button.targetGraphic.color=new Color(.07f,.065f,.045f,.98f);
                var text=button.GetComponentInChildren<Text>();if(text)text.color=Gold;
                if(button.name!="Microphone" && button.name!="Send")
                    Decoration(button.transform,"Button gold trim").GetComponent<DecoDialogueFrame>().Simple=true;
            }
            microphoneIcon=Decoration(Child("Microphone"),"Microphone icon").GetComponent<DecoDialogueFrame>();
            microphoneIcon.MicrophoneIcon=true;
            sendIcon=Decoration(Child("Send"),"Send icon").GetComponent<DecoDialogueFrame>();
            sendIcon.SendIcon=true;
        }
        static RectTransform Decoration(Transform parent,string name)
        {
            var r=GymUI.Rect(parent,name,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            r.gameObject.AddComponent<CanvasRenderer>();
            var g=r.gameObject.AddComponent<DecoDialogueFrame>();g.raycastTarget=false;return r;
        }
        RectTransform Child(string name)=>panel.Find(name) as RectTransform;
        static void Place(RectTransform r,float x,float y,float w,float h)
        {
            r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=Vector2.zero;
            r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(Mathf.Max(0,w),Mathf.Max(0,h));
        }
        void LateUpdate()
        {
            if(!panel || !hud || !layout.SafeRoot)return;
            bool expanded=layout.ConversationExpanded;
            plaque.gameObject.SetActive(expanded);
            panel.GetComponent<Image>().color=Black;
            var oldTitle=panel.GetChild(0);oldTitle.gameObject.SetActive(!expanded);
            if(!expanded)
            {
                frame.anchorMin=Vector2.zero;frame.anchorMax=Vector2.one;frame.offsetMin=frame.offsetMax=Vector2.zero;
                float idleScale=layout.SafeRoot.GetComponentInParent<Canvas>().scaleFactor;
                layout.ApplyPhoneLayout(layout.SafeRoot.rect.size,Mathf.Max(64,EncounterHudLayout.TouchTargetUnits(idleScale)),0);
                return;
            }
            var size=layout.SafeRoot.rect.size;
            float scale=layout.SafeRoot.GetComponentInParent<Canvas>().scaleFactor;
            bool phone=Application.isMobilePlatform||layout.PreviewPhoneLayout;
            float target=phone?Mathf.Max(72,EncounterHudLayout.TouchTargetUnits(scale)):72;
            float keyboard=EncounterHudLayout.KeyboardInset(TouchScreenKeyboard.area,Screen.safeArea,scale,TouchScreenKeyboard.visible);
            if(keyboard>0){lastKeyboardInset=keyboard;keyboardHoldUntil=Time.unscaledTime+.35f;}
            else if(Time.unscaledTime<keyboardHoldUntil)keyboard=lastKeyboardInset;
            float w=Mathf.Min(1440,size.x*.78f), h=Mathf.Min(size.y-keyboard-150,Mathf.Max(360,size.y*.39f));
            h=Mathf.Max(200,Mathf.Min(h,size.y-keyboard-64));
            Place(panel,(size.x-w)*.5f,48+keyboard,w,h);
            layout.Guidance.gameObject.SetActive(false);
            Place(layout.Pause,size.x-460,size.y-110,190,64);
            Place(layout.Reset,size.x-250,size.y-110,190,64);
            float body=w;
            Place(frame,0,0,body,h);
            Place(plaque,60,h-34,300,78);
            title.fontSize=30;subtitle.fontSize=18;
            title.text=hud.SelectedNpcId.ToUpperInvariant();
            foreach(var actor in hud.Coordinator.Characters)if(actor&&actor.Id==hud.SelectedNpcId)subtitle.text=actor.Role;
            float inputY=36;
            Place(Child("Reply area"),40,inputY,body-80,target);
            Child("Reply area").GetComponent<Image>().color=new Color(.035f,.04f,.035f,1);
            Place(Child("Microphone"),body-40-2*target-10,inputY,target,target);
            Place(Child("Send"),body-40-target,inputY,target,target);
            foreach(var name in new[]{"Microphone","Send"})
            {
                var button=Child(name).GetComponent<Button>();
                button.GetComponent<Image>().color=Color.clear;
                button.GetComponentInChildren<Text>().enabled=false;
            }
            var reply=Child("Reply area").GetComponent<InputField>();
            foreach(var text in new[]{reply.textComponent,reply.placeholder as Text})
                if(text)text.rectTransform.offsetMax=new Vector2(-2*target-30,-8);
            microphoneIcon.SetIconState(hud.Voice && hud.Voice.MicrophoneEnabled,
                Child("Microphone").GetComponent<Button>().interactable);
            sendIcon.SetIconState(false,Child("Send").GetComponent<Button>().interactable);
            var history=Child("History scroll");Place(history,40,inputY+target+18,body-80,h-inputY-target-98);
            history.GetComponent<Image>().color=Color.clear;history.gameObject.SetActive(true);
            var transcript=history.GetComponentInChildren<Text>();transcript.fontSize=phone?32:30;
            var status=panel.GetChild(7) as RectTransform;Place(status,380,h-52,body-610,40);status.gameObject.SetActive(true);status.GetComponent<Text>().fontSize=18;status.GetComponent<Text>().verticalOverflow=VerticalWrapMode.Overflow;
            var controls=new[]{"Select maya","Select ren","Select luca","Select theo","Talk","History"};
            float cell=Mathf.Min(155,(w-48)/6);
            for(int i=0;i<controls.Length;i++)
            {var r=Child(controls[i]);r.gameObject.SetActive(keyboard<=0);Place(r,i*(cell+8),h+54,cell,target);r.GetComponentInChildren<Text>().fontSize=24;r.GetComponentInChildren<Text>().verticalOverflow=VerticalWrapMode.Overflow;}
            Child("Leave").gameObject.SetActive(true);Place(Child("Leave"),body-190,h-78,150,64);Child("Leave").GetComponentInChildren<Text>().fontSize=22;Child("Leave").GetComponentInChildren<Text>().verticalOverflow=VerticalWrapMode.Overflow;
            foreach(var name in new[]{"Reply area","Send","Microphone"})Child(name).gameObject.SetActive(true);
        }
    }
}
