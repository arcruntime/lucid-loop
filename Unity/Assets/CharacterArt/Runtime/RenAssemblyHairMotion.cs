using System;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    [DefaultExecutionOrder(200)]
    public sealed class RenAssemblyHairMotion : MonoBehaviour
    {
        [Serializable] public sealed class Strand
        {
            public Transform Bone;
            public float LimitDegrees = 3f;
            [Range(0, 1)] public float CapMotion = .3f;
            [NonSerialized] public Quaternion Rest;
            [NonSerialized] public Vector2 Offset, Velocity;
        }
        public Transform Head;
        public Strand[] Strands = Array.Empty<Strand>();
        public bool MotionEnabled = true, CapOn = true;
        public float Stiffness = 50f, Damping = 15f, IdleAmplitude = .16f;
        Quaternion previousHead;
        bool initialized;

        public void Initialize()
        {
            foreach (var s in Strands) if (s.Bone) { s.Rest = s.Bone.localRotation; s.Offset = s.Velocity = Vector2.zero; }
            previousHead = Head ? Head.rotation : transform.rotation;
            initialized = true;
        }
        void OnEnable() { initialized = false; }
        void LateUpdate()
        {
            if (!initialized) Initialize();
            var current = Head ? Head.rotation : transform.rotation;
            var delta = Quaternion.Inverse(previousHead) * current;
            delta.ToAngleAxis(out var angle, out var axis);
            if (angle > 180) angle -= 360;
            var dt = Mathf.Min(Time.deltaTime, .05f);
            var angular = dt > .00001f && float.IsFinite(axis.x) ? Vector3.ClampMagnitude(axis * angle / dt, 180) : Vector3.zero;
            previousHead = current;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 120f)));
            float h = dt / steps;
            for (int i = 0; i < Strands.Length; i++)
            {
                var s = Strands[i]; if (!s.Bone) continue;
                if (!MotionEnabled) { s.Offset = s.Velocity = Vector2.zero; s.Bone.localRotation = s.Rest; continue; }
                float limit = Mathf.Max(0, s.LimitDegrees) * (CapOn ? s.CapMotion : 1f);
                var target = new Vector2(-angular.x, -angular.z) * .025f;
                target += new Vector2(Mathf.Sin(Time.time * 1.7f + i * .8f), Mathf.Sin(Time.time * 1.13f + i)) * IdleAmplitude;
                target = Vector2.ClampMagnitude(target, limit);
                for (int n = 0; n < steps; n++)
                {
                    s.Velocity += (Stiffness * (target - s.Offset) - Damping * s.Velocity) * h;
                    s.Offset += s.Velocity * h;
                    if (s.Offset.sqrMagnitude > limit * limit) { s.Offset = Vector2.ClampMagnitude(s.Offset, limit); s.Velocity *= .5f; }
                }
                s.Bone.localRotation = s.Rest * Quaternion.Euler(s.Offset.x, 0, s.Offset.y);
            }
        }
        void OnDisable()
        {
            if (!initialized) return;
            foreach (var s in Strands) if (s.Bone) s.Bone.localRotation = s.Rest;
        }
    }
}
