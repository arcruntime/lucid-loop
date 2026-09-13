using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    [Serializable] public sealed class RenHReferenceCapHairBinding
    {
        public string rendererPath, shape = "capOn";
        public float offWeight, onWeight = 100;
    }
    [Serializable] public sealed class RenHReferenceCapBinding
    {
        public string rigArchetype = "female";
        public string mode, animatorPath, headPath, socketPath, reviewNote;
        public RenHReferenceCapHairBinding[] hairFits = Array.Empty<RenHReferenceCapHairBinding>();
    }

    /// <summary>A separate accessory on a reviewed head socket. Only visibility and explicit hair-fit morphs are owned here.</summary>
    public sealed class RenHReferenceCapAttachment : MonoBehaviour
    {
        public const string SharedFemaleMode = "shared-female-humanoid";
        public const string TemporaryBustMode = "temporary-bust-socket";
        public RenHReferenceCapBinding Binding;
        public Transform CapRoot;
        public Avatar ExpectedSharedFemaleAvatar;
        public bool DefaultVisible = true;
        public string SourceSha256;

        sealed class HairTarget { public SkinnedMeshRenderer Renderer; public int Index; public float Off, On; }
        readonly List<HairTarget> hair = new List<HairTarget>();
        bool bound;
        public bool IsBound => bound;
        public bool CapVisible { get; private set; }
        public bool HairFitted { get; private set; }
        public string Status => !bound ? "Cap socket unavailable" : Binding.mode == SharedFemaleMode
            ? "Shared female rig / Head / separate cap" : "Temporary bust socket / female rig calibration pending";

        public static void ValidateDefinition(RenHReferenceCapBinding binding)
        {
            if (binding == null || binding.rigArchetype != "female" ||
                (binding.mode != SharedFemaleMode && binding.mode != TemporaryBustMode) ||
                string.IsNullOrWhiteSpace(binding.reviewNote) || string.IsNullOrWhiteSpace(binding.headPath) ||
                string.IsNullOrWhiteSpace(binding.socketPath) || binding.hairFits == null || binding.hairFits.Length == 0)
                throw new InvalidOperationException("Ren requires a reviewed female Head/CapSocket contract and explicit separate hair-fit bindings.");
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fit in binding.hairFits)
            {
                if (fit == null || string.IsNullOrWhiteSpace(fit.rendererPath) || string.IsNullOrWhiteSpace(fit.shape) ||
                    !Finite(fit.offWeight) || !Finite(fit.onWeight) || fit.offWeight < 0 || fit.offWeight > 100 ||
                    fit.onWeight < 0 || fit.onWeight > 100 || fit.offWeight == fit.onWeight ||
                    !targets.Add(fit.rendererPath + "\n" + fit.shape))
                    throw new InvalidOperationException("Invalid, nonfinite or duplicate cap-on hair binding.");
            }
        }

        public static void ValidateHierarchy(Transform modelRoot, Renderer headRenderer, Transform capRoot, Avatar expectedAvatar,
            RenHReferenceCapBinding binding, RenHReferenceMorphBinding[] faceBindings)
        {
            ValidateDefinition(binding);
            if (!modelRoot || !headRenderer || !capRoot || !capRoot.IsChildOf(modelRoot))
                throw new InvalidOperationException("Separate cap and actual head must belong to the reviewed H candidate.");
            var head = modelRoot.Find(binding.headPath);
            var socket = modelRoot.Find(binding.socketPath);
            if (!head || !socket || head == socket || !socket.IsChildOf(head) || socket.name != "CapSocket" || capRoot.parent != socket)
                throw new InvalidOperationException("The separate cap must be a direct child of the reviewed Head descendant named CapSocket.");
            if (headRenderer.transform.IsChildOf(capRoot) || capRoot == headRenderer.transform || capRoot.GetComponentsInChildren<Animator>(true).Length > 0)
                throw new InvalidOperationException("The cap cannot contain the face or an independent rig.");
            if (binding.mode == SharedFemaleMode)
            {
                var rigNode = string.IsNullOrEmpty(binding.animatorPath) ? modelRoot : modelRoot.Find(binding.animatorPath);
                var animator = rigNode ? rigNode.GetComponent<Animator>() : null;
                if (!animator || !expectedAvatar || animator.avatar != expectedAvatar || !expectedAvatar.isValid || !expectedAvatar.isHuman ||
                    animator.GetBoneTransform(HumanBodyBones.Head) != head)
                    throw new InvalidOperationException("Cap attachment must resolve the exact shared female Avatar's Humanoid Head bone.");
            }
            else if (expectedAvatar)
                throw new InvalidOperationException("A temporary bust socket must not claim a bound shared female Avatar.");
            var capRenderers = capRoot.GetComponentsInChildren<Renderer>(true);
            if (capRenderers.Length == 0 || capRenderers.Any(item => !(item is MeshRenderer) || !MeshOf(item)))
                throw new InvalidOperationException("Expected a separate rigid cap mesh export, without a cap skin or armature.");
            var capMeshes = new HashSet<Mesh>(capRenderers.Select(MeshOf));
            if (modelRoot.GetComponentsInChildren<Renderer>(true).Any(item => !item.transform.IsChildOf(capRoot) && capMeshes.Contains(MeshOf(item))))
                throw new InvalidOperationException("A cap mesh must not be reused by the head or hair renderer.");
            var faceTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var face in faceBindings ?? Array.Empty<RenHReferenceMorphBinding>())
            {
                var node = modelRoot.Find(face.rendererPath); var renderer = node ? node.GetComponent<SkinnedMeshRenderer>() : null;
                if (renderer && renderer.sharedMesh)
                    faceTargets.Add(renderer.GetInstanceID() + ":" + renderer.sharedMesh.GetBlendShapeIndex(face.shape));
            }
            var hairTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fit in binding.hairFits)
            {
                var node = modelRoot.Find(fit.rendererPath); var renderer = node ? node.GetComponent<SkinnedMeshRenderer>() : null;
                var index = renderer && renderer.sharedMesh ? renderer.sharedMesh.GetBlendShapeIndex(fit.shape) : -1;
                if (!renderer || index < 0 || renderer == headRenderer || renderer.transform.IsChildOf(capRoot))
                    throw new InvalidOperationException("Cap fitting requires a separate hair renderer and real shape: " + fit.rendererPath + "/" + fit.shape);
                var key = renderer.GetInstanceID() + ":" + index;
                if (faceTargets.Contains(key) || !hairTargets.Add(key))
                    throw new InvalidOperationException("Cap fitting must not overlap a facial or duplicate hair morph binding.");
            }
        }

        public void Bind(Transform modelRoot, Renderer headRenderer, RenHReferenceMorphBinding[] faceBindings)
        {
            // Resolve everything before changing any source state. Rebinding preserves the user's current cap/hair state.
            ValidateHierarchy(modelRoot, headRenderer, CapRoot, ExpectedSharedFemaleAvatar, Binding, faceBindings);
            hair.Clear();
            foreach (var fit in Binding.hairFits)
            {
                var renderer = modelRoot.Find(fit.rendererPath).GetComponent<SkinnedMeshRenderer>();
                hair.Add(new HairTarget { Renderer = renderer, Index = renderer.sharedMesh.GetBlendShapeIndex(fit.shape), Off = fit.offWeight, On = fit.onWeight });
            }
            var visible = bound ? CapVisible : DefaultVisible;
            var fitted = bound ? HairFitted : DefaultVisible;
            bound = true; SetVisibilityOnly(visible); SetHairFitted(fitted);
        }
        public void SetCapVisible(bool visible)
        {
            RequireBound(); SetVisibilityOnly(visible); SetHairFitted(visible);
        }
        public void SetVisibilityOnly(bool visible)
        {
            RequireBound(); CapRoot.gameObject.SetActive(visible); CapVisible = visible;
        }
        public void SetHairFitted(bool fitted)
        {
            RequireBound();
            foreach (var fit in hair) fit.Renderer.SetBlendShapeWeight(fit.Index, fitted ? fit.On : fit.Off);
            HairFitted = fitted;
        }
        public void ResetCap() { SetCapVisible(DefaultVisible); }
        void RequireBound() { if (!bound) throw new InvalidOperationException("The reviewed cap socket has not been bound."); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static Mesh MeshOf(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
    }
}
