using NUnit.Framework;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class BlinkTimingStateTests
    {
        [Test]
        public void AutomaticBlinkRunsCompleteTimeBasedLifecycle()
        {
            var blink = new BlinkTimingState(() => 3f);

            Assert.That(blink.Advance(2.8f).Left, Is.EqualTo(0f).Within(.001f));
            Assert.That(blink.Advance(.2f).Left, Is.EqualTo(0f).Within(.001f));
            Assert.That(blink.Phase, Is.EqualTo(BlinkPhase.Closing));
            Assert.That(blink.Advance(.035f).Left, Is.EqualTo(.5f).Within(.001f));
            Assert.That(blink.Advance(.035f).Left, Is.EqualTo(1f).Within(.001f));
            Assert.That(blink.Advance(.035f).Left, Is.EqualTo(1f).Within(.001f));
            Assert.That(blink.Advance(.055f).Left, Is.EqualTo(.5f).Within(.001f));
            Assert.That(blink.Advance(.055f).Left, Is.EqualTo(0f).Within(.001f));
            Assert.That(blink.Phase, Is.EqualTo(BlinkPhase.Waiting));
        }

        [Test]
        public void LongTimestepConsumesEveryCompletedPhaseWithoutLeavingEyesClosed()
        {
            var blink = new BlinkTimingState(() => 5f);
            blink.Trigger(BlinkEyes.Both);

            var frame = blink.Advance(1f);

            Assert.That(frame.Left, Is.EqualTo(0f).Within(.001f));
            Assert.That(frame.Right, Is.EqualTo(0f).Within(.001f));
            Assert.That(blink.Phase, Is.EqualTo(BlinkPhase.Waiting));
            Assert.That(blink.TimeUntilAutomaticBlink, Is.EqualTo(4.215f).Within(.001f));
        }

        [Test]
        public void ManualBlinkCanCloseOnlyOneEye()
        {
            var blink = new BlinkTimingState(() => 4f);
            blink.Trigger(BlinkEyes.Left);

            var halfwayClosed = blink.Advance(.035f);

            Assert.That(halfwayClosed.Left, Is.EqualTo(.5f).Within(.001f));
            Assert.That(halfwayClosed.Right, Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void DisablingAutomaticBlinkOpensEyesAndStopsCountdown()
        {
            var blink = new BlinkTimingState(() => 3f);
            blink.Trigger(BlinkEyes.Both);
            blink.Advance(.04f);

            blink.AutomaticEnabled = false;
            var frame = blink.Advance(10f);

            Assert.That(frame.Left, Is.Zero);
            Assert.That(frame.Right, Is.Zero);
            Assert.That(blink.Phase, Is.EqualTo(BlinkPhase.Waiting));
            Assert.That(blink.TimeUntilAutomaticBlink, Is.EqualTo(3f).Within(.001f));
        }
    }
}
