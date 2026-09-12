using NUnit.Framework;
namespace LucidLoop.Gyms.Tests
{
    public class AudioTests
    {
        [Test] public void PcmIsLittleEndianAndClamped()
        {
            Assert.That(AudioRingBuffer.Encode(new[]{-2f,0f,2f},3), Is.EqualTo(new byte[]{0,128,0,0,255,127}));
        }
        [Test] public void BufferPreservesOrderAndUnderrunIsSilence()
        {
            var b = new AudioRingBuffer(4); b.WritePcm16(new byte[]{0,64,0,192});
            Assert.That(b.Count,Is.EqualTo(2));
            Assert.That(b.Read(),Is.EqualTo(.5f).Within(.0001));
            Assert.That(b.Read(),Is.EqualTo(-.5f).Within(.0001));
            Assert.That(b.Read(),Is.Zero);
        }
        [Test] public void OverflowDropsOldAudioRatherThanBuildingLatency()
        {
            var b=new AudioRingBuffer(2); b.WritePcm16(new byte[]{0,32,0,64,0,96});
            Assert.That(b.Count,Is.EqualTo(2));
            Assert.That(b.Read(),Is.EqualTo(.5f).Within(.0001));
            b.Clear(); Assert.That(b.Read(),Is.Zero);
        }
        [Test] public void IncompletePcmIsRejected()
        {
            Assert.Throws<System.ArgumentException>(()=>new AudioRingBuffer(4).WritePcm16(new byte[]{1}));
        }
    }
}
