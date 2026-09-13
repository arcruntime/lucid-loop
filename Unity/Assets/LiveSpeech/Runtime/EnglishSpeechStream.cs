using System;
using SplatterfaceGames.LipSync;

namespace LucidLoop.LiveSpeech
{
    /// <summary>Main-thread analyzer and bounded target timeline. PCM and playback must share a zero origin.</summary>
    public sealed class EnglishSpeechStream : IDisposable
    {
        public const int SampleRate = 24000;
        public const int LabelCount = 15;
        const int Capacity = 512;
        readonly GaussianModel model;
        readonly VisemeFrame[] frames = new VisemeFrame[Capacity];
        VisemeAnalyzer analyzer;
        int head, count;
        long generation, previousConsumed;
        bool active;
        public string Diagnostic { get; private set; } = string.Empty;
        public int QueuedTargets => count;
        public long AcceptedSamples { get; private set; }

        public EnglishSpeechStream(GaussianModel model)
        { this.model = model ?? throw new ArgumentNullException(nameof(model)); }

        public void Begin(long streamGeneration)
        {
            Dispose();
            generation = streamGeneration;
            analyzer = new VisemeAnalyzer(model, SampleRate, 1);
            active = true;
            Diagnostic = string.Empty;
        }

        public bool PushPcm16(byte[] bytes, long streamGeneration)
        {
            // A stale packet must not poison the current valid stream.
            if (!active || streamGeneration != generation) return false;
            if (bytes == null || bytes.Length % 2 != 0 || bytes.Length > SampleRate * 6)
                return Fail("INVALID_PCM: expected complete mono PCM16, at most three seconds per packet.");
            var emitted = analyzer.ProcessChunk(Pcm16.DecodeMono(bytes));
            if (emitted.Count > Capacity - count)
                return Fail("TARGET_OVERFLOW: reset speech and playback together before resuming.");
            foreach (var frame in emitted)
            {
                frames[(head + count) % Capacity] = frame;
                count++;
            }
            AcceptedSamples += bytes.Length / 2;
            return true;
        }

        /// <summary>Returns a 15-weight frame at samples actually consumed, less measured output latency.
        /// Caller clears speech on starvation, overflow, end, speaker switch and loop reset.</summary>
        public bool Sample(long consumedSamples, long streamGeneration, double outputLatencyMs,
            bool starved, bool ended, float[] weights)
        {
            if (weights == null || weights.Length < LabelCount) throw new ArgumentException("Need 15 weights.", nameof(weights));
            Array.Clear(weights, 0, weights.Length);
            if (!active || streamGeneration != generation) return false;
            if (consumedSamples < previousConsumed || consumedSamples > AcceptedSamples ||
                double.IsNaN(outputLatencyMs) || double.IsInfinity(outputLatencyMs) || outputLatencyMs < 0)
                return Fail("PLAYBACK_CLOCK_MISMATCH: reset speech and playback together.");
            previousConsumed = consumedSamples;
            double timeMs = Math.Max(0, consumedSamples * 1000.0 / SampleRate - outputLatencyMs);
            // Retain a single predecessor, allowing interpolation without unbounded history.
            while (count > 1 && At(1).TimeMs <= timeMs)
            { frames[head] = null; head = (head + 1) % Capacity; count--; }
            if (starved || ended || count == 0 || timeMs < At(0).TimeMs || timeMs - At(count - 1).TimeMs > 150)
                return false;
            var before = At(0);
            var after = count > 1 ? At(1) : before;
            float blend = after.TimeMs > before.TimeMs
                ? (float)Math.Clamp((timeMs - before.TimeMs) / (after.TimeMs - before.TimeMs), 0, 1) : 0;
            // Do not interpolate through the seal of a winning bilabial contact.
            var winner = blend < .5f ? before : after;
            if (winner.LabelIndex == 1) { weights[1] = 1; return true; }
            for (int i = 0; i < LabelCount; i++)
                weights[i] = Math.Clamp(before.Weights[i] + (after.Weights[i] - before.Weights[i]) * blend, 0, 1);
            return true;
        }

        VisemeFrame At(int offset) => frames[(head + offset) % Capacity];
        bool Fail(string reason) { Dispose(); Diagnostic = reason; return false; }
        public void Dispose()
        {
            analyzer?.Dispose(); analyzer = null; active = false;
            Array.Clear(frames, 0, frames.Length); head = count = 0;
            AcceptedSamples = previousConsumed = 0;
        }
    }
}
