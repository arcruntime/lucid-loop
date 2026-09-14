using NUnit.Framework;

namespace LucidLoop.LiveSpeech.Tests
{
    public sealed class ConsumedSpeechPlaybackTests
    {
        [Test] public void StarvationDoesNotAdvanceConsumedClock()
        {
            var queue = new ConsumedSpeechPlayback(4);
            queue.TryWritePcm16(new byte[] { 0, 64, 0, 192 });
            var output = new float[4]; queue.ReadBlock(output);
            Assert.That(output, Is.EqualTo(new[] { .5f, -.5f, 0f, 0f }));
            queue.Snapshot(out long consumed, out bool starved);
            Assert.That(consumed, Is.EqualTo(2)); Assert.That(starved, Is.True);
            queue.ReadBlock(output); queue.Snapshot(out consumed, out starved);
            Assert.That(consumed, Is.EqualTo(2)); Assert.That(output, Is.All.Zero);
        }
        [Test] public void OverflowRejectsWholePacketWithoutDroppingOldSamples()
        {
            var queue = new ConsumedSpeechPlayback(2);
            Assert.That(queue.TryWritePcm16(new byte[] { 0, 64 }), Is.True);
            Assert.That(queue.TryWritePcm16(new byte[] { 0, 32, 0, 96 }), Is.False);
            var output = new float[2]; queue.ReadBlock(output);
            Assert.That(output, Is.EqualTo(new[] { .5f, 0f }));
        }
        [Test] public void ClearResetsPlaybackOriginAndRejectsPartialSamples()
        {
            var queue = new ConsumedSpeechPlayback(4);
            queue.TryWritePcm16(new byte[] { 0, 64 }); queue.ReadBlock(new float[1]); queue.Clear();
            queue.Snapshot(out long consumed, out bool starved);
            Assert.That(consumed, Is.Zero); Assert.That(starved, Is.True);
            Assert.That(queue.Count, Is.Zero);
            Assert.Throws<System.ArgumentException>(() => queue.TryWritePcm16(new byte[] { 0 }));
        }
    }
}
