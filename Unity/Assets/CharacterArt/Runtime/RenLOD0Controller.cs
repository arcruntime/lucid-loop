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
        public Transform Head, Neck;
        public Camera ReviewCamera;
        public RenAssemblyHairMotion HairMotion;
        public Animator BodyAnimator;
        public AnimationClip BodyIdle;
        public GameObject BodyIdleRoot;
        float bodyTime;
        public Texture2D DesignerReference;
        public Light CyanLight, MagentaLight;
        public bool MovingLights;
        public bool IdleBlink = true, IdleActing = true, ShowReference;
        public float HeadTurn, HeadTilt, BlinkL, BlinkR;
        public bool CaptureRequested;
        public string CapturePath;
        readonly Dictionary<string, float> values = new Dictionary<string, float>();
        SkinnedMeshRenderer[] renderers;
        Quaternion headRest, neckRest;
        Quaternion headWorldRest;
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
            if (Neck) neckRest = Neck.localRotation;
            faceMaterials = renderers.SelectMany(r => r.materials).Where(m => m.HasProperty("_UseWorldFace")).Distinct().ToArray();
            rigTransforms = LOD0.GetComponentsInChildren<Transform>(true);
            rigPositions = rigTransforms.Select(t => t.localPosition).ToArray();
            rigRotations = rigTransforms.Select(t => t.localRotation).ToArray();
            wasActing = IdleActing;
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
        }
        void LateUpdate()
        {
            if (Time.time >= nextBlink) { blinkStart = Time.time; nextBlink = Time.time + 2.8f + .6f * Mathf.Sin(Time.time); }
            float t = (Time.time - blinkStart) / .24f;
            float blink = IdleBlink && t >= 0 && t <= 1 ? Mathf.Sin(t * Mathf.PI) : 0;
            bool animated = (BodyAnimator && BodyAnimator.enabled) || (IdleActing && BodyIdle && BodyIdleRoot);
            if (Head) Head.localRotation = (animated ? Head.localRotation : headRest) * Quaternion.Euler(HeadTilt + (!animated && IdleActing ? Mathf.Sin(Time.time * .7f) * 1.1f : 0), HeadTurn + (!animated && IdleActing ? Mathf.Sin(Time.time * .43f) * 1.5f : 0), 0);
            if (Neck && !animated) Neck.localRotation = neckRest * Quaternion.Euler(IdleActing ? Mathf.Sin(Time.time * .8f) * .25f : 0, 0, 0);
            if (Head) foreach (var material in faceMaterials) { material.SetFloat("_UseWorldFace", 1); material.SetVector("_FaceForwardWorld", Head.rotation * Quaternion.Inverse(headWorldRest) * Vector3.back); material.SetVector("_FaceRightWorld", Head.rotation * Quaternion.Inverse(headWorldRest) * Vector3.left); }
            if (MovingLights) { if (CyanLight) CyanLight.transform.position = new Vector3(Mathf.Sin(Time.time * .5f), 1.4f, -.7f); if (MagentaLight) MagentaLight.transform.position = new Vector3(Mathf.Cos(Time.time * .37f), 1.6f, .4f); }
            var mixed = RenAssemblyFaceMixer.Mix(values, Mathf.Max(BlinkL, blink), Mathf.Max(BlinkR, blink));
            float closedL=mixed.Where(p=>p.Key.EndsWith("eyeBlinkL",StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).DefaultIfEmpty(0).Max();
            float closedR=mixed.Where(p=>p.Key.EndsWith("eyeBlinkR",StringComparison.OrdinalIgnoreCase)).Select(p=>p.Value).DefaultIfEmpty(0).Max();
            foreach(var material in faceMaterials){material.SetFloat("_ClosedEyeBlendL",Mathf.InverseLerp(.35f,1,closedL));material.SetFloat("_ClosedEyeBlendR",Mathf.InverseLerp(.35f,1,closedR));}
            foreach (var r in renderers)
                for (int i = 0; i < r.sharedMesh.blendShapeCount; i++)
                {
                    string key = r.sharedMesh.GetBlendShapeName(i);
                    float value = key.EndsWith("capOn", StringComparison.OrdinalIgnoreCase) ? (Cap && Cap.activeSelf ? 1 : 0) : mixed.TryGetValue(key, out var v) ? v : 0;
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
        void OnGUI()
        {
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
            if (LOD1) { useLOD1 = GUILayout.Toggle(useLOD1, "LOD1"); if (LOD0) LOD0.SetActive(!useLOD1); LOD1.SetActive(useLOD1); }
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
            foreach (var key in values.Keys.ToArray()) { GUILayout.Label(key); values[key] = GUILayout.HorizontalSlider(values[key], 0, 1); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (ShowReference && DesignerReference) GUI.DrawTexture(new Rect(Screen.width / scale - 520, 20, 500, 334), DesignerReference, ScaleMode.ScaleToFit);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
