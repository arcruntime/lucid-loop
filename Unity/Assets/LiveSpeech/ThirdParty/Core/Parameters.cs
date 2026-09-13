// Ported from HeadAudio modules/parameters.mjs (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.
// Names and values are kept identical to upstream.

namespace SplatterfaceGames.LipSync
{
    /// <summary>Shared processing constants, ported 1:1 from parameters.mjs.</summary>
    internal static class Parameters
    {
        // Audio parameters
        public const int AudioSampleRate = 16000; // AUDIO_SAMPLE_RATE
        public const int AudioDownsampleFilterN = 32; // AUDIO_DOWNSAMPLE_FILTER_N
        public const int AudioDownsamplePhaseN = 64; // AUDIO_DOWNSAMPLE_PHASE_N
        public const bool AudioPreemphasisEnabled = true; // AUDIO_PREEMPHASIS_ENABLED
        public const double AudioPreemphasisAlpha = 0.97; // AUDIO_PREEMPHASIS_ALPHA

        // MFCC parameters
        public const int MfccSamplesN = 512; // MFCC_SAMPLES_N
        public const int MfccSamplesHop = 256; // MFCC_SAMPLES_HOP
        public const int MfccCoeffN = 12; // MFCC_COEFF_N
        public const int MfccMelBandsN = 40; // MFCC_MEL_BANDS_N
        public const int MfccLifter = 22; // MFCC_LIFTER
        public const bool MfccDeltasEnabled = false; // MFCC_DELTAS_ENABLED
        public const bool MfccDeltaDeltasEnabled = false; // MFCC_DELTA_DELTAS_ENABLED
        public const int MfccCoeffNWithDeltas =
            MfccCoeffN * (MfccDeltasEnabled ? (MfccDeltaDeltasEnabled ? 3 : 2) : 1); // MFCC_COEFF_N_WITH_DELTAS
        public const bool MfccCompressionEnabled = true; // MFCC_COMPRESSION_ENABLED
        public const double MfccCompressionTanhR = 1.0; // MFCC_COMPRESSION_TANH_R

        // Model parameters
        public const int ModelVisemesN = 15; // MODEL_VISEMES_N
        public const int ModelVisemeSil = 14; // MODEL_VISEME_SIL

        // Binary record layout (offsets in bytes, lengths in 32-bit floats)
        public const int RecordHeaderOffset = 0; // RECORD_HEADER_OFFSET
        public const int RecordHeaderLen = 2; // RECORD_HEADER_LEN
        public const int RecordMuOffset = RecordHeaderLen * 4; // RECORD_MU_OFFSET
        public const int RecordMuLen = MfccCoeffNWithDeltas; // RECORD_MU_LEN
        public const int RecordSigmaInvLowerOffset = RecordMuOffset + RecordMuLen * 4; // RECORD_SIGMAINVLOWER_OFFSET
        public const int RecordSigmaInvLowerLen = MfccCoeffNWithDeltas * (MfccCoeffNWithDeltas + 1) / 2; // RECORD_SIGMAINVLOWER_LEN
        public const int RecordOffset = RecordSigmaInvLowerOffset + RecordSigmaInvLowerLen * 4; // RECORD_OFFSET
        public const int RecordLen = RecordHeaderLen + RecordMuLen + RecordSigmaInvLowerLen; // RECORD_LEN
    }
}
