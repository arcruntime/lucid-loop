using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class GazeBlendshapeComposerTests
    {
        [Test]
        public void SynthesizesDirectionalMorphsAtDocumentedCalibration()
        {
            var pose = new Dictionary<string, float>();

            GazeBlendshapeComposer.AddSynthesizedGaze(
                pose, new Dictionary<string, float>(), 1f, new Vector2(10f, -20f), true, true);

            Assert.That(pose["eyeLookInLeft"], Is.EqualTo(50f).Within(.001f));
            Assert.That(pose["eyeLookOutRight"], Is.EqualTo(50f).Within(.001f));
            Assert.That(pose["eyeLookDownLeft"], Is.EqualTo(100f).Within(.001f));
            Assert.That(pose["eyeLookDownRight"], Is.EqualTo(100f).Within(.001f));
        }

        [Test]
        public void ExplicitRecipeOwnsItsEyeAxisWithoutDoubleApplication()
        {
            var expression = new Dictionary<string, float> { ["eyeLookOutLeft"] = 35f };
            var pose = new Dictionary<string, float>(expression);

            GazeBlendshapeComposer.AddSynthesizedGaze(
                pose, expression, 1f, new Vector2(-20f, 20f), true, true);

            Assert.That(pose["eyeLookOutLeft"], Is.EqualTo(35f));
            Assert.That(pose.ContainsKey("eyeLookInRight"), Is.False,
                "An explicitly authored horizontal gaze recipe owns that axis for both eyes.");
            Assert.That(pose["eyeLookUpLeft"], Is.EqualTo(100f));
            Assert.That(pose["eyeLookUpRight"], Is.EqualTo(100f));
        }
    }
}
