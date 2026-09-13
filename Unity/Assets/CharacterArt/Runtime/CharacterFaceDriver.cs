using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LucidLoop.CharacterArt
{
    [DisallowMultipleComponent]
    public sealed class CharacterFaceDriver : MonoBehaviour
    {
        static readonly string[] RequiredFaceShapes =
        {
            "eyeBlinkLeft", "eyeBlinkRight", "eyeWideLeft", "eyeWideRight",
            "browInnerUp", "browDownLeft", "browDownRight",
            "mouthSmileLeft", "mouthSmileRight", "mouthFrownLeft", "mouthFrownRight",
            "jawOpen", "mouthClose",
            "eyeLookDownLeft", "eyeLookInLeft", "eyeLookOutLeft", "eyeLookUpLeft",
            "eyeLookDownRight", "eyeLookInRight", "eyeLookOutRight", "eyeLookUpRight"
        };

        [SerializeField] CharacterProfile profile;
        [SerializeField] bool automaticBlink = true;
        [SerializeField] bool idleEnabled = true;

        readonly Dictionary<string, List<ShapeBinding>> bindings =
            new Dictionary<string, List<ShapeBinding>>(StringComparer.Ordinal);
        readonly Dictionary<string, float> expressionWeights =
            new Dictionary<string, float>(StringComparer.Ordinal);
        readonly Dictionary<string, float> speechWeights =
            new Dictionary<string, float>(StringComparer.Ordinal);
        readonly HashSet<string> writtenShapes = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> missingShapeNames = new List<string>();
        readonly Dictionary<string, TransformRestPose> accessoryRestPoses =
            new Dictionary<string, TransformRestPose>(StringComparer.Ordinal);

        BlinkTimingState blink;
        BlinkFrame blinkFrame;
        ExpressionPreset activeExpression;
        float expressionIntensity = 1f;
        Transform headBone;
        Transform leftEyeBone;
        Transform rightEyeBone;
        Quaternion headAnimationRotation;
        Quaternion leftEyeAnimationRotation;
        Quaternion rightEyeAnimationRotation;
        Quaternion headAppliedRotation;
        Quaternion leftEyeAppliedRotation;
        Quaternion rightEyeAppliedRotation;
        bool headOffsetApplied;
        bool leftEyeOffsetApplied;
        bool rightEyeOffsetApplied;
        PlayableGraph motionGraph;
        AnimationLayerMixerPlayable motionMixer;
        AnimationClipPlayable idlePlayable;
        AnimationClipPlayable bodyPlayable;
        AnimationClipPlayable gesturePlayable;
        AvatarMask bodyMask;
        AvatarMask gestureMask;
        Animator motionAnimator;
        bool savedApplyRootMotion;
        bool bodyMotionLooping;
        float gestureWeight;
        float gestureTargetWeight;
        const float GestureFadeSeconds = .18f;
        SpeechReviewTimeline speechTimeline;
        string lastSpeechDiagnostic = string.Empty;

        public CharacterProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                if (isActiveAndEnabled) Bind();
            }
        }

        public IReadOnlyList<string> MissingShapeNames => missingShapeNames;
        public ExpressionPreset ActiveExpression => activeExpression;
        public float ExpressionIntensity => expressionIntensity;
        public string LastSpeechDiagnostic => lastSpeechDiagnostic;
        public bool IsBodyMotionPlaying => bodyPlayable.IsValid();
        public AnimationClip ActiveBodyMotionClip => bodyPlayable.IsValid() ? bodyPlayable.GetAnimationClip() : null;
        public bool BodyMotionLooping => bodyPlayable.IsValid() && bodyMotionLooping;
        public double BodyMotionTime => bodyPlayable.IsValid() ? bodyPlayable.GetTime() : 0d;

        /// <summary>
        /// Plays an in-place clip on the existing motion graph, beneath expression gestures.
        /// Navigation owns the character transform. Clips must target the shared male/female rig;
        /// this API does not retarget, translate the actor, or install an Animator controller.
        /// Requires an idle clip to return to. A one-shot returns to idle; a loop continues
        /// until replaced or stopped. Replacement/stop is immediate, without a crossfade.
        /// Invalid input leaves the current motion unchanged. Bind/disable cancels motion.
        /// </summary>
        public bool PlayBodyMotion(AnimationClip clip, bool loop = false)
        {
            if (!clip || clip.legacy || clip.length <= 0f || !isActiveAndEnabled ||
                !Application.isPlaying || !motionGraph.IsValid() || !idlePlayable.IsValid() ||
                (clip.isHumanMotion && !motionAnimator.isHuman)) return false;
            StopBodyMotion();
            bodyPlayable = AnimationClipPlayable.Create(motionGraph, clip);
            bodyPlayable.SetApplyFootIK(false);
            // Lifetime is controlled here, independently of the clip's import loop setting.
            bodyPlayable.SetDuration(double.PositiveInfinity);
            bodyPlayable.SetTime(0d);
            bodyPlayable.SetSpeed(0d);
            bodyMotionLooping = loop;
            motionGraph.Connect(bodyPlayable, 0, motionMixer, 1);
            motionMixer.SetInputWeight(1, 1f);
            return true;
        }

        /// <summary>Immediately releases the body layer to idle without resetting expressions or speech.</summary>
        public void StopBodyMotion()
        {
            if (bodyPlayable.IsValid() && motionGraph.IsValid())
            {
                motionGraph.Disconnect(motionMixer, 1);
                motionGraph.DestroyPlayable(bodyPlayable);
            }
            bodyPlayable = default;
            bodyMotionLooping = false;
            if (motionMixer.IsValid()) motionMixer.SetInputWeight(1, 0f);
        }

        public bool AutomaticBlink
        {
            get => automaticBlink;
            set
            {
                automaticBlink = value;
                EnsureBlink();
                blink.AutomaticEnabled = value;
            }
        }

        public bool IdleEnabled
        {
            get => idleEnabled;
            set
            {
                idleEnabled = value;
                if (idlePlayable.IsValid()) idlePlayable.SetSpeed(value ? 1d : 0d);
            }
        }

        void Awake() => Bind();

        void OnEnable()
        {
            EnsureBlink();
            blink.AutomaticEnabled = automaticBlink;
            StartMotion();
        }

        void OnDisable()
        {
            RestorePostAnimationOffsets();
            StopMotion();
        }

        void OnDestroy()
        {
            StopMotion();
        }

        void Update()
        {
            EnsureBlink();
            if (speechTimeline != null) ApplyTimelineFrame(speechTimeline.Advance(Time.deltaTime));
            if (idleEnabled && idlePlayable.IsValid() && profile && profile.IdleClip &&
                idlePlayable.GetTime() >= profile.IdleClip.length)
                idlePlayable.SetTime(IdlePlaybackMath.WrapTime(idlePlayable.GetTime(), profile.IdleClip.length));
            UpdateGesture(Time.deltaTime);
            if (bodyPlayable.IsValid())
            {
                // Sample a bounded time before animation evaluation so imported looping flags
                // cannot replay the first frame of a one-shot during its final update.
                var nextTime = bodyPlayable.GetTime() + Time.deltaTime;
                var length = bodyPlayable.GetAnimationClip().length;
                if (!bodyMotionLooping && nextTime >= length) StopBodyMotion();
                else bodyPlayable.SetTime(IdlePlaybackMath.WrapTime(nextTime, length));
            }
            blinkFrame = blink.Advance(Time.deltaTime);
            ApplyPose();
        }

        void LateUpdate() => ApplyPostAnimationOffsets();

        public void Bind()
        {
            RestorePostAnimationOffsets();
            bindings.Clear();
            writtenShapes.Clear();
            missingShapeNames.Clear();
            expressionWeights.Clear();
            speechWeights.Clear();
            speechTimeline = null;
            lastSpeechDiagnostic = string.Empty;
            accessoryRestPoses.Clear();
            activeExpression = null;

            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (!mesh) continue;
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var shapeName = mesh.GetBlendShapeName(i);
                    if (!bindings.TryGetValue(shapeName, out var shapeBindings))
                    {
                        shapeBindings = new List<ShapeBinding>();
                        bindings.Add(shapeName, shapeBindings);
                    }
                    shapeBindings.Add(new ShapeBinding(renderer, i));
                }
            }

            var required = new HashSet<string>(RequiredFaceShapes, StringComparer.Ordinal);
            foreach (var label in FacePoseComposer.SpeechLabels) required.Add("viseme_" + label);
            foreach (var shapeName in FacePoseComposer.ReservedSpeechShapeNames) required.Add(shapeName);
            if (profile && profile.Expressions != null)
            {
                foreach (var expression in profile.Expressions)
                {
                    if (expression?.BlendshapeWeights != null)
                        foreach (var weight in expression.BlendshapeWeights)
                            if (!string.IsNullOrWhiteSpace(weight.ShapeName)) required.Add(weight.ShapeName);
                    CacheAccessoryRestPoses(expression);
                }
            }

            foreach (var shapeName in required)
                if (!bindings.ContainsKey(shapeName)) missingShapeNames.Add(shapeName);
            missingShapeNames.Sort(StringComparer.Ordinal);

            BindPoseBones();
            StartMotion();
        }

        public bool SetExpression(string expressionId, float normalizedIntensity = 1f)
        {
            activeExpression = null;
            expressionWeights.Clear();
            expressionIntensity = Mathf.Clamp01(normalizedIntensity);
            if (!profile || profile.Expressions == null || string.IsNullOrEmpty(expressionId))
            {
                TransitionGesture(null);
                ApplyPose();
                return false;
            }

            foreach (var expression in profile.Expressions)
            {
                if (expression == null || !string.Equals(expression.Id, expressionId, StringComparison.Ordinal)) continue;
                activeExpression = expression;
                if (expression.BlendshapeWeights != null)
                    foreach (var shape in expression.BlendshapeWeights)
                        if (!string.IsNullOrWhiteSpace(shape.ShapeName))
                            expressionWeights[shape.ShapeName] = shape.Weight;
                TransitionGesture(expression.GestureClip);
                ApplyPose();
                return true;
            }

            TransitionGesture(null);
            ApplyPose();
            return false;
        }

        public void SetExpressionIntensity(float normalizedIntensity)
        {
            expressionIntensity = Mathf.Clamp01(normalizedIntensity);
            gestureTargetWeight = gesturePlayable.IsValid() ? expressionIntensity : 0f;
            ApplyPose();
        }

        public bool SetViseme(string label, float normalizedWeight)
        {
            speechTimeline = null;
            if (!FacePoseComposer.IsValidSpeechLabel(label))
            {
                SetSpeechDiagnostic("UNKNOWN_LEGACY_VISEME: '" + label + "' is not in the 15-target adapter.");
                ApplyPose();
                return false;
            }
            speechWeights.Clear();
            lastSpeechDiagnostic = string.Empty;
            if (label == "sil" || normalizedWeight <= 0f)
            {
                ApplyPose();
                return true;
            }
            speechWeights["viseme_" + label] = Mathf.Clamp01(normalizedWeight) * 100f;
            ApplyPose();
            return true;
        }

        public void SetVisemes(IReadOnlyList<VisemeWeight> normalizedWeights)
        {
            speechTimeline = null;
            speechWeights.Clear();
            lastSpeechDiagnostic = string.Empty;
            if (normalizedWeights != null)
            {
                foreach (var weight in normalizedWeights)
                {
                    if (!FacePoseComposer.IsValidSpeechLabel(weight.Label) || weight.Label == "sil") continue;
                    var normalized = Mathf.Clamp01(weight.Weight);
                    if (normalized > 0f) speechWeights["viseme_" + weight.Label] = normalized * 100f;
                }
            }
            ApplyPose();
        }

        /// <summary>
        /// Replaces the complete speech snapshot, with a language per authored pose.
        /// Invalid frames or missing active targets clear speech and report a diagnostic.
        /// Call on the main thread at the playback clock; this does not schedule audio.
        /// </summary>
        public bool ApplySpeechFrame(IReadOnlyList<SpeechPoseWeight> frame)
        {
            speechTimeline = null;
            var valid = SpeechFrameResolver.TryResolve(frame, profile ? profile.SpeechPoses : null,
                speechWeights, out var diagnostic);
            if (valid)
                foreach (var pair in speechWeights)
                    if (!bindings.ContainsKey(pair.Key))
                    {
                        valid = false;
                        diagnostic = "SPEECH_TARGET_MISSING: " + pair.Key + "; complete snapshot rejected, no fallback applied.";
                        break;
                    }
            if (!valid)
            {
                speechWeights.Clear();
                SetSpeechDiagnostic(diagnostic);
            }
            else lastSpeechDiagnostic = string.Empty;
            ApplyPose();
            return valid;
        }

        public bool SetSpeechPose(SpeechLanguage language, string poseId, float normalizedWeight = 1f)
        {
            speechTimeline = null;
            if (!TryGetSpeechPose(language, poseId, out var pose)) return false;
            speechWeights.Clear();
            lastSpeechDiagnostic = string.Empty;
            if (pose.Id == "sil" || normalizedWeight <= 0f)
            {
                ApplyPose();
                return true;
            }

            AddPoseWeights(pose, Mathf.Clamp01(normalizedWeight));
            var missing = FindMissingPoseTargets(pose);
            if (missing.Count > 0)
            {
                SetSpeechDiagnostic("SPEECH_TARGET_MISSING: " + language + "/" + poseId + " requires " +
                                    string.Join(", ", missing) + "; no English fallback applied.");
                ApplyPose();
                return false;
            }
            ApplyPose();
            return true;
        }

        public bool StartSpeechReviewSequence(SpeechLanguage language, string sequenceId)
        {
            if (!profile || profile.SpeechReviewSequences == null)
            {
                SetSpeechDiagnostic("SPEECH_SEQUENCE_UNAVAILABLE: no speech profile is assigned.");
                return false;
            }
            SpeechReviewSequence sequence = null;
            foreach (var candidate in profile.SpeechReviewSequences)
                if (candidate != null && candidate.Language == language &&
                    string.Equals(candidate.Id, sequenceId, StringComparison.Ordinal))
                {
                    sequence = candidate;
                    break;
                }
            if (sequence == null)
            {
                SetSpeechDiagnostic("UNKNOWN_SPEECH_SEQUENCE: " + language + "/" + sequenceId +
                                    " is not configured; no fallback applied.");
                return false;
            }

            var missingTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in sequence.Steps)
            {
                if (!TryGetSpeechPose(step.Language, step.PoseId, out var pose)) return false;
                foreach (var shapeName in FindMissingPoseTargets(pose)) missingTargets.Add(shapeName);
            }
            if (missingTargets.Count > 0)
            {
                var sorted = new List<string>(missingTargets);
                sorted.Sort(StringComparer.Ordinal);
                speechTimeline = null;
                speechWeights.Clear();
                SetSpeechDiagnostic("SPEECH_TARGET_MISSING: " + language + "/" + sequenceId + " requires " +
                                    string.Join(", ", sorted) + "; no English fallback applied.");
                ApplyPose();
                return false;
            }
            speechTimeline = new SpeechReviewTimeline(sequence);
            lastSpeechDiagnostic = string.Empty;
            ApplyTimelineFrame(speechTimeline.Advance(0f));
            ApplyPose();
            return true;
        }

        public void StopSpeechReviewSequence(bool returnToSilence = true)
        {
            speechTimeline = null;
            if (returnToSilence) speechWeights.Clear();
            ApplyPose();
        }

        public void ResetSpeech()
        {
            speechTimeline = null;
            speechWeights.Clear();
            lastSpeechDiagnostic = string.Empty;
            ApplyPose();
        }

        public void ManualBlink(BlinkEyes eyes = BlinkEyes.Both)
        {
            EnsureBlink();
            blink.Trigger(eyes);
        }

        public string BuildBindingDiagnostic()
        {
            var text = new StringBuilder();
            if (missingShapeNames.Count == 0) text.AppendLine("All required facial shapes are bound.");
            else
            {
                text.AppendLine("Missing facial shapes (not delivered):");
                foreach (var shapeName in missingShapeNames) text.Append("• ").Append(shapeName).Append('\n');
            }
            if (!string.IsNullOrEmpty(lastSpeechDiagnostic)) text.AppendLine(lastSpeechDiagnostic);
            return text.ToString().TrimEnd();
        }

        void ApplyPose()
        {
            var pose = FacePoseComposer.ComposeResolvedSpeech(
                expressionWeights,
                expressionIntensity,
                speechWeights,
                speechWeights.Count > 0,
                blinkFrame);
            var gaze = activeExpression != null ? activeExpression.Gaze : Vector2.zero;
            GazeBlendshapeComposer.AddSynthesizedGaze(
                pose, expressionWeights, expressionIntensity, gaze, !leftEyeBone, !rightEyeBone);

            foreach (var shapeName in writtenShapes)
                if (bindings.TryGetValue(shapeName, out var oldBindings))
                    foreach (var binding in oldBindings) binding.Renderer.SetBlendShapeWeight(binding.Index, 0f);
            writtenShapes.Clear();

            foreach (var pair in pose)
            {
                if (!bindings.TryGetValue(pair.Key, out var shapeBindings)) continue;
                foreach (var binding in shapeBindings)
                    binding.Renderer.SetBlendShapeWeight(binding.Index, pair.Value);
                writtenShapes.Add(pair.Key);
            }
        }

        bool TryGetSpeechPose(SpeechLanguage language, string poseId, out SpeechPoseProfile pose)
        {
            if (!profile)
            {
                pose = null;
                SetSpeechDiagnostic("SPEECH_PROFILE_UNAVAILABLE: no character profile is assigned.");
                return false;
            }
            if (!BilingualSpeechLibrary.TryFindPose(profile.SpeechPoses, language, poseId,
                    out pose, out var diagnostic))
            {
                SetSpeechDiagnostic(diagnostic);
                return false;
            }
            return true;
        }

        void ApplyTimelineFrame(SpeechTimelineFrame frame)
        {
            if (!TryGetSpeechPose(frame.FromLanguage, frame.FromPoseId, out var from) ||
                !TryGetSpeechPose(frame.ToLanguage, frame.ToPoseId, out var to))
            {
                speechTimeline = null;
                return;
            }
            speechWeights.Clear();
            AddPoseWeights(from, 1f - frame.Blend);
            AddPoseWeights(to, frame.Blend);
        }

        void AddPoseWeights(SpeechPoseProfile pose, float strength)
        {
            if (pose?.Targets == null) return;
            foreach (var target in pose.Targets)
            {
                if (!FacePoseComposer.IsValidSpeechShapeName(target.ShapeName)) continue;
                var value = Mathf.Clamp01(target.Weight) * Mathf.Clamp01(strength) * 100f;
                if (speechWeights.TryGetValue(target.ShapeName, out var existing)) value += existing;
                speechWeights[target.ShapeName] = Mathf.Clamp(value, 0f, 100f);
            }
        }

        List<string> FindMissingPoseTargets(SpeechPoseProfile pose)
        {
            var missing = new List<string>();
            if (pose?.Targets == null) return missing;
            foreach (var target in pose.Targets)
                if (!bindings.ContainsKey(target.ShapeName) && !missing.Contains(target.ShapeName)) missing.Add(target.ShapeName);
            return missing;
        }

        void SetSpeechDiagnostic(string diagnostic)
        {
            lastSpeechDiagnostic = diagnostic;
            Debug.LogWarning(diagnostic, this);
        }

        void ApplyPostAnimationOffsets()
        {
            foreach (var pair in accessoryRestPoses)
            {
                var target = transform.Find(pair.Key);
                if (target) pair.Value.Apply(target);
            }

            var strength = expressionIntensity;
            var headEuler = activeExpression != null ? activeExpression.HeadEuler * strength : Vector3.zero;
            var gaze = activeExpression != null ? activeExpression.Gaze * strength : Vector2.zero;
            ApplyOffset(headBone, Quaternion.Euler(headEuler), ref headAnimationRotation,
                ref headAppliedRotation, ref headOffsetApplied);
            ApplyOffset(leftEyeBone, Quaternion.Euler(-gaze.y, gaze.x, 0f), ref leftEyeAnimationRotation,
                ref leftEyeAppliedRotation, ref leftEyeOffsetApplied);
            ApplyOffset(rightEyeBone, Quaternion.Euler(-gaze.y, gaze.x, 0f), ref rightEyeAnimationRotation,
                ref rightEyeAppliedRotation, ref rightEyeOffsetApplied);

            if (activeExpression?.AccessoryPoses == null) return;
            foreach (var accessory in activeExpression.AccessoryPoses)
            {
                if (accessory == null || string.IsNullOrWhiteSpace(accessory.TransformPath)) continue;
                var target = transform.Find(accessory.TransformPath);
                if (!target) continue;
                target.gameObject.SetActive(accessory.Active);
                target.localPosition = accessory.LocalPosition;
                target.localRotation = Quaternion.Euler(accessory.LocalEuler);
                target.localScale = accessory.LocalScale;
            }
        }

        void CacheAccessoryRestPoses(ExpressionPreset expression)
        {
            if (expression?.AccessoryPoses == null) return;
            foreach (var accessory in expression.AccessoryPoses)
            {
                if (accessory == null || string.IsNullOrWhiteSpace(accessory.TransformPath) ||
                    accessoryRestPoses.ContainsKey(accessory.TransformPath)) continue;
                var target = transform.Find(accessory.TransformPath);
                if (target) accessoryRestPoses.Add(accessory.TransformPath, new TransformRestPose(target));
            }
        }

        void BindPoseBones()
        {
            headBone = FindProfileTransform(profile?.RestPose?.HeadBonePath);
            leftEyeBone = FindProfileTransform(profile?.RestPose?.LeftEyeBonePath);
            rightEyeBone = FindProfileTransform(profile?.RestPose?.RightEyeBonePath);
            headOffsetApplied = leftEyeOffsetApplied = rightEyeOffsetApplied = false;
        }

        Transform FindProfileTransform(string path) => string.IsNullOrWhiteSpace(path) ? null : transform.Find(path);

        void EnsureBlink()
        {
            if (blink != null) return;
            var seed = profile && !string.IsNullOrEmpty(profile.Id) ? StableHash(profile.Id) : 1831;
            var random = new System.Random(seed);
            blink = new BlinkTimingState(() => 2.8f + (float)random.NextDouble() * 3.2f)
            {
                AutomaticEnabled = automaticBlink
            };
        }

        void StartMotion()
        {
            StopMotion();
            if (!Application.isPlaying || !isActiveAndEnabled || !profile) return;
            var animator = GetComponentInChildren<Animator>();
            if (!animator) return;
            // One animation owner: callers must remove external Animator controllers before binding.
            if (animator.runtimeAnimatorController)
            {
                Debug.LogWarning("CHARACTER_MOTION_CONTROLLER_CONFLICT: remove the Animator controller before binding CharacterFaceDriver.", this);
                return;
            }
            motionAnimator = animator;
            savedApplyRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = false;

            motionGraph = PlayableGraph.Create(name + " Character Motion");
            motionGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            motionMixer = AnimationLayerMixerPlayable.Create(motionGraph, 3);
            if (profile.IdleClip)
            {
                idlePlayable = AnimationClipPlayable.Create(motionGraph, profile.IdleClip);
                idlePlayable.SetApplyFootIK(false);
                idlePlayable.SetDuration(profile.IdleClip.length);
                idlePlayable.SetSpeed(idleEnabled ? 1d : 0d);
                motionGraph.Connect(idlePlayable, 0, motionMixer, 0);
                motionMixer.SetInputWeight(0, 1f);
            }
            gestureMask = BuildGestureMask(animator);
            bodyMask = new AvatarMask();
            bodyMask.AddTransformPath(animator.transform, true);
            for (var index = 0; index < bodyMask.transformCount; index++)
                bodyMask.SetTransformActive(index, !string.IsNullOrEmpty(bodyMask.GetTransformPath(index)));
            bodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            motionMixer.SetLayerMaskFromAvatarMask(1, bodyMask);
            motionMixer.SetInputWeight(1, 0f);
            motionMixer.SetLayerMaskFromAvatarMask(2, gestureMask);
            motionMixer.SetInputWeight(2, 0f);
            var output = AnimationPlayableOutput.Create(motionGraph, "Character Motion", animator);
            output.SetSourcePlayable(motionMixer);
            motionGraph.Play();
            if (activeExpression?.GestureClip) TransitionGesture(activeExpression.GestureClip);
        }

        void StopMotion()
        {
            StopBodyMotion();
            if (motionGraph.IsValid()) motionGraph.Destroy();
            if (motionAnimator) motionAnimator.applyRootMotion = savedApplyRootMotion;
            motionAnimator = null;
            if (bodyMask) Destroy(bodyMask);
            bodyMask = null;
            if (gestureMask) Destroy(gestureMask);
            gestureMask = null;
            idlePlayable = default;
            gesturePlayable = default;
            motionMixer = default;
            gestureWeight = gestureTargetWeight = 0f;
        }

        void TransitionGesture(AnimationClip clip)
        {
            if (!motionGraph.IsValid()) return;
            if (gesturePlayable.IsValid())
            {
                motionGraph.Disconnect(motionMixer, 2);
                motionGraph.DestroyPlayable(gesturePlayable);
                gesturePlayable = default;
            }
            gestureWeight = 0f;
            gestureTargetWeight = 0f;
            motionMixer.SetInputWeight(2, 0f);
            if (!clip) return;
            gesturePlayable = AnimationClipPlayable.Create(motionGraph, clip);
            gesturePlayable.SetApplyFootIK(false);
            gesturePlayable.SetDuration(clip.length);
            gesturePlayable.SetTime(0d);
            gesturePlayable.SetSpeed(1d);
            motionGraph.Connect(gesturePlayable, 0, motionMixer, 2);
            gestureTargetWeight = expressionIntensity;
        }

        void UpdateGesture(float deltaTime)
        {
            if (!gesturePlayable.IsValid() || !motionMixer.IsValid()) return;
            gestureWeight = Mathf.MoveTowards(gestureWeight, gestureTargetWeight,
                deltaTime / GestureFadeSeconds);
            motionMixer.SetInputWeight(2, gestureWeight);
            if (gesturePlayable.GetTime() >= gesturePlayable.GetAnimationClip().length)
            {
                gesturePlayable.SetTime(gesturePlayable.GetAnimationClip().length);
                gesturePlayable.SetSpeed(0d);
            }
        }

        static AvatarMask BuildGestureMask(Animator animator)
        {
            var mask = new AvatarMask();
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.AddTransformPath(animator.transform, true);
            for (var index = 0; index < mask.transformCount; index++)
                mask.SetTransformActive(index, IsGestureOwnedTransformPath(mask.GetTransformPath(index)));
            return mask;
        }

        public static bool IsGestureOwnedTransformPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var normalized = path.Replace('\\', '/');
            var finalSegment = normalized.Substring(normalized.LastIndexOf('/') + 1);
            if (string.Equals(finalSegment, "Armature", StringComparison.OrdinalIgnoreCase)) return false;
            if (ContainsSegment(normalized, "Head") || ContainsSegment(normalized, "Neck") ||
                ContainsSegment(normalized, "Eye") || ContainsSegment(normalized, "Jaw") ||
                ContainsSegment(normalized, "UpLeg") || ContainsSegment(normalized, "Leg") ||
                ContainsSegment(normalized, "Foot") || ContainsSegment(normalized, "Toe") ||
                normalized.EndsWith("/Hips", StringComparison.OrdinalIgnoreCase)) return false;
            return ContainsSegment(normalized, "Spine") ||
                   ContainsSegment(normalized, "Shoulder") ||
                   ContainsArmSegment(normalized) ||
                   ContainsSegment(normalized, "Hand") ||
                   normalized.IndexOf("Phone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("Accessory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ContainsSegment(string path, string segment)
        {
            var pieces = path.Split('/');
            foreach (var piece in pieces)
                if (piece.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool ContainsArmSegment(string path)
        {
            var pieces = path.Split('/');
            foreach (var piece in pieces)
                if (!string.Equals(piece, "Armature", StringComparison.OrdinalIgnoreCase) &&
                    piece.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static void ApplyOffset(Transform target, Quaternion offset, ref Quaternion animationRotation,
            ref Quaternion appliedRotation, ref bool hasApplied)
        {
            if (!target) return;
            var current = target.localRotation;
            if (!hasApplied || Quaternion.Angle(current, appliedRotation) > .01f) animationRotation = current;
            target.localRotation = animationRotation * offset;
            appliedRotation = target.localRotation;
            hasApplied = true;
        }

        void RestorePostAnimationOffsets()
        {
            RestoreOffset(headBone, headAnimationRotation, headAppliedRotation, ref headOffsetApplied);
            RestoreOffset(leftEyeBone, leftEyeAnimationRotation, leftEyeAppliedRotation, ref leftEyeOffsetApplied);
            RestoreOffset(rightEyeBone, rightEyeAnimationRotation, rightEyeAppliedRotation, ref rightEyeOffsetApplied);
        }

        static void RestoreOffset(Transform target, Quaternion animationRotation,
            Quaternion appliedRotation, ref bool hasApplied)
        {
            if (target && hasApplied && Quaternion.Angle(target.localRotation, appliedRotation) <= .01f)
                target.localRotation = animationRotation;
            hasApplied = false;
        }

        static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value) hash = hash * 31 + character;
                return hash;
            }
        }

        readonly struct ShapeBinding
        {
            public readonly SkinnedMeshRenderer Renderer;
            public readonly int Index;

            public ShapeBinding(SkinnedMeshRenderer renderer, int index)
            {
                Renderer = renderer;
                Index = index;
            }
        }

        readonly struct TransformRestPose
        {
            readonly bool active;
            readonly Vector3 position;
            readonly Quaternion rotation;
            readonly Vector3 scale;

            public TransformRestPose(Transform target)
            {
                active = target.gameObject.activeSelf;
                position = target.localPosition;
                rotation = target.localRotation;
                scale = target.localScale;
            }

            public void Apply(Transform target)
            {
                target.gameObject.SetActive(active);
                target.localPosition = position;
                target.localRotation = rotation;
                target.localScale = scale;
            }
        }
    }
}
