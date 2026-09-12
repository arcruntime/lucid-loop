using NUnit.Framework;
namespace LucidLoop.Gyms.Tests
{
    public class GestureTests
    {
        [Test] public void ShortGroundPressIsATap()
        {
            var g = new HoldGesture(); g.Begin(0,0,0,false);
            Assert.That(g.Tick(.2f), Is.EqualTo(GestureResult.None));
            Assert.That(g.Release(), Is.EqualTo(GestureResult.Tap));
        }
        [Test] public void CharacterHoldFiresOnceAndReleaseCannotWalk()
        {
            var g = new HoldGesture(); g.Begin(0,0,0,true);
            Assert.That(g.Tick(.5f), Is.EqualTo(GestureResult.Hold));
            Assert.That(g.Tick(.6f), Is.EqualTo(GestureResult.None));
            Assert.That(g.Release(), Is.EqualTo(GestureResult.None));
        }
        [Test] public void DragCancelsInteraction()
        {
            var g = new HoldGesture(); g.Begin(0,0,0,true); g.Move(50,0,16);
            Assert.That(g.Tick(1), Is.EqualTo(GestureResult.None));
            Assert.That(g.Release(), Is.EqualTo(GestureResult.None));
        }
        [Test] public void CancelledFingerCannotBecomeTap()
        {
            var g = new HoldGesture(); g.Begin(0,0,0,false); g.Cancel();
            Assert.That(g.Release(), Is.EqualTo(GestureResult.None));
        }
    }
}
