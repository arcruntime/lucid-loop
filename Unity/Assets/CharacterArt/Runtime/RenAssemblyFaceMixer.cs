using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LucidLoop.CharacterArt
{
    // Generated speech targets already contain jaw and lip motion. They are
    // alternatives to the primitive controls, not additive jaw-open correctives.
    public static class RenAssemblyFaceMixer
    {
        static string Semantic(string key) => key.Substring(key.LastIndexOf('.') + 1);
        public static Dictionary<string, float> Mix(IReadOnlyDictionary<string, float> input, float blinkL, float blinkR)
        {
            var channels = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in input)
            {
                string name = Semantic(pair.Key);
                float value = float.IsFinite(pair.Value) ? Mathf.Clamp01(pair.Value) : 0;
                channels[name] = Mathf.Max(channels.TryGetValue(name, out var old) ? old : 0, value);
            }
            float Read(string name) => channels.TryGetValue(name, out var value) ? value : 0;
            float speech = channels.Where(p => p.Key.StartsWith("speech_", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Value);
            float normalization = Mathf.Max(1, speech);
            float seal = Mathf.Max(Read("lipSeal"), Read("speech_MBP") / normalization);
            float closureL = Mathf.Max(Mathf.Clamp01(blinkL), Read("eyeBlinkL"));
            float closureR = Mathf.Max(Mathf.Clamp01(blinkR), Read("eyeBlinkR"));
            var output = new Dictionary<string, float>();
            foreach (var pair in input)
            {
                string name = Semantic(pair.Key);
                float value = Read(name);
                if (name.StartsWith("speech_", StringComparison.OrdinalIgnoreCase))
                {
                    value /= normalization;
                    if (!name.Equals("speech_MBP", StringComparison.OrdinalIgnoreCase)) value *= 1 - seal;
                }
                else if (name.StartsWith("jaw", StringComparison.OrdinalIgnoreCase) || name.StartsWith("mouth", StringComparison.OrdinalIgnoreCase) || name.StartsWith("tongue", StringComparison.OrdinalIgnoreCase) || name == "lowerLipRollIn")
                    value *= (1 - Mathf.Clamp01(speech)) * (1 - seal);
                if (name == "emotion_Amused" || name == "emotion_Skeptical" || name == "emotion_Alert") value *= 1 - seal;
                if (name.Equals("eyeBlinkL", StringComparison.OrdinalIgnoreCase)) value = closureL;
                if (name.Equals("eyeBlinkR", StringComparison.OrdinalIgnoreCase)) value = closureR;
                if (name == "eyeWideL" || name == "eyeSquintL") value *= 1 - closureL;
                if (name == "eyeWideR" || name == "eyeSquintR") value *= 1 - closureR;
                if (name.StartsWith("gaze", StringComparison.Ordinal))
                {
                    string side = name.EndsWith("L", StringComparison.Ordinal) ? "L" : "R";
                    float x = Read("gazeRight" + side) - Read("gazeLeft" + side);
                    float y = Read("gazeUp" + side) - Read("gazeDown" + side);
                    float radius = Mathf.Max(1, Mathf.Sqrt(x * x + y * y));
                    float direction = name.StartsWith("gazeRight") ? x : name.StartsWith("gazeLeft") ? -x : name.StartsWith("gazeUp") ? y : -y;
                    float open = 1 - (side == "L" ? closureL : closureR);
                    value = Mathf.Max(0, direction) / radius * open * open;
                }
                output[pair.Key] = Mathf.Clamp01(value);
            }
            return output;
        }
    }
}
