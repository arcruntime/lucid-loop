using System.Collections.Generic;
using NUnit.Framework;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class FacePoseComposerTests
    {
        [Test]
        public void ActiveSpeechOwnsLowerFaceWhileUpperExpressionRemains()
        {
            var expression = new Dictionary<string, float>
            {
                ["browInnerUp"] = 42f,
                ["mouthSmileLeft"] = 80f,
                ["jawOpen"] = 35f
            };
            var speech = new Dictionary<string, float> { ["aa"] = 75f };

            var pose = FacePoseComposer.Compose(expression, 1f, speech, true, BlinkFrame.Open);

            Assert.That(pose["browInnerUp"], Is.EqualTo(42f));
            Assert.That(pose.ContainsKey("mouthSmileLeft"), Is.False);
            Assert.That(pose.ContainsKey("jawOpen"), Is.False);
            Assert.That(pose["viseme_aa"], Is.EqualTo(75f));
        }

        [Test]
        public void SilenceClearsPreviousVisemeAndRestoresExpressionLowerFace()
        {
            var expression = new Dictionary<string, float> { ["mouthFrownRight"] = 60f };
            var speech = new Dictionary<string, float> { ["O"] = 90f };
            var active = FacePoseComposer.Compose(expression, 1f, speech, true, BlinkFrame.Open);
            var silent = FacePoseComposer.Compose(expression, 1f, speech, false, BlinkFrame.Open);

            Assert.That(active.ContainsKey("mouthFrownRight"), Is.False);
            Assert.That(active["viseme_O"], Is.EqualTo(90f));
            Assert.That(silent["mouthFrownRight"], Is.EqualTo(60f));
            Assert.That(silent.ContainsKey("viseme_O"), Is.False);
        }

        [Test]
        public void BlinkOverridesEyeWideWithoutAddingToClosedExpression()
        {
            var expression = new Dictionary<string, float>
            {
                ["eyeWideLeft"] = 80f,
                ["eyeWideRight"] = 80f,
                ["eyeBlinkLeft"] = 30f,
                ["eyeBlinkRight"] = 100f
            };

            var pose = FacePoseComposer.Compose(
                expression,
                1f,
                new Dictionary<string, float>(),
                false,
                new BlinkFrame(.5f, 1f));

            Assert.That(pose["eyeWideLeft"], Is.EqualTo(40f).Within(.001f));
            Assert.That(pose["eyeBlinkLeft"], Is.EqualTo(50f).Within(.001f));
            Assert.That(pose["eyeWideRight"], Is.Zero.Within(.001f));
            Assert.That(pose["eyeBlinkRight"], Is.EqualTo(100f).Within(.001f));
        }

        [Test]
        public void CompositionClampsNormalizedIntensityAndIncomingWeights()
        {
            var expression = new Dictionary<string, float> { ["browDownLeft"] = 150f };

            var pose = FacePoseComposer.Compose(
                expression,
                2f,
                new Dictionary<string, float>(),
                false,
                BlinkFrame.Open);

            Assert.That(pose["browDownLeft"], Is.EqualTo(100f));
        }
    }
}
