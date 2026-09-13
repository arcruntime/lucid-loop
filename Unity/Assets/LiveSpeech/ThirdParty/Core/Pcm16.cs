// Part of SplatterfaceGames.LipSync.Core. Normalization matches HeadAudio's
// training-node-utils.mjs getFloat32ArrayFromWav: int16 / 0x8000, channels averaged.

using System;
using System.Buffers.Binary;

namespace SplatterfaceGames.LipSync
{
    /// <summary>Little-endian PCM16 decode helpers.</summary>
    public static class Pcm16
    {
        /// <summary>Decode mono PCM16 bytes to normalized floats in [-1, 1].</summary>
        public static float[] DecodeMono(ReadOnlySpan<byte> bytes)
        {
            int n = bytes.Length / 2;
            var samples = new float[n];
            for (int i = 0; i < n; i++)
                samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(i * 2, 2)) / 32768f;
            return samples;
        }

        /// <summary>
        /// Decode interleaved PCM16 with <paramref name="channels"/> channels and
        /// downmix to mono by averaging channels (same as upstream WAV loader).
        /// </summary>
        public static float[] DecodeToMono(ReadOnlySpan<byte> bytes, int channels)
        {
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
            int frames = bytes.Length / (2 * channels);
            var samples = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                double sum = 0;
                for (int ch = 0; ch < channels; ch++)
                    sum += BinaryPrimitives.ReadInt16LittleEndian(
                        bytes.Slice((i * channels + ch) * 2, 2)) / 32768.0;
                samples[i] = (float)(sum / channels);
            }
            return samples;
        }
    }
}
