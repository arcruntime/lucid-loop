using System;
using System.Collections.Generic;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // An optional artist-owned adapter may implement this after clip/rig review.
    // Returning true for a fall promises to HOLD its terminal pose until ResetPresentation.
    // PlayBodyMotion(non-looping) alone cannot satisfy that promise: it returns to idle.
    public interface IEncounterBodyPresentation
    {
        bool TryPresent(CharacterActor actor, string action, bool holdUntilReset);
        void ResetPresentation(CharacterActor actor);
    }

    // Deliberately a primitive visual placeholder, not imported/retargeted final animation.
    // World transforms, facts, navigation, catastrophe and win state remain server-owned.
    public sealed class EncounterPrimitivePresentation : MonoBehaviour
    {
        public EncounterCoordinator Coordinator;
        public MonoBehaviour ReviewedBodyPresentation;
        public float FallSeconds = .45f;
        public string Diagnostic { get; private set; } = "not_bound";
        readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>(StringComparer.Ordinal);
        EncounterCoordinator subscribed;
        string loopId;
        bool falling, recording, externalFall, externalRecording;
        float fallProgress;
        IEncounterBodyPresentation Reviewed => ReviewedBodyPresentation as IEncounterBodyPresentation;

        sealed class Pose
        {
            public CharacterActor Actor;
            public Transform Visual, Arm, Hand;
            public Vector3 Position, ArmPosition, HandPosition;
            public Quaternion Rotation, ArmRotation, HandRotation;
            public GameObject Phone;
            public bool Primitive;
        }

        void OnEnable() => BindCoordinator();
        void Update() { BindCoordinator(); AdvancePresentation(Time.unscaledDeltaTime); }

        void BindCoordinator()
        {
            if (subscribed == Coordinator) return;
            if (subscribed) subscribed.StateChanged -= ApplyState;
            RestoreAll(); subscribed = Coordinator;
            if (!subscribed) return;
            BindActors(subscribed.Characters);
            subscribed.StateChanged += ApplyState;
            if (subscribed.State.HasSnapshot) ApplyState(subscribed.State);
        }

        public void BindActors(CharacterActor[] actors)
        {
            RestoreAll(); DestroyPhones(); poses.Clear(); loopId = null;
            foreach (var actor in actors ?? Array.Empty<CharacterActor>())
            {
                if (!actor || (actor.Id != "luca" && actor.Id != "maya") || !actor.Visual || poses.ContainsKey(actor.Id)) continue;
                var pose = new Pose { Actor = actor, Visual = actor.Visual, Position = actor.Visual.localPosition,
                    Rotation = actor.Visual.localRotation,
                    Primitive = actor.Visual.Find("Torso") && !actor.GetComponentInChildren<Animator>(true) &&
                        !actor.Visual.GetComponentInChildren<SkinnedMeshRenderer>(true) };
                if (pose.Primitive)
                    foreach (Transform part in pose.Visual)
                    {
                        if (part.localPosition.x <= 0) continue;
                        if (part.name == "Arm") { pose.Arm = part; pose.ArmPosition = part.localPosition; pose.ArmRotation = part.localRotation; }
                        if (part.name == "Hand") { pose.Hand = part; pose.HandPosition = part.localPosition; pose.HandRotation = part.localRotation; }
                    }
                poses.Add(actor.Id, pose);
            }
            Diagnostic = "primitive_placeholder_bound";
        }

        public void ApplyState(EncounterClientState state)
        {
            if (state == null || !state.HasSnapshot) return;
            bool firstOrNewLoop = loopId != state.LoopId;
            if (firstOrNewLoop) { RestoreAll(); loopId = state.LoopId; }
            bool nextFall = state.Actors.TryGetValue("luca", out var luca) && luca.Type == "fall";
            if (nextFall != falling)
            {
                falling = nextFall; fallProgress = nextFall && firstOrNewLoop ? 1f : 0f;
                if (poses.TryGetValue("luca", out var pose))
                {
                    externalFall = nextFall && Reviewed != null && Reviewed.TryPresent(pose.Actor, "fall", true);
                    if (!nextFall) { Reviewed?.ResetPresentation(pose.Actor); if (pose.Primitive) RestoreVisual(pose); }
                    else if (!externalFall && !pose.Primitive) Diagnostic = "reviewed_fall_presentation_missing";
                }
            }
            if (state.Recording != recording)
            {
                recording = state.Recording;
                if (poses.TryGetValue("maya", out var pose))
                {
                    externalRecording = recording && Reviewed != null && Reviewed.TryPresent(pose.Actor, "record", false);
                    if (!recording) { Reviewed?.ResetPresentation(pose.Actor); RestoreArms(pose); }
                    else if (!externalRecording && pose.Primitive) ShowPhone(pose);
                    else if (!externalRecording) Diagnostic = "reviewed_recording_presentation_missing";
                }
            }
            ApplyFallPose();
        }

        public void AdvancePresentation(float seconds)
        {
            if (!falling || externalFall || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) return;
            float duration = float.IsNaN(FallSeconds) || float.IsInfinity(FallSeconds) ? .45f : Mathf.Max(.05f, FallSeconds);
            fallProgress = Mathf.Clamp01(fallProgress + seconds / duration);
            ApplyFallPose();
        }

        void ApplyFallPose()
        {
            if (!falling || externalFall || !poses.TryGetValue("luca", out var pose) || !pose.Primitive || !pose.Visual) return;
            float amount = Mathf.SmoothStep(0, 1, fallProgress);
            // Feet-origin primitive: lift slightly so the horizontal body remains above the floor.
            pose.Visual.localRotation = pose.Rotation * Quaternion.Euler(-85f * amount, 0, 0);
            pose.Visual.localPosition = pose.Position + new Vector3(0, .26f * amount, 0);
        }

        void ShowPhone(Pose pose)
        {
            if (!pose.Arm || !pose.Hand) { Diagnostic = "primitive_recording_parts_missing"; return; }
            if (!pose.Phone)
            {
                pose.Phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pose.Phone.name = "Encounter recording phone (placeholder)";
                pose.Phone.transform.SetParent(pose.Visual, false);
                pose.Phone.transform.localPosition = new Vector3(.37f, 1.48f, .50f);
                pose.Phone.transform.localScale = new Vector3(.11f, .20f, .035f);
                pose.Phone.GetComponent<Collider>().enabled = false;
                if (pose.Actor.Mouth)
                {
                    var source = pose.Actor.Mouth.GetComponent<Renderer>();
                    if (source) pose.Phone.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
                }
            }
            pose.Arm.localPosition = new Vector3(.35f, 1.21f, .26f);
            pose.Arm.localRotation = Quaternion.Euler(-65f, 0, -12f);
            pose.Hand.localPosition = new Vector3(.36f, 1.37f, .46f);
            pose.Phone.SetActive(true);
        }

        public void RestoreAll()
        {
            foreach (var pose in poses.Values)
            {
                if (pose.Actor) Reviewed?.ResetPresentation(pose.Actor);
                if (pose.Primitive) { RestoreVisual(pose); RestoreArms(pose); }
            }
            falling = recording = externalFall = externalRecording = false; fallProgress = 0;
        }
        static void RestoreVisual(Pose pose)
        { if (pose.Visual) { pose.Visual.localPosition = pose.Position; pose.Visual.localRotation = pose.Rotation; } }
        static void RestoreArms(Pose pose)
        {
            if (pose.Arm) { pose.Arm.localPosition = pose.ArmPosition; pose.Arm.localRotation = pose.ArmRotation; }
            if (pose.Hand) { pose.Hand.localPosition = pose.HandPosition; pose.Hand.localRotation = pose.HandRotation; }
            if (pose.Phone) pose.Phone.SetActive(false);
        }
        void DestroyPhones()
        {
            foreach (var pose in poses.Values)
                if (pose.Phone) { if (Application.isPlaying) Destroy(pose.Phone); else DestroyImmediate(pose.Phone); }
        }
        void OnDisable()
        {
            if (subscribed) subscribed.StateChanged -= ApplyState;
            subscribed = null; RestoreAll(); loopId = null;
        }
        void OnDestroy() => DestroyPhones();
    }
}
