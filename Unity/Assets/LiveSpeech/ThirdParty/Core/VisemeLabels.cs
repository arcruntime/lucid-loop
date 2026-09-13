// Contract API surface (CONTRACTS.md). Label names follow HeadAudio's Oculus-style
// viseme naming; the ORDER here is the contract order (Oculus OVR order), which differs
// from HeadAudio's internal viseme id order — see VisemeAnalyzer for the mapping.

namespace SplatterfaceGames.LipSync
{
    /// <summary>
    /// The 15 viseme labels in contract order:
    /// sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, I, O, U.
    /// </summary>
    public static class VisemeLabels
    {
        public static readonly string[] Labels =
        {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR",
            "aa", "E", "I", "O", "U"
        };

        public const int Count = 15;
    }

    /// <summary>One analysis frame emitted by <see cref="VisemeAnalyzer"/>.</summary>
    public sealed class VisemeFrame
    {
        /// <summary>Position (input-rate samples) of the analysis window center.</summary>
        public long InputSamplePos;
        /// <summary>Same instant in milliseconds on the input timeline.</summary>
        public double TimeMs;
        /// <summary>Index 0..14 into <see cref="VisemeLabels.Labels"/>.</summary>
        public int LabelIndex;
        /// <summary>Viseme label ("sil","PP",...).</summary>
        public string Label = "sil";
        /// <summary>Weight of the selected label, 0..1.</summary>
        public float Confidence;
        /// <summary>Full 15-class distribution, same order as <see cref="VisemeLabels.Labels"/>.</summary>
        public float[] Weights = new float[VisemeLabels.Count];
    }
}
