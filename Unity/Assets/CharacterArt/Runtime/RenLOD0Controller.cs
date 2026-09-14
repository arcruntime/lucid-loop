using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    [DefaultExecutionOrder(100)]
    public sealed class RenLOD0Controller : MonoBehaviour
    {
        public GameObject LOD0, LOD1, Cap;
        public GameObject LOD1Cap, LOD1Headphones;
        public LODGroup CharacterLODs;
        public Transform Head, Neck;
        public Camera ReviewCamera;
        public RenAssemblyHairMotion HairMotion;
        public Animator BodyAnimator;
        public AnimationClip BodyIdle;
        public RenGuardedPose GuardedPose;
        Transform[] guardedBones;
        float guardedAmount;
        public GameObject BodyIdleRoot;
        float bodyTime;
        public Texture2D DesignerReference;
        public Light CyanLight, MagentaLight;
        public bool MovingLights;
        public bool ShowControls = true;
        public bool IdleBlink = true, IdleActing = true, ShowReference;
        public float HeadTurn, HeadTilt, HeadRoll, BlinkL, BlinkR;
        public bool CaptureRequested;
        public string CapturePath;
        readonly Dictionary<string, float> values = new Dictionary<string, float>();
        readonly Dictionary<string, float> speechWeights = new Dictionary<string, float>();
        bool externalSpeech;
        public void SetSpeechWeights(IReadOnlyDictionary<string, float> snapshot)
        {
            externalSpeech = true;
            speechWeights.Clear();
            if (snapshot == null) return;
            foreach (var pair in snapshot)
                if (pair.Key.StartsWith("speech_", StringComparison.Ordinal) && float.IsFinite(pair.Value))
                    speechWeights[pair.Key] = Mathf.Clamp01(pair.Value);
        }
        SkinnedMeshRenderer[] renderers;
        Quaternion headRest, neckRest;
        Quaternion headWorldRest;
        Vector3 faceForwardRest, faceRightRest;
        Material[] faceMaterials;
        Vector2 scroll;
        float nextBlink = 2.2f, blinkStart = -10;
        int selectedPose;
        bool useLOD1, closeUp;
        Transform[] rigTransforms;
        Vector3[] rigPositions;
        Quaternion[] rigRotations;
        bool wasActing;
        readonly string[] poses = { "rest", "A", "I", "U", "E", "O", "MBP", "FV", "TH", "L", "SZ", "CH_SH_J", "R", "W_OO", "amused", "skeptical", "focused", "alert", "guarded" };
        void Start()
        {
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in renderers) for (int i = 0; i < r.sharedMesh.blendShapeCount; i++) values[r.sharedMesh.GetBlendShapeName(i)] = 0;
            if (Head) headRest = Head.localRotation;
            if (Head) headWorldRest = Head.rotation;
            faceForwardRest = LOD0.transform.forward;
            faceRightRest = LOD0.transform.right;
            if (Neck) neckRest = Neck.localRotation;
            faceMaterials = renderers.SelectMany(r => r.materials).Where(m => m.HasProperty("_UseWorldFace")).Distinct().ToArray();
            rigTransforms = LOD0.GetComponentsInChildren<Transform>(true);
            rigPositions = rigTransforms.Select(t => t.localPosition).ToArray();
            rigRotations = rigTransforms.Select(t => t.localRotation).ToArray();
            wasActing = IdleActing;
            if (GuardedPose && BodyIdleRoot)
                guardedBones = GuardedPose.Bones.Select(b => BodyIdleRoot.transform.Find(b.Path)).ToArray();
        }
        void Update()
        {
            if (BodyAnimator) BodyAnimator.enabled = IdleActing;
            if (IdleActing && BodyIdle && BodyIdleRoot)
            {
                bodyTime = Mathf.Repeat(bodyTime + Time.deltaTime, BodyIdle.length);
                BodyIdle.SampleAnimation(BodyIdleRoot, bodyTime);
            }
            if (wasActing && !IdleActing)
                for (int i = 0; i < rigTransforms.Length; i++) { rigTransforms[i].localPosition = rigPositions[i]; rigTransforms[i].localRotation = rigRotations[i]; }
            wasActing = IdleActing;
            if (GuardedPose && guardedBones != null)
            {
                float target = values.Where(p => p.Key.EndsWith("emotion_Guarded", StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).DefaultIfEmpty(0).Max();
                float duration = target > guardedAmount ? GuardedPose.EnterSeconds : GuardedPose.ExitSeconds;
                guardedAmount = Mathf.MoveTowards(guardedAmount, target, Time.deltaTime / Mathf.Max(.01f, duration));
                float amount = Mathf.SmoothStep(0, 1, guardedAmount);
                for (int i = 0; i < guardedBones.Length; i++)
                {
                    var bone = guardedBones[i]; if (!bone) continue;
                    var pose = GuardedPose.Bones[i];
                    bool fromIdle = IdleActing && BodyIdle && BodyIdleRoot;
                    bone.localPosition = Vector3.Lerp(fromIdle ? bone.localPosition : pose.RestPosition, pose.Position, amount);
                    bone.localRotation = Quaternion.Slerp(fromIdle ? bone.localRotation : pose.RestRotation, pose.Rotation, amount);
                }
            }
        }
        void LateUpdate()
        {
            if (LOD1Cap && Cap && LOD1Cap.activeSelf != Cap.activeSelf) LOD1Cap.SetActive(Cap.activeSelf);
            if (Time.time >= nextBlink) { blinkStart = Time.time; nextBlink = Time.time + 2.8f + .6f * Mathf.Sin(Time.time); }
            float t = (Time.time - blinkStart) / .24f;
            float blink = IdleBlink && t >= 0 && t <= 1 ? Mathf.Sin(t * Mathf.PI) : 0;
            bool animated = (BodyAnimator && BodyAnimator.enabled) || (IdleActing && BodyIdle && BodyIdleRoot);
            if (Head) Head.localRotation = (animated ? Head.localRotation : headRest) * Quaternion.Euler(HeadTilt + (!animated && IdleActing ? Mathf.Sin(Time.time * .7f) * 1.1f : 0), HeadTurn + (!animated && IdleActing ? Mathf.Sin(Time.time * .43f) * 1.5f : 0), HeadRoll);
            if (Neck && !animated) Neck.localRotation = neckRest * Quaternion.Euler(IdleActing ? Mathf.Sin(Time.time * .8f) * .25f : 0, 0, 0);
            if (Head) foreach (var material in faceMaterials) { material.SetFloat("_UseWorldFace", 1); material.SetVector("_FaceForwardWorld", Head.rotation * Quaternion.Inverse(headWorldRest) * faceForwardRest); material.SetVector("_FaceRightWorld", Head.rotation * Quaternion.Inverse(headWorldRest) * faceRightRest); }
            if (MovingLights) { if (CyanLight) CyanLight.transform.position = new Vector3(Mathf.Sin(Time.time * .5f), 1.4f, -.7f); if (MagentaLight) MagentaLight.transform.position = new Vector3(Mathf.Cos(Time.time * .37f), 1.6f, .4f); }
            if (externalSpeech)
                foreach (var key in values.Keys.ToArray())
                {
                    var name = key.Substring(key.LastIndexOf('.') + 1);
                    if (name.StartsWith("speech_", StringComparison.Ordinal)) values[key] = speechWeights.TryGetValue(name, out var weight) ? weight : 0;
                }
            var mixed = RenAssemblyFaceMixer.Mix(values, Mathf.Max(BlinkL, blink), Mathf.Max(BlinkR, blink));
            float closedL=mixed.Where(p=>p.Key.EndsWith("eyeBlinkL",StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).DefaultIfEmpty(0).Max();
            float closedR=mixed.Where(p=>p.Key.EndsWith("eyeBlinkR",StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).DefaultIfEmpty(0).Max();
            foreach(var material in faceMaterials){material.SetFloat("_ClosedEyeBlendL",Mathf.InverseLerp(.35f,1,closedL));material.SetFloat("_ClosedEyeBlendR",Mathf.InverseLerp(.35f,1,closedR));}
            foreach (var r in renderers)
                for (int i = 0; i < r.sharedMesh.blendShapeCount; i++)
                {
                    string key = r.sharedMesh.GetBlendShapeName(i);
                    // The cap fits the intact hair. The rejected legacy crown-tuck
                    // target is never driven, including when the cap is visible.
                    float value = key.EndsWith("capOn", StringComparison.OrdinalIgnoreCase) ? 0
                        : key.EndsWith("guardedBodyCorrective", StringComparison.OrdinalIgnoreCase) ? Mathf.SmoothStep(0, 1, guardedAmount)
                        : mixed.TryGetValue(key, out var v) ? v : 0;
                    r.SetBlendShapeWeight(i, Mathf.Clamp01(value) * 100);
                }
            if (CaptureRequested) { CaptureRequested = false; ScreenCapture.CaptureScreenshot(CapturePath); }
        }
        public void SetPose(int index)
        {
            selectedPose = index;
            foreach (var key in values.Keys.ToArray()) values[key] = 0;
            if (index == 0) return;
            string suffix = index <= 13 ? "speech_" + poses[index] : poses[index];
            foreach (var key in values.Keys.ToArray()) if (key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) values[key] = 1;
        }
        public void SetGaze(Vector2 left, Vector2 right)
        {
            foreach(var key in values.Keys.ToArray())
            {
                var name=key.Substring(key.LastIndexOf('.')+1);
                if(!name.StartsWith("gaze",StringComparison.Ordinal))continue;
                var point=name.EndsWith("L",StringComparison.Ordinal)?left:right;
                float value=name.StartsWith("gazeRight")?point.x:name.StartsWith("gazeLeft")?-point.x:name.StartsWith("gazeUp")?point.y:-point.y;
                values[key]=float.IsFinite(value)?Mathf.Clamp01(value):0;
            }
        }
        public void SetFacialControls(IReadOnlyDictionary<string, float> snapshot)
        {
            foreach (var key in values.Keys.ToArray())
            {
                var semantic = key.Substring(key.LastIndexOf('.') + 1);
                values[key] = snapshot != null && snapshot.TryGetValue(semantic, out var weight) && float.IsFinite(weight)
                    ? Mathf.Clamp01(weight) : 0;
            }
        }
        void OnGUI()
        {
            if (!ShowControls) return;
            float scale = Mathf.Max(.6f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float height = Screen.height / scale;
            GUILayout.BeginArea(new Rect(16, 16, 286, height - 32), GUI.skin.box);
            GUILayout.Label("Ren · assembled character");
            GUILayout.Label("LOD0 · body, face, hair and accessories");
            IdleActing = GUILayout.Toggle(IdleActing, (BodyAnimator || BodyIdle) ? "Walt / Kimodo body idle" : "Idle head motion");
            IdleBlink = GUILayout.Toggle(IdleBlink, "Idle blink");
            MovingLights = GUILayout.Toggle(MovingLights, "Moving club lights");
            if (HairMotion) HairMotion.MotionEnabled = GUILayout.Toggle(HairMotion.MotionEnabled, "Secondary hair motion");
            if (Cap) { bool on = GUILayout.Toggle(Cap.activeSelf, "Baseball cap"); Cap.SetActive(on); if (HairMotion) HairMotion.CapOn = on; }
            if (LOD1 && CharacterLODs)
            {
                useLOD1 = GUILayout.Toggle(useLOD1, "Preview LOD1");
                CharacterLODs.ForceLOD(useLOD1 ? 1 : 0);
            }
            bool nextClose = GUILayout.Toggle(closeUp, "Face close-up");
            if (nextClose != closeUp && ReviewCamera && Head)
            {
                closeUp = nextClose;
                Vector3 target = closeUp ? Head.position + Vector3.up * .1f : new Vector3(0, .9f, 0);
                ReviewCamera.transform.position = target + new Vector3(.12f, closeUp ? .02f : .08f, closeUp ? -.7f : -2.7f);
                ReviewCamera.transform.LookAt(target);
            }
            ShowReference = GUILayout.Toggle(ShowReference, "Designer reference");
            GUILayout.Label("Head turn"); HeadTurn = GUILayout.HorizontalSlider(HeadTurn, -45, 45);
            GUILayout.Label("Head tilt"); HeadTilt = GUILayout.HorizontalSlider(HeadTilt, -20, 20);
            GUILayout.Label("Left blink"); BlinkL = GUILayout.HorizontalSlider(BlinkL, 0, 1);
            GUILayout.Label("Right blink"); BlinkR = GUILayout.HorizontalSlider(BlinkR, 0, 1);
            int pose = GUILayout.SelectionGrid(selectedPose, poses, 3);
            if (pose != selectedPose) SetPose(pose);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var key in values.Keys.Where(k => !k.EndsWith("capOn", StringComparison.OrdinalIgnoreCase) && !k.EndsWith("guardedBodyCorrective", StringComparison.OrdinalIgnoreCase)).ToArray()) { GUILayout.Label(key); values[key] = GUILayout.HorizontalSlider(values[key], 0, 1); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (ShowReference && DesignerReference) GUI.DrawTexture(new Rect(Screen.width / scale - 520, 20, 500, 334), DesignerReference, ScaleMode.ScaleToFit);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
