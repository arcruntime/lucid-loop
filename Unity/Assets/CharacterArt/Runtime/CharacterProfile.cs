using System;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    [CreateAssetMenu(menuName = "Lucid Loop/Character Profile", fileName = "CharacterProfile")]
    public sealed class CharacterProfile : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public GameObject ModelPrefab;
        public Color Accent = new Color(.95f, .12f, .55f);
        public ExpressionPreset[] Expressions = Array.Empty<ExpressionPreset>();
        public SpeechPoseProfile[] SpeechPoses = Array.Empty<SpeechPoseProfile>();
        public SpeechReviewSequence[] SpeechReviewSequences = Array.Empty<SpeechReviewSequence>();
        public AnimationClip IdleClip;
        public RestPoseMetadata RestPose = new RestPoseMetadata();
    }

    [Serializable]
    public sealed class ExpressionPreset
    {
        public string Id;
        public string Label;
        public BlendshapeWeight[] BlendshapeWeights = Array.Empty<BlendshapeWeight>();
        public Vector3 HeadEuler;
        public Vector2 Gaze;
        public AnimationClip GestureClip;
        public AccessoryTransformPose[] AccessoryPoses = Array.Empty<AccessoryTransformPose>();
    }

    [Serializable]
    public struct BlendshapeWeight
    {
        public string ShapeName;
        [Range(0f, 100f)] public float Weight;

        public BlendshapeWeight(string shapeName, float weight)
        {
            ShapeName = shapeName;
            Weight = weight;
        }
    }

    [Serializable]
    public struct VisemeWeight
    {
        public string Label;
        [Range(0f, 1f)] public float Weight;

        public VisemeWeight(string label, float weight)
        {
            Label = label;
            Weight = weight;
        }
    }

    [Serializable]
    public sealed class AccessoryTransformPose
    {
        public string TransformPath;
        public bool Active = true;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public Vector3 LocalScale = Vector3.one;
    }

    [Serializable]
    public sealed class RestPoseMetadata
    {
        public string HeadBonePath;
        public string LeftEyeBonePath;
        public string RightEyeBonePath;
        public Vector3 FaceFocusOffset = new Vector3(0f, 1.55f, 0f);
        public Vector3 FullBodyFocusOffset = new Vector3(0f, .95f, 0f);
        public float CharacterHeight = 1.8f;
    }
}
