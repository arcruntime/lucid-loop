using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using NUnit.Framework;

namespace LucidLoop.Gyms.Tests
{
    public sealed class DspPcmPlaybackTests
    {
        static byte[] Pcm(params short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++) { bytes[i * 2] = (byte)samples[i]; bytes[i * 2 + 1] = (byte)(samples[i] >> 8); }
            return bytes;
        }

        [Test]
        public void InterpolationDuplicatesChannelsWithoutConsumingLookahead()
        {
            var queue = new DspPcmPlayback(8, 48000);
            Assert.That(queue.TryWritePcm16(Pcm(0, 16384, -16384, 32767)), Is.True);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.Zero, "Accepting PCM cannot advance playback.");
            var frame = new float[2];
            queue.Render(frame, 2);
            Assert.That(frame, Is.EqualTo(new[] { 0f, 0f }));
            Assert.That(queue.Snapshot().ConsumedSamples, Is.Zero);
            queue.Render(frame, 2);
            Assert.That(frame, Is.EqualTo(new[] { .25f, .25f }));
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(1));
            queue.Render(frame, 2);
            Assert.That(frame[0], Is.EqualTo(.5f));
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(1), "Interpolation lookahead is not consumed.");
            queue.Render(frame, 2);
            Assert.That(frame[0], Is.Zero);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(2));
        }

        [Test, Combinatorial]
        public void ClockTracksEveryOutputBlockWithoutRateDrift(
            [Values(24000, 44100, 48000, 96000)] int rate, [Values(1, 2, 6)] int channels)
        {
            var queue = new DspPcmPlayback(24001, rate);
            Assert.That(queue.TryWritePcm16(new byte[48002]), Is.True);
            var output = new float[channels * 137];
            int rendered = 0;
            while (rendered + 137 <= rate)
            {
                queue.Render(output, channels); rendered += 137;
                var state = queue.Snapshot();
                Assert.That(state.ConsumedSamples, Is.EqualTo((long)rendered * 24000 / rate));
                Assert.That(state.AcceptedSamples, Is.EqualTo(state.ConsumedSamples + state.QueuedSamples));
            }
            queue.Render(new float[(rate - rendered) * channels], channels);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(24000));
        }

        [Test]
        public void IsolatedLastSamplePlaysThenUnderflowFreezesClockAndResumes()
        {
            var queue = new DspPcmPlayback(8, 48000);
            var output = new float[8];
            queue.Render(output, 2);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.Zero);
            Assert.That(queue.TryWritePcm16(Pcm(8192)), Is.True);
            queue.Render(output, 2);
            Assert.That(output, Is.EqualTo(new[] { .25f, .25f, .25f, .25f, 0f, 0f, 0f, 0f }));
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(1));
            Assert.That(queue.Snapshot().Starved, Is.True);
            queue.Render(output, 2);
            Assert.That(output, Is.All.Zero);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(1));
            Assert.That(queue.TryWritePcm16(Pcm(16384)), Is.True);
            var frame = new float[2]; queue.Render(frame, 2);
            Assert.That(frame, Is.EqualTo(new[] { .5f, .5f }));
            Assert.That(queue.Snapshot().Starved, Is.False);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(1));
        }

        [Test]
        public void MalformedOrOversizedWritesAreRejectedAtomically()
        {
            var queue = new DspPcmPlayback(4, 48000);
            Assert.That(queue.TryWritePcm16(Pcm(100, 200, 300)), Is.True);
            Assert.That(queue.TryWritePcm16(null), Is.False);
            Assert.That(queue.TryWritePcm16(new byte[3]), Is.False);
            Assert.That(queue.TryWritePcm16(Pcm(400, 500)), Is.False);
            Assert.That(queue.Snapshot().AcceptedSamples, Is.EqualTo(3));
            Assert.That(queue.Snapshot().QueuedSamples, Is.EqualTo(3));
            var output = new float[6]; queue.Render(output, 1);
            Assert.That(output[0], Is.EqualTo(100f / 32768));
            Assert.That(output[2], Is.EqualTo(200f / 32768));
            Assert.That(output[4], Is.EqualTo(300f / 32768));
        }

        [Test]
        public void CallbackPartitionPreservesFractionalResamplingWaveform()
        {
            var wholeQueue = new DspPcmPlayback(1024, 44100);
            var splitQueue = new DspPcmPlayback(1024, 44100);
            var signal = Pcm(Enumerable.Range(0, 1000).Select(i => (short)(Math.Sin(i * .03) * 20000)).ToArray());
            wholeQueue.TryWritePcm16(signal); splitQueue.TryWritePcm16(signal);
            var whole = new float[1800]; wholeQueue.Render(whole, 1);
            var chunks = new List<float>();
            for (int i = 0; i < 1800; i += 9) { var part = new float[9]; splitQueue.Render(part, 1); chunks.AddRange(part); }
            Assert.That(chunks, Is.EqualTo(whole));
        }

        [Test]
        public void RingWrapAndResetPreserveQueueBoundaries()
        {
            var queue = new DspPcmPlayback(4, 48000);
            for (int cycle = 0; cycle < 100; cycle++)
            {
                Assert.That(queue.TryWritePcm16(Pcm(100, 200, 300)), Is.True);
                queue.Render(new float[6], 1);
            }
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(300));
            queue.TryWritePcm16(Pcm(100)); queue.Reset();
            var state = queue.Snapshot();
            Assert.That(state.ConsumedSamples, Is.Zero);
            Assert.That(state.AcceptedSamples, Is.Zero);
            Assert.That(state.QueuedSamples, Is.Zero);
            Assert.That(state.Starved, Is.True);
            var output = new float[4]; queue.Render(output, 1);
            Assert.That(output, Is.All.Zero);
        }

        [Test]
        public void RenderAndSnapshotAllocateNothingAfterWarmup()
        {
            var queue = new DspPcmPlayback(48000, 48000);
            queue.TryWritePcm16(new byte[96000]);
            var block = new float[128]; queue.Render(block, 2); _ = queue.Snapshot();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { queue.Render(block, 2); _ = queue.Snapshot(); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void ConcurrentWritesRenderingResetAndSnapshotsMaintainAtomicInvariants()
        {
            var queue = new DspPcmPlayback(4096, 48000);
            var packet = new byte[960]; var errors = new ConcurrentQueue<string>();
            Parallel.Invoke(
                () => { for (int i = 0; i < 10000; i++) queue.TryWritePcm16(packet); },
                () => { var output = new float[256]; for (int i = 0; i < 10000; i++) queue.Render(output, 2); },
                () => { for (int i = 0; i < 10000; i++) { var s = queue.Snapshot(); if (s.AcceptedSamples != s.ConsumedSamples + s.QueuedSamples || s.QueuedSamples < 0 || s.QueuedSamples > 4096) errors.Enqueue("Non-atomic snapshot"); } },
                () => { for (int i = 0; i < 100; i++) queue.Reset(); });
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void DeactivatedGenerationRejectsPacketsAndSilencesStaleCallbacksPermanently()
        {
            var queue = new DspPcmPlayback(8, 48000);
            var packet = Pcm(100, 200, 300);
            queue.TryWritePcm16(packet); queue.Render(new float[2], 1);
            queue.Deactivate();
            var retired = queue.Snapshot();
            Assert.That(retired.Active, Is.False);
            Assert.That(retired.QueuedSamples, Is.Zero);
            Assert.That(queue.TryWritePcm16(packet), Is.False);
            var stale = Enumerable.Repeat(1f, 16).ToArray(); queue.Render(stale, 2);
            Assert.That(stale, Is.All.Zero);
            Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(retired.ConsumedSamples));
            queue.Reset();
            Assert.That(queue.TryWritePcm16(packet), Is.False, "Reset must never reactivate a retired generation.");
        }
    }
}
