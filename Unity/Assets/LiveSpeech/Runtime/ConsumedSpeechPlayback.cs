using System;

namespace LucidLoop.LiveSpeech
{
    /// <summary>Bounded mono PCM queue; the clock advances only on real samples read by audio playback.</summary>
    public sealed class ConsumedSpeechPlayback
    {
        readonly object gate = new object();
        readonly float[] samples;
        int read, write, count;
        long consumed;
        bool starved = true;
        public ConsumedSpeechPlayback(int capacity) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); samples = new float[capacity]; }
        public int Count { get { lock (gate) return count; } }
        public bool TryWritePcm16(byte[] bytes)
        {
            if (bytes == null || bytes.Length % 2 != 0) throw new ArgumentException("Complete PCM16 samples required.");
            lock (gate)
            {
                if (bytes.Length / 2 > samples.Length - count) return false;
                for (int i = 0; i < bytes.Length; i += 2)
                { samples[write] = (short)(bytes[i] | bytes[i + 1] << 8) / 32768f; write = (write + 1) % samples.Length; count++; }
                return true;
            }
        }
        public float ReadBlock(float[] output)
        {
            lock (gate)
            {
                int available = Math.Min(output.Length, count); float sum = 0;
                for (int i = 0; i < available; i++)
                { float value = samples[read]; read = (read + 1) % samples.Length; output[i] = value; sum += value * value; }
                Array.Clear(output, available, output.Length - available);
                count -= available; consumed += available; starved = available < output.Length;
                return (float)Math.Sqrt(sum / Math.Max(1, output.Length));
            }
        }
        public void Snapshot(out long consumedSamples, out bool isStarved)
        { lock (gate) { consumedSamples = consumed; isStarved = starved; } }
        public void Clear()
        { lock (gate) { read = write = count = 0; consumed = 0; starved = true; } }
    }
}
