using System;
using System.Collections.Generic;

namespace LucidLoop.CharacterArt
{
    public readonly struct SpeechPoseWeight
    {
        public readonly SpeechLanguage Language;
        public readonly string PoseId;
        public readonly float Weight;

        public SpeechPoseWeight(SpeechLanguage language, string poseId, float weight)
        {
            Language = language;
            PoseId = poseId;
            Weight = weight;
        }
    }

    /// <summary>Speech-only snapshot expansion. No renderer access or contact correction.</summary>
    public static class SpeechFrameResolver
    {
        public static bool TryResolve(IReadOnlyList<SpeechPoseWeight> frame,
            IReadOnlyList<SpeechPoseProfile> profiles, Dictionary<string, float> output,
            out string diagnostic)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            diagnostic = string.Empty;
            if (frame == null) return Fail(output, "INVALID_SPEECH_FRAME: null; use an empty snapshot or ResetSpeech.", out diagnostic);
            for (var i = 0; i < frame.Count; i++)
            {
                var entry = frame[i];
                if (entry.Language != SpeechLanguage.English && entry.Language != SpeechLanguage.Japanese)
                    return Fail(output, "UNKNOWN_SPEECH_LANGUAGE: entry " + i, out diagnostic);
                if (!Unit(entry.Weight))
                    return Fail(output, "INVALID_SPEECH_WEIGHT: entry " + i + " must be finite and within [0,1].", out diagnostic);
                for (var j = 0; j < i; j++)
                    if (frame[j].Language == entry.Language && frame[j].PoseId == entry.PoseId)
                        return Fail(output, "DUPLICATE_SPEECH_POSE: " + entry.Language + "/" + entry.PoseId, out diagnostic);
                SpeechPoseProfile pose = null;
                if (profiles != null)
                    for (var j = 0; j < profiles.Count; j++)
                        if (profiles[j] != null && profiles[j].Language == entry.Language && profiles[j].Id == entry.PoseId)
                        {
                            if (pose != null) return Fail(output, "AMBIGUOUS_SPEECH_PROFILE: " + entry.PoseId, out diagnostic);
                            pose = profiles[j];
                        }
                if (pose == null || string.IsNullOrWhiteSpace(entry.PoseId))
                    return Fail(output, "UNKNOWN_SPEECH_POSE: " + entry.Language + "/" + entry.PoseId, out diagnostic);
                if (pose.Targets == null)
                    return Fail(output, "INVALID_SPEECH_PROFILE: null targets for " + entry.PoseId, out diagnostic);
                foreach (var target in pose.Targets)
                {
                    if (!FacePoseComposer.IsValidSpeechShapeName(target.ShapeName) || !Unit(target.Weight))
                        return Fail(output, "INVALID_SPEECH_TARGET: " + entry.PoseId + "/" + target.ShapeName, out diagnostic);
                    if (entry.PoseId == "sil" || target.ShapeName == "viseme_sil" || entry.Weight == 0f || target.Weight == 0f) continue;
                    output.TryGetValue(target.ShapeName, out var previous);
                    output[target.ShapeName] = Math.Min(100f, previous + 100f * entry.Weight * target.Weight);
                }
            }
            return true;
        }

        static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        static bool Fail(Dictionary<string, float> output, string message, out string diagnostic)
        {
            output.Clear();
            diagnostic = message;
            return false;
        }
    }

    /// <summary>Explicit mapping from upstream PoseLabel.Extended indices; no analyzer dependency.</summary>
    public static class CanonicalSpeechPoseMap
    {
        public const int Count = 19;
        static readonly string[] english = { "sil", "pbm", "fv", "th", "tdn", "kgng", "chj", "sz", "n", "r", "a", "e", "i", "o", "u", "u", "fu", "r_tap", "l" };
        static readonly string[] japanese = { "sil", "pbm", "fv", "th", "tdn", "kg", "ch", "sz", "n_alveolar", "r", "a", "e", "i", "o", "u", "u", "fu", "r_tap", "l" };

        // Canonical FF/TH/RR/U retain their English meaning even in a Japanese segment.
        // Japanese acoustic remapping must occur upstream, before canonical queue insertion.
        public static bool TryMap(int index, SpeechLanguage segmentLanguage, float weight,
            out SpeechPoseWeight pose)
        {
            pose = default;
            if (index < 0 || index >= Count || float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f || weight > 1f ||
                (segmentLanguage != SpeechLanguage.English && segmentLanguage != SpeechLanguage.Japanese)) return false;
            var language = index >= 15 && index <= 17 ? SpeechLanguage.Japanese :
                index == 2 || index == 3 || index == 9 || index == 14 || index == 18 ? SpeechLanguage.English : segmentLanguage;
            pose = new SpeechPoseWeight(language, (language == SpeechLanguage.Japanese ? japanese : english)[index], weight);
            return true;
        }
    }
}
