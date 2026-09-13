using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    /// <summary>Independent mouth, stitched eyelids and iris controls for local construction review.</summary>
    public sealed class RenFaceStudyController : MonoBehaviour
    {
        public const string HairObjectName = "RenFaceStudy Fitted Hair";
        public const string StaticTextureId = "ren-tripo-texture-static";
        public bool RightIsStatic => Viewer && IsStatic(Viewer.RightIndex);
        bool IsStatic(int index) => index >= 0 && index < Viewer.Candidates.Length && Viewer.Candidates[index].Id == StaticTextureId;
        public const float CloseSeconds = .065f, HoldSeconds = .040f, OpenSeconds = .140f;
        public RenBustComparisonViewer Viewer;
        [Range(0,1)] public float MouthSeal = 1, JawOpenA, BlinkLeft, BlinkRight;
        public Vector2 Gaze;
        public bool IdleBlink = true, ShowHair;
        public int BlinkSeed = 20260913;
        public float EffectiveBlinkLeft { get; private set; }
        public float EffectiveBlinkRight { get; private set; }
        static readonly string[] Names = { "mouthSeal", "jawOpen_A", "eyeBlinkL", "eyeBlinkR", "gazeLeft", "gazeRight", "gazeUp", "gazeDown" };
        struct Binding { public SkinnedMeshRenderer Renderer; public int[] Shapes; }
        GameObject leftRoot, rightRoot, leftHair, rightHair;
        Binding[] left = Array.Empty<Binding>(), right = Array.Empty<Binding>();
        System.Random blinkRandom;
        double nextBlink;
        bool wasIdle, draggingGaze;
        GUIStyle label, small, button;
        [Serializable] sealed class LiveCapture
        {
            public string file;
            public float mouthSeal, jawOpenA, blinkLeft, blinkRight, gazeX, gazeY;
            public bool hair, idle;
            public int renderedFrames;
        }
        [Serializable] sealed class LiveEvidence
        {
            public string unityVersion;
            public string method = "Visible player screenshots after rendered frames; live SkinnedMeshRenderers, no baked capture meshes.";
            public string blinkTiming = "Smooth close 65ms, hold 40ms, smooth open 140ms; seeded intervals 2-6 seconds after each blink.";
            public int blinkSeed;
            public bool idlePeakObserved, idleReopened;
            public LiveCapture[] captures;
        }
        public static bool ShapeMatches(string actual, string expected) => actual == expected || actual.EndsWith("." + expected, StringComparison.Ordinal);
        static Binding[] Bind(GameObject root)
        {
            if (!root) return Array.Empty<Binding>();
            var result = new List<Binding>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.sharedMesh) continue;
                var indices = new int[Names.Length]; for (var i=0; i<indices.Length; i++) indices[i] = -1;
                for (var i=0; i<renderer.sharedMesh.blendShapeCount; i++)
                {
                    renderer.SetBlendShapeWeight(i,0);
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    for (var key=0; key<Names.Length; key++) if (ShapeMatches(name, Names[key])) indices[key] = i;
                }
                renderer.updateWhenOffscreen = true;
                result.Add(new Binding { Renderer = renderer, Shapes = indices });
            }
            return result.ToArray();
        }
        static void Apply(Binding[] bindings, float seal, float openA, float blinkL, float blinkR, Vector2 gaze)
        {
            foreach (var binding in bindings)
            {
                if (!binding.Renderer) continue;
                for (var i=0; i<Names.Length; i++)
                {
                    if (binding.Shapes[i] < 0) continue;
                    var weight = i==0 ? seal : i==1 ? openA : i==2 ? blinkL : i==3 ? blinkR :
                        i==4 ? Mathf.Max(0,-gaze.x) : i==5 ? Mathf.Max(0,gaze.x) : i==6 ? Mathf.Max(0,gaze.y) : Mathf.Max(0,-gaze.y);
                    binding.Renderer.SetBlendShapeWeight(binding.Shapes[i], Mathf.Clamp01(weight)*100);
                }
            }
        }
        public static void ApplyFaceTo(GameObject root, float seal, float openA, float blinkL, float blinkR, Vector2 gaze) => Apply(Bind(root),seal,openA,blinkL,blinkR,gaze);
        public static void ApplyMouthTo(GameObject root, float seal, float openA) => ApplyFaceTo(root,seal,openA,0,0,Vector2.zero);
        public void SetMouth(float seal, float openA)
        {
            MouthSeal = Mathf.Clamp01(seal); JawOpenA = Mathf.Clamp01(openA);
            var sum = MouthSeal+JawOpenA; if (sum>1) { MouthSeal/=sum; JawOpenA/=sum; }
            ApplyNow();
        }
        public void SetEyes(float blinkL, float blinkR, Vector2 gaze)
        {
            BlinkLeft = Mathf.Clamp01(blinkL); BlinkRight = Mathf.Clamp01(blinkR);
            Gaze = new Vector2(Mathf.Clamp(gaze.x,-1,1),Mathf.Clamp(gaze.y,-1,1)); ApplyNow();
        }
        public void SetFace(float seal, float openA, float blinkL, float blinkR, Vector2 gaze) { SetMouth(seal,openA); SetEyes(blinkL,blinkR,gaze); }
        public void ClosedRest() => SetMouth(1,0);
        public void SourceRest() => SetMouth(0,0);
        public void OpenA() => SetMouth(0,1);
        public void ResetAll() { IdleBlink = false; SetFace(1,0,0,0,Vector2.zero); }
        public static float BlinkAt(float elapsed)
        {
            if (elapsed<0 || elapsed>=CloseSeconds+HoldSeconds+OpenSeconds) return 0;
            if (elapsed<CloseSeconds) return Mathf.SmoothStep(0,1,elapsed/CloseSeconds);
            if (elapsed<CloseSeconds+HoldSeconds) return 1;
            return Mathf.SmoothStep(1,0,(elapsed-CloseSeconds-HoldSeconds)/OpenSeconds);
        }
        void ResetIdleSchedule(bool immediate=false)
        {
            blinkRandom = new System.Random(BlinkSeed);
            nextBlink = Time.unscaledTimeAsDouble+(immediate ? 0 : 2+4*blinkRandom.NextDouble()); wasIdle = IdleBlink;
        }
        float IdleWeight()
        {
            if (!IdleBlink) { wasIdle = false; return 0; }
            if (!wasIdle || blinkRandom==null) ResetIdleSchedule();
            var elapsed = (float)(Time.unscaledTimeAsDouble-nextBlink);
            if (elapsed>=CloseSeconds+HoldSeconds+OpenSeconds) { nextBlink = Time.unscaledTimeAsDouble+2+4*blinkRandom.NextDouble(); return 0; }
            return BlinkAt(elapsed);
        }
        static GameObject FindHair(GameObject root)
        {
            if (root) foreach (var item in root.GetComponentsInChildren<Transform>(true)) if (item.name==HairObjectName) return item.gameObject;
            return null;
        }
        public void ApplyNow()
        {
            if (!Viewer) return;
            if (leftRoot!=Viewer.LeftInstance) { leftRoot=Viewer.LeftInstance; left=IsStatic(Viewer.LeftIndex) ? Array.Empty<Binding>() : Bind(leftRoot); leftHair=FindHair(leftRoot); }
            if (rightRoot!=Viewer.RightInstance) { rightRoot=Viewer.RightInstance; right=RightIsStatic ? Array.Empty<Binding>() : Bind(rightRoot); rightHair=FindHair(rightRoot); }
            var idle=IdleWeight(); EffectiveBlinkLeft=Mathf.Max(BlinkLeft,idle); EffectiveBlinkRight=Mathf.Max(BlinkRight,idle);
            Apply(left,1,0,0,0,Vector2.zero); Apply(right,MouthSeal,JawOpenA,EffectiveBlinkLeft,EffectiveBlinkRight,Gaze);
            if (leftHair) leftHair.SetActive(ShowHair); if (rightHair) rightHair.SetActive(ShowHair);
        }
        void LateUpdate() => ApplyNow();

        IEnumerator Start()
        {
            ResetIdleSchedule(); ApplyNow();
            var args=Environment.GetCommandLineArgs(); var index=Array.IndexOf(args,"-renFaceLiveCapture");
            if (index<0 || index+1>=args.Length) yield break;
            var output=Path.GetFullPath(args[index+1]); Directory.CreateDirectory(output);
            // Facial verification always selects the live source, even when a static paint trial is the scene default.
            Viewer.SelectCandidate(0,true); Viewer.SelectCandidate(0,false); ApplyNow();
            Application.runInBackground=true; Screen.SetResolution(1600,900,false);
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            var records=new List<LiveCapture>(); IdleBlink=false;
            foreach (var pose in new[] { "closed-rest","open-A","blink-left","blink-right","blink-full","gaze-left-up","gaze-right-down","combined-half","combined-full","hair-closed","hair-combined","reset-closed-rest" })
            {
                ShowHair=pose.StartsWith("hair",StringComparison.Ordinal);
                var open=pose=="open-A" || pose.StartsWith("combined",StringComparison.Ordinal) || pose=="hair-combined";
                var full=pose=="blink-full" || pose=="combined-full" || pose=="hair-combined"; var half=pose=="combined-half";
                var gaze=pose=="gaze-left-up" ? new Vector2(-1,1) : pose=="gaze-right-down" || pose=="combined-full" || pose=="hair-combined" ? new Vector2(1,-1) : half ? new Vector2(-.5f,.5f) : Vector2.zero;
                SetFace(open ? 0 : 1, open ? 1 : 0, full || pose=="blink-left" ? 1 : half ? .5f : 0, full || pose=="blink-right" ? 1 : half ? .5f : 0, gaze);
                for (var frame=0; frame<12; frame++) yield return new WaitForEndOfFrame();
                records.Add(SaveLive(output,pose,12));
            }
            SetFace(0,1,0,0,new Vector2(.5f,.5f)); IdleBlink=true; ResetIdleSchedule(true);
            var peak=false; var reopened=false;
            for (var frame=0; frame<180; frame++)
            {
                yield return new WaitForEndOfFrame();
                if (!peak && EffectiveBlinkLeft>=.999f && EffectiveBlinkRight>=.999f) { records.Add(SaveLive(output,"idle-peak-combined",frame+1)); peak=true; }
                else if (peak && EffectiveBlinkLeft<=.001f && EffectiveBlinkRight<=.001f) { records.Add(SaveLive(output,"idle-reopened-combined",frame+1)); reopened=true; break; }
            }
            File.WriteAllText(Path.Combine(output,"RenFaceStudyLiveCapture.json"),JsonUtility.ToJson(new LiveEvidence { unityVersion=Application.unityVersion,blinkSeed=BlinkSeed,idlePeakObserved=peak,idleReopened=reopened,captures=records.ToArray() },true));
            Debug.Log((peak && reopened ? "REN_FACE_LIVE_CAPTURE_OK: " : "REN_FACE_LIVE_CAPTURE_FAILED: idle cycle not observed ")+output);
            Application.Quit(peak && reopened?0:2);
        }
        LiveCapture SaveLive(string output,string pose,int frames)
        {
            var file="RenFaceStudy-live--"+pose+".png"; var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
            if (!screenshot) throw new InvalidOperationException("No live rendered screenshot.");
            File.WriteAllBytes(Path.Combine(output,file),screenshot.EncodeToPNG()); Destroy(screenshot);
            return new LiveCapture { file=file,mouthSeal=MouthSeal,jawOpenA=JawOpenA,blinkLeft=EffectiveBlinkLeft,blinkRight=EffectiveBlinkRight,gazeX=Gaze.x,gazeY=Gaze.y,hair=ShowHair,idle=IdleBlink,renderedFrames=frames };
        }
        void OnGUI()
        {
            if (!Viewer) return;
            if (label==null)
            {
                label=new GUIStyle(GUI.skin.label) { fontSize=16,wordWrap=true }; small=new GUIStyle(label) { fontSize=14 }; button=new GUIStyle(GUI.skin.button) { fontSize=15 };
                label.normal.textColor=new Color(.91f,.93f,.97f); small.normal.textColor=new Color(.73f,.78f,.84f);
            }
            var scale=Mathf.Min(Screen.width/1600f,Screen.height/900f); if (scale<=0) return;
            var matrix=GUI.matrix; var depth=GUI.depth; GUI.depth=-10;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)*.5f,(Screen.height-900*scale)*.5f,0),Quaternion.identity,new Vector3(scale,scale,1));
            try
            {
                Panel(new Rect(30,61,1280,28)); GUI.Label(new Rect(32,62,1270,26),"V2 face repair / blink and mouth controls / optional static texture trial / intermediate gaze needs repair",label);
                if (!Array.Exists(Viewer.Candidates,candidate=>candidate.Id==StaticTextureId))
                { Panel(new Rect(1326,22,245,50)); GUI.Label(new Rect(1332,29,230,42),"Unpainted construction study",small); }
                var rx=Viewer.ShowReference && Viewer.References.Length>0?1072f:812f; var width=1568-rx;
                Panel(new Rect(0,696,532,204)); Panel(new Rect(rx,696,width+32,204)); Panel(new Rect(532,799,rx-532,101));
                if (RightIsStatic)
                {
                    draggingGaze=false;
                    GUI.Label(new Rect(32,704,480,40),"Right panel is a static texture trial",label);
                    GUI.Label(new Rect(32,750,480,95),"Facial and hair controls are unavailable for the returned static model. Select a repaired-face candidate with the right panel's < > buttons to use those controls.",small);
                    GUI.Label(new Rect(rx,704,width,40),"Tripo texture trial · static",label);
                    GUI.Label(new Rect(rx,750,width,95),"Actual returned meshes, UVs and maps. Provider irises lack UV0 and appear flat grey. Painting and likeness remain unaccepted. View and lighting controls remain available.",small);
                    Panel(new Rect(0,864,1600,36));
                    GUI.Label(new Rect(32,868,1540,28),"Static paint comparison; no deformation transfer or likeness acceptance. Source normal maps are optional and default OFF. Live-source intermediate gaze remains unaccepted.",small);
                    return;
                }
                GUI.Label(new Rect(32,701,496,24),"Right face: independent eyelids",label);
                var l=Slider(32,735,496,"Blink L",BlinkLeft); var r=Slider(32,772,496,"Blink R",BlinkRight);
                if (!Mathf.Approximately(l,BlinkLeft) || !Mathf.Approximately(r,BlinkRight)) { IdleBlink=false; SetEyes(l,r,Gaze); }
                IdleBlink=GUI.Toggle(new Rect(32,814,238,28),IdleBlink,"Idle blink (right face)",button);
                var hair=GUI.Toggle(new Rect(280,814,248,28),ShowHair,"Fitted hair trial",button);
                if (hair!=ShowHair) { ShowHair=hair; ApplyNow(); }
                GUI.Label(new Rect(rx,701,width,24),"Right face: mouth and gaze",label);
                var seal=Slider(rx,735,width,"Mouth seal",MouthSeal); if (!Mathf.Approximately(seal,MouthSeal)) SetMouth(seal,Mathf.Min(JawOpenA,1-seal));
                var open=Slider(rx,772,width,"Open A",JawOpenA); if (!Mathf.Approximately(open,JawOpenA)) SetMouth(Mathf.Min(MouthSeal,1-open),open);
                if (GUI.Button(new Rect(rx,814,(width-12)/3,28),"Mouth rest",button)) ClosedRest();
                if (GUI.Button(new Rect(rx+(width-12)/3+6,814,(width-12)/3,28),"Source open",button)) SourceRest();
                if (GUI.Button(new Rect(rx+2*((width-12)/3+6),814,(width-12)/3,28),"Open A",button)) OpenA();
                // Gaze pad is below the reference; its artwork and navigation remain unobscured.
                var hasReference = rx > 1000;
                var pad=new Rect(hasReference ? 700 : 552,807,hasReference ? 188 : 132,52); GUI.Box(pad,"");
                var evt=Event.current;
                if (evt.type==EventType.MouseDown && pad.Contains(evt.mousePosition)) { draggingGaze=true; SetGazeFromPointer(pad,evt.mousePosition); evt.Use(); }
                if (draggingGaze && evt.type==EventType.MouseDrag) { SetGazeFromPointer(pad,evt.mousePosition); evt.Use(); }
                if (evt.type==EventType.MouseUp) draggingGaze=false;
                GUI.Label(new Rect(pad.center.x+Gaze.x*(pad.width*.5f-6)-4,pad.center.y-Gaze.y*(pad.height*.5f-6)-10,12,22),"+",label);
                if (hasReference)
                {
                    GUI.Label(new Rect(552,803,144,30),"Gaze X / Y",small);
                    if (GUI.Button(new Rect(552,834,136,28),"Center gaze",button)) SetEyes(BlinkLeft,BlinkRight,Vector2.zero);
                }
                if (GUI.Button(new Rect(hasReference ? 904 : 696,807,hasReference ? 132 : 100,52),"Reset all",button)) ResetAll();
                Panel(new Rect(0,864,1600,36));
                GUI.Label(new Rect(32,868,1540,28),"Left stays at rest. Intermediate gaze has visible sclera overlap and needs repair. Hair, painting, full speech, expressions, final likeness and phone performance remain pending.",small);
            }
            finally { GUI.matrix=matrix; GUI.depth=depth; }
        }
        void SetGazeFromPointer(Rect pad,Vector2 pointer) => SetEyes(BlinkLeft,BlinkRight,new Vector2((pointer.x-pad.center.x)/(pad.width*.5f),(pad.center.y-pointer.y)/(pad.height*.5f)));
        float Slider(float x,float y,float width,string name,float value)
        {
            GUI.Label(new Rect(x,y-4,155,25),name+" "+(value*100).ToString("0")+"%",small);
            return GUI.HorizontalSlider(new Rect(x+156,y+5,width-164,22),value,0,1);
        }
        static void Panel(Rect rect)
        {
            var color=GUI.color; GUI.color=new Color(.055f,.064f,.082f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=color;
        }
    }
}
