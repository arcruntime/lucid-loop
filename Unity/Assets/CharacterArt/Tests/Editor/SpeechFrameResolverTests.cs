using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class SpeechFrameResolverTests
    {
        readonly SpeechPoseProfile[] profiles = BilingualSpeechLibrary.CreateDefaultPoses();
        readonly Dictionary<string, float> output = new Dictionary<string, float>();
        SpeechPoseWeight En(string id, float weight = 1f) => new SpeechPoseWeight(SpeechLanguage.English, id, weight);
        SpeechPoseWeight Ja(string id, float weight = 1f) => new SpeechPoseWeight(SpeechLanguage.Japanese, id, weight);

        [Test]
        public void MixedLanguageSnapshotReplacesAbsentTargetsAndEmptyClears()
        {
            Assert.That(SpeechFrameResolver.TryResolve(new[] { En("l", .3f), Ja("u", .7f) }, profiles, output, out _), Is.True);
            Assert.That(output["viseme_en_L"], Is.EqualTo(30f).Within(.001f));
            Assert.That(output["viseme_ja_U"], Is.EqualTo(70f));
            Assert.That(SpeechFrameResolver.TryResolve(new[] { En("a") }, profiles, output, out _), Is.True);
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(SpeechFrameResolver.TryResolve(Array.Empty<SpeechPoseWeight>(), profiles, output, out _), Is.True);
            Assert.That(output, Is.Empty);
        }

        [Test]
        public void InvalidEntriesClearWholeSnapshotIncludingEarlierValidEntries()
        {
            var invalid = new[] { En("a", float.NaN), En("a", float.PositiveInfinity), En("a", -.1f), En("a", 1.1f), En("missing"), new SpeechPoseWeight((SpeechLanguage)99, "a", 1f) };
            foreach (var entry in invalid)
            {
                output["viseme_aa"] = 100f;
                Assert.That(SpeechFrameResolver.TryResolve(new[] { Ja("u"), entry }, profiles, output, out var diagnostic), Is.False);
                Assert.That(diagnostic, Is.Not.Empty);
                Assert.That(output, Is.Empty);
            }
        }

        [Test]
        public void DuplicatePosesAndNullFrameFailExplicitly()
        {
            Assert.That(SpeechFrameResolver.TryResolve(new[] { En("a"), En("a") }, profiles, output, out var error), Is.False);
            Assert.That(error, Does.StartWith("DUPLICATE_SPEECH_POSE"));
            Assert.That(SpeechFrameResolver.TryResolve(null, profiles, output, out error), Is.False);
            Assert.That(error, Does.StartWith("INVALID_SPEECH_FRAME"));
        }

        [Test]
        public void SharedTargetsAccumulateWithoutNormalizingUnrelatedTargets()
        {
            Assert.That(SpeechFrameResolver.TryResolve(new[] { En("a", .8f), Ja("a", .8f), En("l", .4f) }, profiles, output, out _), Is.True);
            Assert.That(output["viseme_aa"], Is.EqualTo(100f));
            Assert.That(output["viseme_en_L"], Is.EqualTo(40f));
        }

        [Test]
        public void BadAuthoredTargetsCannotWriteExpressionsOrNonFiniteWeights()
        {
            foreach (var target in new[] { new SpeechTargetContribution("eyeBlinkLeft", 1f), new SpeechTargetContribution("viseme_aa", float.NaN) })
            {
                var bad = new[] { new SpeechPoseProfile { Language = SpeechLanguage.English, Id = "bad", Targets = new[] { target } } };
                Assert.That(SpeechFrameResolver.TryResolve(new[] { En("bad") }, bad, output, out var error), Is.False);
                Assert.That(error, Does.StartWith("INVALID_SPEECH_TARGET"));
                Assert.That(output, Is.Empty);
            }
        }

        [Test]
        public void EveryCanonicalSlotMapsToItsExactTargetInBothSegments()
        {
            var labels = new[] { "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U", "ja_U", "ja_FU", "ja_R", "en_L" };
            foreach (var language in new[] { SpeechLanguage.English, SpeechLanguage.Japanese })
                for (var i = 0; i < labels.Length; i++)
                {
                    Assert.That(CanonicalSpeechPoseMap.TryMap(i, language, 1f, out var pose), Is.True);
                    Assert.That(SpeechFrameResolver.TryResolve(new[] { pose }, profiles, output, out _), Is.True);
                    if (i == 0) Assert.That(output, Is.Empty);
                    else Assert.That(output["viseme_" + labels[i]], Is.EqualTo(100f), language + "/" + labels[i]);
                }
            Assert.That(CanonicalSpeechPoseMap.TryMap(-1, SpeechLanguage.English, 1f, out _), Is.False);
            Assert.That(CanonicalSpeechPoseMap.TryMap(19, SpeechLanguage.English, 1f, out _), Is.False);
        }

        [Test]
        public void SpeechCompositionLeavesBrowsBlinkAndGazeChannelsOwnedByTheirLayers()
        {
            SpeechFrameResolver.TryResolve(new[] { Ja("fu", .5f), En("l", .5f) }, profiles, output, out _);
            var expression = new Dictionary<string, float> { ["browInnerUp"] = 40f, ["mouthSmileLeft"] = 90f, ["eyeLookOutLeft"] = 20f };
            var pose = FacePoseComposer.ComposeResolvedSpeech(expression, 1f, output, true, new BlinkFrame(.5f, 0f));
            Assert.That(pose["browInnerUp"], Is.EqualTo(40f));
            Assert.That(pose["eyeLookOutLeft"], Is.EqualTo(20f));
            Assert.That(pose["eyeBlinkLeft"], Is.EqualTo(50f));
            Assert.That(pose.ContainsKey("mouthSmileLeft"), Is.False);
            Assert.That(expression["mouthSmileLeft"], Is.EqualTo(90f));
        }
    }
}
