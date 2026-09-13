using System;
using System.Collections.Generic;

namespace LucidLoop.CharacterArt
{
    public static class FacePoseComposer
    {
        public static readonly string[] SpeechLabels =
        {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
        };

        static readonly HashSet<string> validSpeechLabels =
            new HashSet<string>(SpeechLabels, StringComparer.Ordinal);

        public static readonly string[] ReservedSpeechShapeNames =
        {
            "viseme_ja_U", "viseme_ja_FU", "viseme_ja_R", "viseme_en_L"
        };

        static readonly HashSet<string> validSpeechShapeNames = BuildValidSpeechShapeNames();

        public static Dictionary<string, float> Compose(
            IReadOnlyDictionary<string, float> expression,
            float expressionIntensity,
            IReadOnlyDictionary<string, float> speech,
            bool speechActive,
            BlinkFrame blink)
        {
            var resolved = new Dictionary<string, float>(StringComparer.Ordinal);
            if (speech != null)
                foreach (var pair in speech)
                    if (validSpeechLabels.Contains(pair.Key)) resolved["viseme_" + pair.Key] = pair.Value;
            return ComposeResolvedSpeech(expression, expressionIntensity, resolved, speechActive, blink);
        }

        public static Dictionary<string, float> ComposeResolvedSpeech(
            IReadOnlyDictionary<string, float> expression,
            float expressionIntensity,
            IReadOnlyDictionary<string, float> resolvedSpeech,
            bool speechActive,
            BlinkFrame blink)
        {
            var pose = new Dictionary<string, float>(StringComparer.Ordinal);
            var intensity = Clamp01(expressionIntensity);

            if (expression != null)
            {
                foreach (var pair in expression)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                    if (speechActive && IsLowerFaceShape(pair.Key)) continue;
                    pose[pair.Key] = ClampWeight(pair.Value) * intensity;
                }
            }

            if (speechActive && resolvedSpeech != null)
            {
                foreach (var pair in resolvedSpeech)
                {
                    if (!validSpeechShapeNames.Contains(pair.Key) || pair.Key == "viseme_sil") continue;
                    pose[pair.Key] = ClampWeight(pair.Value);
                }
            }

            ApplyBlink(pose, "Left", blink.Left);
            ApplyBlink(pose, "Right", blink.Right);
            return pose;
        }

        public static bool IsValidSpeechLabel(string label) =>
            !string.IsNullOrEmpty(label) && validSpeechLabels.Contains(label);

        public static bool IsValidSpeechShapeName(string shapeName) =>
            !string.IsNullOrEmpty(shapeName) && validSpeechShapeNames.Contains(shapeName);

        public static bool IsLowerFaceShape(string shapeName)
        {
            return shapeName.StartsWith("mouth", StringComparison.OrdinalIgnoreCase) ||
                   shapeName.StartsWith("jaw", StringComparison.OrdinalIgnoreCase) ||
                   shapeName.StartsWith("viseme_", StringComparison.OrdinalIgnoreCase);
        }

        static void ApplyBlink(IDictionary<string, float> pose, string side, float closure)
        {
            var blinkName = "eyeBlink" + side;
            var wideName = "eyeWide" + side;
            var closureWeight = Clamp01(closure) * 100f;

            if (pose.TryGetValue(blinkName, out var expressionBlink))
                pose[blinkName] = Math.Max(ClampWeight(expressionBlink), closureWeight);
            else if (closureWeight > 0f)
                pose[blinkName] = closureWeight;

            if (pose.TryGetValue(wideName, out var wide))
                pose[wideName] = ClampWeight(wide) * (1f - Clamp01(closure));
        }

        static float ClampWeight(float value) => value < 0f ? 0f : value > 100f ? 100f : value;
        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        static HashSet<string> BuildValidSpeechShapeNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var label in SpeechLabels) names.Add("viseme_" + label);
            foreach (var shapeName in ReservedSpeechShapeNames) names.Add(shapeName);
            return names;
        }
    }
}
