using NUnit.Framework;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class IdlePlaybackMathTests
    {
        [Test]
        public void AnimationOnlyIdleLoopsWithoutDependingOnImporterWrapMode()
        {
            Assert.That(IdlePlaybackMath.WrapTime(10.2, 10.0), Is.EqualTo(.2).Within(.0001));
            Assert.That(IdlePlaybackMath.WrapTime(20.25, 10.0), Is.EqualTo(.25).Within(.0001));
        }
    }
}
