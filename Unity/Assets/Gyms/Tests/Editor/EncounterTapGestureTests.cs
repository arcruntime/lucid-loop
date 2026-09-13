using NUnit.Framework;
using UnityEngine;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterTapGestureTests
    {
        [Test] public void FingerReplacementCannotCompleteTap()
        {
            var tap = new EncounterTapGesture();
            tap.Begin(7, Vector2.zero, 30, false);
            Assert.IsFalse(tap.End(8, Vector2.zero, false));
        }
        [Test] public void DragReturningToOriginRemainsCancelled()
        {
            var tap = new EncounterTapGesture();
            tap.Begin(7, Vector2.zero, 30, false);
            tap.Move(7, new Vector2(31, 0));
            Assert.IsFalse(tap.End(7, Vector2.zero, false));
        }
        [TestCase(true, false)] [TestCase(false, true)]
        public void UiAtEitherEndRejectsFloorTap(bool startUi, bool endUi)
        {
            var tap = new EncounterTapGesture();
            tap.Begin(7, Vector2.zero, 30, startUi);
            Assert.IsFalse(tap.End(7, Vector2.zero, endUi));
        }
        [Test] public void SmallFingerJitterAcceptsOnlyOnce()
        {
            var tap = new EncounterTapGesture();
            tap.Begin(7, Vector2.zero, 30, false);
            Assert.IsTrue(tap.End(7, new Vector2(20, 5), false));
            Assert.IsFalse(tap.End(7, Vector2.zero, false));
        }
        [Test] public void CancelRequiresANewBegin()
        {
            var tap = new EncounterTapGesture();
            tap.Begin(7, Vector2.zero, 30, false);
            tap.Cancel();
            Assert.IsFalse(tap.End(7, Vector2.zero, false));
            tap.Begin(9, Vector2.zero, 30, false);
            Assert.IsTrue(tap.End(9, Vector2.zero, false));
        }
    }
}
