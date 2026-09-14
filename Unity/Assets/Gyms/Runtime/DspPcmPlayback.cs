using System;

namespace LucidLoop.Gyms
{
    /// <summary>Mono PCM16 input at 24 kHz. Render must run at the device DSP output cadence,
    /// never from an AudioClip streaming/prefetch callback. One instance has one output format.</summary>
    public sealed class DspPcmPlayback
    {
        public const int InputSampleRate = 24000;
        readonly object gate = new object();
        readonly float[] ring;
        readonly int outputRate;
        int read, write, count, phase;
        long accepted, consumed;
        bool starved = true, active = true;

        public readonly struct State
        {
            public readonly long AcceptedSamples, ConsumedSamples;
            public readonly bool Active;
            public readonly int QueuedSamples;
            public readonly bool Starved;
            internal State(long accepted, long consumed, int queued, bool starved, bool active)
            { AcceptedSamples = accepted; ConsumedSamples = consumed; QueuedSamples = queued; Starved = starved; Active = active; }
        }

        public DspPcmPlayback(int capacityInputSamples, int outputSampleRate)
        {
            if (capacityInputSamples < 2) throw new ArgumentOutOfRangeException(nameof(capacityInputSamples));
            // The game targets device rates of 44.1/48 kHz. Downsampling requires an anti-alias filter.
            if (outputSampleRate < InputSampleRate || outputSampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(outputSampleRate));
            ring = new float[capacityInputSamples]; outputRate = outputSampleRate;
        }

        public bool TryWritePcm16(byte[] bytes)
        {
            if (bytes == null || (bytes.Length & 1) != 0) return false;
            lock (gate)
            {
                int samples = bytes.Length / 2;
                if (!active || samples > ring.Length - count) return false;
                for (int i = 0; i < bytes.Length; i += 2)
                {
                    ring[write] = (short)(bytes[i] | bytes[i + 1] << 8) / 32768f;
                    if (++write == ring.Length) write = 0;
                }
                count += samples; accepted += samples; return true;
            }
        }

        /// <summary>Fills an entire interleaved device buffer; mono is copied to each output channel.
        /// Retires input only when emitted output frames cross an input-sample boundary.
        /// Looking ahead for interpolation does not consume that input. Underflow outputs zeros
        /// and freezes the input clock; a fresh packet resumes at the retained fractional phase.</summary>
        public void Render(float[] output, int channels)
        {
            if (channels < 1 || channels > 8 || output == null || output.Length % channels != 0) throw new ArgumentException("Complete output frames required.", nameof(output));
            lock (gate)
            {
                for (int offset = 0; offset < output.Length; offset += channels)
                {
                    if (!active || count == 0)
                    {
                        Array.Clear(output, offset, output.Length - offset);
                        starved = true;
                        return;
                    }
                    float current = ring[read];
                    // At an incomplete packet boundary hold the current sample instead of
                    // inventing a zero-valued lookahead. The current sample is still genuine PCM.
                    int next = read + 1 == ring.Length ? 0 : read + 1;
                    float following = count > 1 ? ring[next] : current;
                    float value = current + (following - current) * ((float)phase / outputRate);
                    for (int channel = 0; channel < channels; channel++) output[offset + channel] = value;
                    starved = false;
                    phase += InputSampleRate;
                    if (phase >= outputRate)
                    {
                        phase -= outputRate;
                        if (++read == ring.Length) read = 0;
                        count--; consumed++;
                    }
                }
            }
        }

        public State Snapshot()
        { lock (gate) return new State(accepted, consumed, count, starved, active); }

        /// <summary>Permanently retire this generation. Old DSP callbacks output silence and
        /// subsequent writes are rejected. Consumed count remains available for final reporting.</summary>
        public void Deactivate()
        {
            lock (gate)
            { active = false; count = 0; read = write = phase = 0; starved = true; Array.Clear(ring, 0, ring.Length); }
        }

        /// <summary>Clear an active queue/clock. Does not reactivate a retired generation.</summary>
        public void Reset()
        {
            lock (gate)
            {
                Array.Clear(ring, 0, ring.Length);
                read = write = count = phase = 0;
                accepted = consumed = 0; starved = true;
            }
        }
    }
}
