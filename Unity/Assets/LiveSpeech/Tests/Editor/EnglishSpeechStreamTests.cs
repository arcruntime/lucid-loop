using System;
using System.IO;
using NUnit.Framework;
using SplatterfaceGames.LipSync;

namespace LucidLoop.LiveSpeech.Tests
{
    public class EnglishSpeechStreamTests
    {
        static GaussianModel LoadModel()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                var path = Path.Combine(directory.FullName, "Unity/Assets/LiveSpeech/Resources/LiveSpeech/model-en-mixed.bytes");
                if (File.Exists(path)) return GaussianModel.Load(path);
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Run from the lucid-loop repository or a descendant.");
        }

        static byte[] Pcm(int samples)
        {
            var bytes = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                // Deterministic varying harmonic input, not a claimed speech-quality fixture.
                var value = (short)(9000 * Math.Sin(i * .052) * Math.Sin(i * .0009));
                bytes[i * 2] = (byte)value; bytes[i * 2 + 1] = (byte)(value >> 8);
            }
            return bytes;
        }

        [Test] public void IrregularChunksPreserveClockAndAnalyzerTargets()
        {
            using var whole = new EnglishSpeechStream(LoadModel());
            using var chunks = new EnglishSpeechStream(LoadModel());
            whole.Begin(1); chunks.Begin(1);
            var pcm = Pcm(48000);
            Assert.That(whole.PushPcm16(pcm, 1), Is.True);
            for (int offset = 0; offset < pcm.Length;)
            {
                int size = Math.Min(694, pcm.Length - offset);
                var part = new byte[size]; Array.Copy(pcm, offset, part, 0, size);
                Assert.That(chunks.PushPcm16(part, 1), Is.True); offset += size;
            }
            Assert.That(chunks.QueuedTargets, Is.GreaterThan(0));
            Assert.That(chunks.AcceptedSamples, Is.EqualTo(whole.AcceptedSamples));
            var a = new float[15]; var b = new float[15];
            for (int consumed = 0; consumed <= 48000; consumed += 480)
            {
                Assert.That(chunks.Sample(consumed, 1, 40, false, false, b),
                    Is.EqualTo(whole.Sample(consumed, 1, 40, false, false, a)));
                Assert.That(b, Is.EqualTo(a));
            }
            Assert.That(chunks.QueuedTargets, Is.LessThan(8));
        }

        [Test] public void NewStreamRejectsStalePcmAndResetsClock()
        {
            using var stream = new EnglishSpeechStream(LoadModel());
            stream.Begin(1); stream.PushPcm16(Pcm(24000), 1);
            stream.Begin(2);
            Assert.That(stream.PushPcm16(Pcm(480), 1), Is.False);
            Assert.That(stream.AcceptedSamples, Is.Zero);
            Assert.That(stream.PushPcm16(Pcm(24000), 2), Is.True);
            var weights = new float[15];
            Assert.That(stream.Sample(12000, 1, 0, false, false, weights), Is.False);
            Assert.That(stream.Sample(12000, 2, 0, false, false, weights), Is.True);
        }

        [Test] public void StarvationAndEndClearSnapshotWithoutAdvancingClock()
        {
            using var stream = new EnglishSpeechStream(LoadModel());
            stream.Begin(3); stream.PushPcm16(Pcm(24000), 3);
            var weights = new float[15];
            Assert.That(stream.Sample(12000, 3, 0, false, false, weights), Is.True);
            Assert.That(stream.Sample(12000, 3, 0, true, false, weights), Is.False);
            Assert.That(weights, Is.All.Zero);
            Assert.That(stream.Sample(12000, 3, 0, false, true, weights), Is.False);
            Assert.That(weights, Is.All.Zero);
        }

        [Test] public void OverflowFailsClosedAndHistoryIsBounded()
        {
            using var stream = new EnglishSpeechStream(LoadModel());
            stream.Begin(4); var packet = Pcm(72000);
            Assert.That(stream.PushPcm16(packet, 4), Is.True);
            Assert.That(stream.PushPcm16(packet, 4), Is.True);
            Assert.That(stream.PushPcm16(packet, 4), Is.False);
            Assert.That(stream.Diagnostic, Does.StartWith("TARGET_OVERFLOW"));
            Assert.That(stream.QueuedTargets, Is.Zero);
        }

        [Test] public void ImpossiblePlaybackClockFailsClosed()
        {
            using var stream = new EnglishSpeechStream(LoadModel());
            stream.Begin(5); stream.PushPcm16(Pcm(24000), 5);
            Assert.That(stream.Sample(24001, 5, 0, false, false, new float[15]), Is.False);
            Assert.That(stream.Diagnostic, Does.StartWith("PLAYBACK_CLOCK_MISMATCH"));
        }

        [Test] public void IncompletePcmCannotSilentlyLoseBytes()
        {
            using var stream = new EnglishSpeechStream(LoadModel());
            stream.Begin(6);
            Assert.That(stream.PushPcm16(new byte[3], 6), Is.False);
            Assert.That(stream.Diagnostic, Does.StartWith("INVALID_PCM"));
        }
    }
}
