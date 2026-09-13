using System;
using System.Collections.Generic;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    /// <summary>Four independent facial channels plus rigid gaze within the authored ellipsoid frames.</summary>
    public sealed class RenCompleteHeadRig : MonoBehaviour
    {
        [Serializable] public sealed class Eye
        {
            public string Side;
            public Transform Rotation;
            public Quaternion RestLocalRotation = Quaternion.identity;
            // Imported local pseudovector axes, including the handedness of the full basis conversion.
            public Vector3 PitchAxis = Vector3.up, YawAxis = Vector3.forward;
        }
        public Eye[] Eyes = Array.Empty<Eye>();
        public float YawRadians = .16199795457112545f;
        public float NegativePitchRadians = -.11693296146237843f;
        public string ContractSha256, SourceSha256, RuntimeRigSha256, RuntimeControllerSha256;
        public GameObject[] HairObjects = Array.Empty<GameObject>();
        public GameObject[] CapObjects = Array.Empty<GameObject>();
        public bool CapVisible = true;
        static readonly string[] Channels = { "mouthSeal", "jawOpen_A", "eyeBlinkL", "eyeBlinkR" };
        static readonly string[] StaleGaze = { "gazeLeft", "gazeRight", "gazeUp", "gazeDown" };
        sealed class Binding { public SkinnedMeshRenderer Renderer; public int[] Channels; public int[] StaleGaze; public int CapOn = -1; }
        [Serializable] public sealed class CapWeight { public string renderer; public float weight; }
        Binding[] bindings;

        void Awake() => Bind();
        void Bind()
        {
            var result = new List<Binding>();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh; if (!mesh) continue;
                var channels = new[] { -1, -1, -1, -1 }; var stale = new List<int>(); var capOn = -1;
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    for (var channel = 0; channel < Channels.Length; channel++)
                        if (RenFaceStudyController.ShapeMatches(name, Channels[channel])) channels[channel] = i;
                    foreach (var old in StaleGaze) if (RenFaceStudyController.ShapeMatches(name, old)) stale.Add(i);
                    if (RenFaceStudyController.ShapeMatches(name, "capOn")) capOn = i;
                }
                renderer.updateWhenOffscreen = true;
                result.Add(new Binding { Renderer = renderer, Channels = channels, StaleGaze = stale.ToArray(), CapOn = capOn });
            }
            bindings = result.ToArray();
            ApplyCapState();
        }

        public void Apply(float seal, float openA, float blinkL, float blinkR, Vector2 gaze)
        {
            if (bindings == null) Bind();
            seal = Mathf.Clamp01(seal); openA = Mathf.Clamp01(openA);
            var sum = seal + openA; if (sum > 1) { seal /= sum; openA /= sum; }
            foreach (var binding in bindings)
            {
                if (!binding.Renderer) continue;
                for (var i = 0; i < 4; i++)
                    if (binding.Channels[i] >= 0)
                        binding.Renderer.SetBlendShapeWeight(binding.Channels[i], 100 * Mathf.Clamp01(i == 0 ? seal : i == 1 ? openA : i == 2 ? blinkL : blinkR));
                // Never allow the old additive iris deformation to compose with the repaired rotation.
                foreach (var index in binding.StaleGaze) binding.Renderer.SetBlendShapeWeight(index, 0);
            }
            gaze.x = Mathf.Clamp(gaze.x, -1, 1); gaze.y = Mathf.Clamp(gaze.y, -1, 1);
            foreach (var eye in Eyes)
            {
                if (!eye.Rotation) continue;
                var yaw = Quaternion.AngleAxis(gaze.x * YawRadians * Mathf.Rad2Deg, eye.YawAxis);
                var pitch = Quaternion.AngleAxis(gaze.y * NegativePitchRadians * Mathf.Rad2Deg, eye.PitchAxis);
                eye.Rotation.localRotation = eye.RestLocalRotation * yaw * pitch;
            }
            ApplyCapState();
        }

        public void SetHairVisible(bool visible)
        {
            foreach (var item in HairObjects) if (item) item.SetActive(visible);
        }
        public void SetCapVisible(bool visible)
        {
            CapVisible = visible;
            if (bindings == null) Bind();
            ApplyCapState();
        }
        void ApplyCapState()
        {
            foreach (var item in CapObjects) if (item) item.SetActive(CapVisible);
            foreach (var binding in bindings)
                if (binding.Renderer && binding.CapOn >= 0)
                    binding.Renderer.SetBlendShapeWeight(binding.CapOn, CapVisible ? 100 : 0);
        }
        public CapWeight[] CaptureCapWeights()
        {
            if (bindings == null) Bind();
            var result = new List<CapWeight>();
            foreach (var binding in bindings)
                if (binding.Renderer && binding.CapOn >= 0)
                    result.Add(new CapWeight { renderer = binding.Renderer.name, weight = binding.Renderer.GetBlendShapeWeight(binding.CapOn) });
            return result.ToArray();
        }
    }
}
