using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.CharacterArt
{
    /// <summary>Isolated complete-head review; the older face and texture viewers remain unchanged.</summary>
    public sealed class RenCompleteHeadController : MonoBehaviour
    {
        public RenBustComparisonViewer Viewer;
        [Range(0, 1)] public float MouthSeal = 1, OpenA, BlinkLeft, BlinkRight;
        public Vector2 Gaze;
        public bool IdleBlink = true, ShowHair = true, ShowCap = true;
        public int BlinkSeed = 20260914;
        public float EffectiveBlinkLeft { get; private set; }
        public float EffectiveBlinkRight { get; private set; }
        RenCompleteHeadRig leftRig, rightRig;
        GameObject leftRoot, rightRoot;
        System.Random blinkRandom;
        double nextBlink;
        bool wasIdle, dragGaze;
        GUIStyle label, small, button;
        public bool RightHasControls => rightRig;

        public void SetFace(float seal, float openA, float blinkL, float blinkR, Vector2 gaze)
        {
            MouthSeal = Mathf.Clamp01(seal); OpenA = Mathf.Clamp01(openA);
            var sum = MouthSeal + OpenA; if (sum > 1) { MouthSeal /= sum; OpenA /= sum; }
            BlinkLeft = Mathf.Clamp01(blinkL); BlinkRight = Mathf.Clamp01(blinkR);
            Gaze = new Vector2(Mathf.Clamp(gaze.x, -1, 1), Mathf.Clamp(gaze.y, -1, 1)); ApplyNow();
        }
        public void ResetFace() { IdleBlink = false; SetFace(1, 0, 0, 0, Vector2.zero); }
        void ResetIdle(bool immediate = false)
        {
            blinkRandom = new System.Random(BlinkSeed);
            nextBlink = Time.unscaledTimeAsDouble + (immediate ? 0 : 2 + 4 * blinkRandom.NextDouble());
            wasIdle = IdleBlink;
        }
        float IdleWeight()
        {
            if (!IdleBlink) { wasIdle = false; return 0; }
            if (!wasIdle || blinkRandom == null) ResetIdle();
            var elapsed = (float)(Time.unscaledTimeAsDouble - nextBlink);
            if (elapsed >= RenFaceStudyController.CloseSeconds + RenFaceStudyController.HoldSeconds + RenFaceStudyController.OpenSeconds)
            { nextBlink = Time.unscaledTimeAsDouble + 2 + 4 * blinkRandom.NextDouble(); return 0; }
            return RenFaceStudyController.BlinkAt(elapsed);
        }
        public void ApplyNow()
        {
            if (!Viewer) return;
            if (leftRoot != Viewer.LeftInstance)
            {
                leftRoot = Viewer.LeftInstance; leftRig = leftRoot ? leftRoot.GetComponent<RenCompleteHeadRig>() : null;
                if (leftRoot && !leftRig) RenFaceStudyController.ApplyFaceTo(leftRoot, 1, 0, 0, 0, Vector2.zero);
            }
            if (rightRoot != Viewer.RightInstance)
            {
                rightRoot = Viewer.RightInstance; rightRig = rightRoot ? rightRoot.GetComponent<RenCompleteHeadRig>() : null;
                if (rightRoot && !rightRig) RenFaceStudyController.ApplyFaceTo(rightRoot, 1, 0, 0, 0, Vector2.zero);
            }
            var idle = IdleWeight(); EffectiveBlinkLeft = Mathf.Max(BlinkLeft, idle); EffectiveBlinkRight = Mathf.Max(BlinkRight, idle);
            if (leftRig) { leftRig.SetCapVisible(ShowCap); leftRig.Apply(1, 0, 0, 0, Vector2.zero); leftRig.SetHairVisible(ShowHair); }
            if (rightRig) { rightRig.SetCapVisible(ShowCap); rightRig.Apply(MouthSeal, OpenA, EffectiveBlinkLeft, EffectiveBlinkRight, Gaze); rightRig.SetHairVisible(ShowHair); }
        }
        void LateUpdate() => ApplyNow();

        [Serializable] sealed class PoseRecord
        {
            public string file, pose, lighting;
            public int firstFrame, captureFrame;
            public float mouthSeal, openA, blinkL, blinkR, gazeX, gazeY, yaw;
            public bool hair, cap, capObjectVisible;
            public RenCompleteHeadRig.CapWeight[] capWeights;
        }
        [Serializable] sealed class LiveEvidence
        {
            public string unityVersion;
            public string method = "Running player; actual live renderer after 12 player frame updates per pose, then an explicit URP GPU camera render request. No BakeMesh capture snapshots. Actual cap visibility and fitting weights recorded per pose.";
            public PoseRecord[] captures;
        }
        IEnumerator Start()
        {
            ResetIdle(); ApplyNow();
            var args = Environment.GetCommandLineArgs(); var motionIndex = Array.IndexOf(args, "-renCompleteMotionCapture");
            if (motionIndex >= 0 && motionIndex + 1 < args.Length)
            {
                yield return CaptureMotion(Path.GetFullPath(args[motionIndex + 1]));
                Application.Quit(0); yield break;
            }
            var index = Array.IndexOf(args, "-renCompleteLiveCapture");
            if (index < 0 || index + 1 >= args.Length) yield break;
            var output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
            Application.runInBackground = true; Screen.SetResolution(1600, 900, false);
            for (var i = 0; i < Viewer.Candidates.Length; i++)
                if (Viewer.Candidates[i].Prefab && Viewer.Candidates[i].Prefab.GetComponent<RenCompleteHeadRig>())
                { Viewer.SelectCandidate(i, true); Viewer.SelectCandidate(i, false); break; }
            ResetFace(); ApplyNow();
            if (!RightHasControls) throw new InvalidOperationException("Live complete-head capture requires the actual repaired candidate.");
            var records = new List<PoseRecord>();
            var focused = Array.IndexOf(args, "-renCompleteFocusedCapture") >= 0;
            var poses = focused ? new[] { "rest", "rest-club", "partial-diagonal-blink", "open-A-diagonal", "full-blink", "cap-off", "cap-off-reset", "cap-off-rebind", "cap-on-restored" } :
                new[] { "rest", "partial-diagonal", "partial-diagonal-blink", "opposite-diagonal-blink", "open-A-diagonal", "full-blink", "blink-left", "blink-right", "reset", "cap-off", "cap-off-reset", "cap-off-rebind", "cap-off-hair-hidden", "cap-on-restored" };
            foreach (var pose in poses)
            {
                ShowCap = !pose.StartsWith("cap-off", StringComparison.Ordinal);
                ShowHair = pose != "cap-off-hair-hidden";
                if (pose == "cap-off-rebind") { Viewer.SelectCandidate(Viewer.RightIndex, false); ApplyNow(); }
                if (pose == "cap-off-reset") ResetFace();
                var partial = pose == "partial-diagonal-blink" || pose == "opposite-diagonal-blink";
                var gaze = pose == "partial-diagonal" || pose == "partial-diagonal-blink" || pose == "open-A-diagonal" ? new Vector2(.5f, .5f) :
                    pose == "opposite-diagonal-blink" ? new Vector2(-.5f, -.5f) : Vector2.zero;
                SetFace(pose == "open-A-diagonal" ? 0 : 1, pose == "open-A-diagonal" ? 1 : 0,
                    pose == "full-blink" || pose == "blink-left" ? 1 : partial ? .5f : 0,
                    pose == "full-blink" || pose == "blink-right" ? 1 : partial ? .5f : 0, gaze);
                var views = focused ? pose == "rest" ? new[] { 0f, 45f, 90f } : partial || pose == "cap-off" ? new[] { 0f, 45f } : new[] { 0f } :
                    pose == "rest" ? new[] { 0f, 45f, -45f, 90f } : partial || pose == "full-blink" || pose == "cap-off" ? new[] { 0f, 45f, -45f } : new[] { 0f };
                foreach (var yaw in views)
                {
                    var lighting = pose == "rest-club" ? 2 : 1;
                    Viewer.SetView(yaw); Viewer.SetLighting(lighting); var firstFrame = Time.frameCount;
                    for (var frame = 0; frame < 12; frame++) yield return null;
                    ApplyNow();
                    var capWeights = rightRig.CaptureCapWeights();
                    var capVisible = Array.Exists(rightRig.CapObjects, item => item && item.activeSelf);
                    if (capWeights.Length == 0 || Array.Exists(capWeights, item => Mathf.Abs(item.weight - (ShowCap ? 100 : 0)) > .001f) || capVisible != ShowCap)
                        throw new InvalidOperationException("Live cap fitting/visibility does not match the requested state: " + pose);
                    var file = "RenCompleteHead-live--" + pose + "--" + (yaw == 0 ? "front" : yaw == 45 ? "quarter" : yaw == -45 ? "other-quarter" : "profile") + ".png";
                    CaptureLiveCamera(Viewer.RightCamera, Path.Combine(output, file));
                    records.Add(new PoseRecord { file = file, pose = pose, firstFrame = firstFrame, captureFrame = Time.frameCount,
                        lighting = lighting == 2 ? "Club diffuse review" : "Neutral diffuse review",
                        mouthSeal = MouthSeal, openA = OpenA, blinkL = EffectiveBlinkLeft, blinkR = EffectiveBlinkRight,
                        gazeX = Gaze.x, gazeY = Gaze.y, yaw = yaw, hair = ShowHair, cap = ShowCap, capObjectVisible = capVisible, capWeights = capWeights });
                }
            }
            File.WriteAllText(Path.Combine(output, "RenCompleteHeadLiveEvidence.json"), JsonUtility.ToJson(new LiveEvidence { unityVersion = Application.unityVersion, captures = records.ToArray() }, true));
            Debug.Log("REN_COMPLETE_HEAD_LIVE_CAPTURE_OK: " + output); Application.Quit(0);
        }

        [Serializable] sealed class MotionFrame
        {
            public string file, sha256;
            public int index, playerFrame;
            public float seconds, mouthSeal, openA, blink, gazeX, gazeY;
        }
        [Serializable] sealed class MotionEvidence
        {
            public string sourceFbxSha256, gazeContractSha256, runtimeRigSha256, runtimeControllerSha256;
            public string unityVersion, lighting = "Neutral diffuse review";
            public float cameraYaw = 20;
            public bool hairVisible, capVisible;
            public RenCompleteHeadRig.CapWeight[] capWeights;
            public int frameCount = 150, outputFps = 30, width = 1280, height = 720;
            public string method = "Five-second visual motion evidence from explicit live Unity GPU render requests. Fixed output sampling is not a device-performance measurement.";
            public MotionFrame[] frames;
        }
        IEnumerator CaptureMotion(string output)
        {
            Directory.CreateDirectory(output); Application.runInBackground = true;
            for (var i = 0; i < Viewer.Candidates.Length; i++)
                if (Viewer.Candidates[i].Prefab && Viewer.Candidates[i].Prefab.GetComponent<RenCompleteHeadRig>())
                { Viewer.SelectCandidate(i, true); Viewer.SelectCandidate(i, false); break; }
            ShowHair = true; ShowCap = true; ResetFace(); ApplyNow();
            if (!RightHasControls) throw new InvalidOperationException("Motion capture requires the repaired complete-head candidate.");
            Viewer.SetView(20); Viewer.SetLighting(1);
            for (var i = 0; i < 12; i++) yield return null;
            var records = new List<MotionFrame>();
            for (var i = 0; i < 150; i++)
            {
                var t = i / 30f;
                var gaze = new Vector2(.5f * Mathf.Sin(t * Mathf.PI * 2 / 5), .35f * Mathf.Sin(t * Mathf.PI * 2 / 5));
                var blink = RenFaceStudyController.BlinkAt(t - .9f);
                var open = t < 2 ? 0 : t < 2.5f ? Mathf.SmoothStep(0, 1, (t - 2) / .5f) : t < 3 ? 1 : Mathf.SmoothStep(1, 0, (t - 3) / .5f);
                SetFace(1 - open, open, blink, blink, gaze);
                // Advance the running renderer before the explicit GPU request; no CPU-baked mesh is substituted.
                yield return null; ApplyNow();
                if (!Array.Exists(rightRig.CapObjects, item => item && item.activeSelf) ||
                    Array.Exists(rightRig.CaptureCapWeights(), item => Mathf.Abs(item.weight - 100) > .001f))
                    throw new InvalidOperationException("Motion capture lost the cap fitting state.");
                var file = "frame-" + i.ToString("D4") + ".png"; var path = Path.Combine(output, file);
                CaptureLiveCamera(Viewer.RightCamera, path, 1280, 720);
                string hash;
                using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                records.Add(new MotionFrame { file = file, sha256 = hash, index = i, playerFrame = Time.frameCount, seconds = t,
                    mouthSeal = MouthSeal, openA = OpenA, blink = EffectiveBlinkLeft, gazeX = Gaze.x, gazeY = Gaze.y });
            }
            File.WriteAllText(Path.Combine(output, "RenCompleteHeadMotionEvidence.json"), JsonUtility.ToJson(new MotionEvidence {
                unityVersion = Application.unityVersion, hairVisible = ShowHair, capVisible = ShowCap,
                capWeights = rightRig.CaptureCapWeights(),
                sourceFbxSha256 = rightRig.SourceSha256, gazeContractSha256 = rightRig.ContractSha256,
                runtimeRigSha256 = rightRig.RuntimeRigSha256, runtimeControllerSha256 = rightRig.RuntimeControllerSha256,
                frames = records.ToArray() }, true));
            Debug.Log("REN_COMPLETE_HEAD_MOTION_CAPTURE_OK: " + output);
        }

        static void CaptureLiveCamera(Camera camera, string path, int width = 1536, int height = 1536)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var previous = RenderTexture.active; var rect = camera.rect; var aspect = camera.aspect;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            try
            {
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)width / height; target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.rect = rect; camera.aspect = aspect; RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(image);
            }
        }

        void OnGUI()
        {
            if (!Viewer) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
                small = new GUIStyle(label) { fontSize = 14 }; button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
                label.normal.textColor = new Color(.91f, .93f, .97f); small.normal.textColor = new Color(.73f, .78f, .84f);
            }
            var scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f); if (scale <= 0) return;
            var matrix = GUI.matrix; var depth = GUI.depth; GUI.depth = -10;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) * .5f, (Screen.height - 900 * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
            try
            {
                Panel(new Rect(30, 61, 1280, 28)); GUI.Label(new Rect(32, 62, 1270, 26), "Complete-head review / painted head and hair trial / continuous ellipsoid gaze / independent blink and mouth", label);
                var rx = Viewer.ShowReference && Viewer.References.Length > 0 ? 1072f : 812f; var width = 1568 - rx;
                Panel(new Rect(0, 696, 532, 204)); Panel(new Rect(rx, 696, width + 32, 204)); Panel(new Rect(532, 799, rx - 532, 101));
                if (!RightHasControls)
                {
                    dragGaze = false;
                    GUI.Label(new Rect(32, 704, 480, 90), "This panel is an earlier comparison source. Select a complete-head candidate with the right < > buttons to use the repaired controls.", label);
                    GUI.Label(new Rect(rx, 704, width, 90), "Earlier source held at rest. Its existing viewer remains independently available.", small);
                }
                else
                {
                    GUI.Label(new Rect(32, 701, 496, 24), "Right face: independent eyelids", label);
                    var l = Slider(32, 735, 496, "Blink L", BlinkLeft); var r = Slider(32, 772, 496, "Blink R", BlinkRight);
                    if (!Mathf.Approximately(l, BlinkLeft) || !Mathf.Approximately(r, BlinkRight)) { IdleBlink = false; SetFace(MouthSeal, OpenA, l, r, Gaze); }
                    IdleBlink = GUI.Toggle(new Rect(32, 814, 160, 28), IdleBlink, "Idle blink", button);
                    ShowHair = GUI.Toggle(new Rect(200, 814, 160, 28), ShowHair, "Hair", button);
                    ShowCap = GUI.Toggle(new Rect(368, 814, 160, 28), ShowCap, "Cap", button);
                    GUI.Label(new Rect(rx, 701, width, 24), "Right face: mouth and gaze", label);
                    var seal = Slider(rx, 735, width, "Mouth seal", MouthSeal);
                    if (!Mathf.Approximately(seal, MouthSeal)) SetFace(seal, Mathf.Min(OpenA, 1 - seal), BlinkLeft, BlinkRight, Gaze);
                    var open = Slider(rx, 772, width, "Open A", OpenA);
                    if (!Mathf.Approximately(open, OpenA)) SetFace(Mathf.Min(MouthSeal, 1 - open), open, BlinkLeft, BlinkRight, Gaze);
                    if (GUI.Button(new Rect(rx, 814, (width - 6) / 2, 28), "Mouth rest", button)) SetFace(1, 0, BlinkLeft, BlinkRight, Gaze);
                    if (GUI.Button(new Rect(rx + (width + 6) / 2, 814, (width - 6) / 2, 28), "Open A", button)) SetFace(0, 1, BlinkLeft, BlinkRight, Gaze);
                    var hasReference = rx > 1000; var pad = new Rect(hasReference ? 700 : 552, 807, hasReference ? 188 : 132, 52);
                    GUI.Box(pad, ""); var evt = Event.current;
                    if (evt.type == EventType.MouseDown && pad.Contains(evt.mousePosition)) { dragGaze = true; PointerGaze(pad, evt.mousePosition); evt.Use(); }
                    if (dragGaze && evt.type == EventType.MouseDrag) { PointerGaze(pad, evt.mousePosition); evt.Use(); }
                    if (evt.type == EventType.MouseUp) dragGaze = false;
                    GUI.Label(new Rect(pad.center.x + Gaze.x * (pad.width * .5f - 6) - 4, pad.center.y - Gaze.y * (pad.height * .5f - 6) - 10, 12, 22), "+", label);
                    if (hasReference)
                    {
                        GUI.Label(new Rect(552, 803, 144, 30), "Gaze X / Y", small);
                        if (GUI.Button(new Rect(552, 834, 136, 28), "Center gaze", button)) SetFace(MouthSeal, OpenA, BlinkLeft, BlinkRight, Vector2.zero);
                    }
                    if (GUI.Button(new Rect(hasReference ? 904 : 696, 807, hasReference ? 132 : 100, 52), "Reset face", button)) ResetFace();
                }
                Panel(new Rect(0, 864, 1600, 36)); GUI.Label(new Rect(32, 868, 1540, 28), "Left stays at rest. Complete-head construction and paint trial; final likeness, full speech/emotions, and iPhone performance require separate acceptance.", small);
            }
            finally { GUI.matrix = matrix; GUI.depth = depth; }
        }
        void PointerGaze(Rect pad, Vector2 point) => SetFace(MouthSeal, OpenA, BlinkLeft, BlinkRight, new Vector2((point.x - pad.center.x) / (pad.width * .5f), (pad.center.y - point.y) / (pad.height * .5f)));
        float Slider(float x, float y, float width, string name, float value)
        {
            GUI.Label(new Rect(x, y - 4, 155, 25), name + " " + (value * 100).ToString("0") + "%", small);
            return GUI.HorizontalSlider(new Rect(x + 156, y + 5, width - 164, 22), value, 0, 1);
        }
        static void Panel(Rect rect)
        {
            var color = GUI.color; GUI.color = new Color(.055f, .064f, .082f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = color;
        }
    }
}
