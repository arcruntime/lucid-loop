using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class BilingualSpeechProfileTests
    {
        [Test]
        public void LegacyAdapterPreservesAllFifteenTargets()
        {
            var expected = new[] { "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U" };

            Assert.That(FacePoseComposer.SpeechLabels, Is.EqualTo(expected));
            foreach (var label in expected)
                Assert.That(FacePoseComposer.IsValidSpeechLabel(label), Is.True, label);
        }

        [Test]
        public void LanguageProfilesKeepJapaneseFuAndTapRDistinctFromEnglishShapes()
        {
            var profiles = BilingualSpeechLibrary.CreateDefaultPoses();

            var englishF = Find(profiles, SpeechLanguage.English, "fv");
            var japaneseFu = Find(profiles, SpeechLanguage.Japanese, "fu");
            var englishR = Find(profiles, SpeechLanguage.English, "r");
            var englishL = Find(profiles, SpeechLanguage.English, "l");
            var japaneseR = Find(profiles, SpeechLanguage.Japanese, "r_tap");

            Assert.That(englishF.Targets.Single().ShapeName, Is.EqualTo("viseme_FF"));
            Assert.That(japaneseFu.Targets.Single().ShapeName, Is.EqualTo("viseme_ja_FU"));
            Assert.That(englishR.Targets.Single().ShapeName, Is.EqualTo("viseme_RR"));
            Assert.That(englishL.Targets.Single().ShapeName, Is.EqualTo("viseme_en_L"));
            Assert.That(japaneseR.Targets.Single().ShapeName, Is.EqualTo("viseme_ja_R"));
        }

        [Test]
        public void JapaneseFiveVowelsAreIndependentlyAddressableWithCompressedU()
        {
            var profiles = BilingualSpeechLibrary.CreateDefaultPoses();
            var vowelTargets = new Dictionary<string, string>
            {
                ["a"] = "viseme_aa",
                ["i"] = "viseme_I",
                ["u"] = "viseme_ja_U",
                ["e"] = "viseme_E",
                ["o"] = "viseme_O"
            };

            foreach (var pair in vowelTargets)
            {
                var pose = Find(profiles, SpeechLanguage.Japanese, pair.Key);
                Assert.That(pose.Targets.Single().ShapeName, Is.EqualTo(pair.Value), pair.Key);
            }
            Assert.That(Find(profiles, SpeechLanguage.Japanese, "u").Contact,
                Is.EqualTo(SpeechContactBehavior.VowelCompressed));
        }

        [Test]
        public void UnknownLanguageAndPoseAreReportedWithoutApproximation()
        {
            var profiles = BilingualSpeechLibrary.CreateDefaultPoses();

            Assert.That(BilingualSpeechLibrary.TryParseLanguage("Korean", out _, out var languageError), Is.False);
            Assert.That(languageError, Does.Contain("UNKNOWN_SPEECH_LANGUAGE"));
            Assert.That(BilingualSpeechLibrary.TryFindPose(profiles, SpeechLanguage.Japanese, "english_ff", out _, out var poseError), Is.False);
            Assert.That(poseError, Does.Contain("UNKNOWN_SPEECH_POSE"));
        }

        [Test]
        public void MissingJapaneseTargetIsReportedWithoutEnglishFallback()
        {
            var root = new GameObject("speech target diagnostic test");
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            try
            {
                profile.SpeechPoses = BilingualSpeechLibrary.CreateDefaultPoses();
                profile.SpeechReviewSequences = BilingualSpeechLibrary.CreateDefaultSequences();
                var driver = root.AddComponent<CharacterFaceDriver>();
                driver.Profile = profile;
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    "SPEECH_TARGET_MISSING: Japanese/u requires viseme_ja_U; no English fallback applied"));

                Assert.That(driver.SetSpeechPose(SpeechLanguage.Japanese, "u"), Is.False);
                Assert.That(driver.LastSpeechDiagnostic, Does.Contain("viseme_ja_U"));
                Assert.That(driver.LastSpeechDiagnostic, Does.Contain("no English fallback"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TimingSequenceReportsMissingTargetsBeforePlayback()
        {
            var root = new GameObject("speech sequence diagnostic test");
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            try
            {
                profile.SpeechPoses = BilingualSpeechLibrary.CreateDefaultPoses();
                profile.SpeechReviewSequences = BilingualSpeechLibrary.CreateDefaultSequences();
                var driver = root.AddComponent<CharacterFaceDriver>();
                driver.Profile = profile;
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    "SPEECH_TARGET_MISSING: Japanese/ja_vowels requires .*viseme_ja_U.*no English fallback applied"));

                Assert.That(driver.StartSpeechReviewSequence(SpeechLanguage.Japanese, "ja_vowels"), Is.False);
                Assert.That(driver.LastSpeechDiagnostic, Does.Contain("viseme_ja_U"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void JapaneseContextSequencesSelectDifferentNasalContacts()
        {
            var sequences = BilingualSpeechLibrary.CreateDefaultSequences();

            Assert.That(FindSequence(sequences, "ja_n_sampo").Steps[1].PoseId, Is.EqualTo("n_bilabial"));
            Assert.That(FindSequence(sequences, "ja_n_sanda").Steps[1].PoseId, Is.EqualTo("n_alveolar"));
            Assert.That(FindSequence(sequences, "ja_n_sanka").Steps[1].PoseId, Is.EqualTo("n_velar"));
        }

        [Test]
        public void CoverageMatrixExposesEnglishCentralAndGlideAndJapaneseGlottalVariants()
        {
            var poses = BilingualSpeechLibrary.CreateDefaultPoses();

            Assert.That(Find(poses, SpeechLanguage.English, "schwa").Contact,
                Is.EqualTo(SpeechContactBehavior.VowelCentral));
            Assert.That(Find(poses, SpeechLanguage.English, "w").Contact,
                Is.EqualTo(SpeechContactBehavior.Glide));
            Assert.That(Find(poses, SpeechLanguage.Japanese, "h").Contact,
                Is.EqualTo(SpeechContactBehavior.GlottalOpen));
            Assert.That(Find(poses, SpeechLanguage.Japanese, "hi").Contact,
                Is.EqualTo(SpeechContactBehavior.GlottalOpen));
        }

        [Test]
        public void ReviewFixturesCoverLongVowelsLoanwordsAndConnectedEnglish()
        {
            var sequences = BilingualSpeechLibrary.CreateDefaultSequences();

            Assert.That(FindSequence(sequences, "en_connected").Steps.Length, Is.GreaterThan(4));
            Assert.That(FindSequence(sequences, "ja_long_vowels").Steps.Count(step => step.HoldSeconds >= .2f),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(FindSequence(sequences, "ja_loan_ti_di_tu_du").ReviewText,
                Does.Contain("ティ").And.Contain("ドゥ"));
            Assert.That(FindSequence(sequences, "ja_loan_v_w").ReviewText,
                Does.Contain("ヴ").And.Contain("ウォ"));
        }

        [Test]
        public void ReviewTimelineTransitionsBetweenPosesWithoutInsertingSilence()
        {
            var sequence = new SpeechReviewSequence
            {
                Id = "english_diphthong",
                Language = SpeechLanguage.English,
                Steps = new[]
                {
                    new SpeechReviewStep("a", .1f, .08f),
                    new SpeechReviewStep("i", .1f, 0f)
                }
            };
            var timeline = new SpeechReviewTimeline(sequence);

            var frame = timeline.Advance(.14f);

            Assert.That(frame.FromPoseId, Is.EqualTo("a"));
            Assert.That(frame.ToPoseId, Is.EqualTo("i"));
            Assert.That(frame.Blend, Is.EqualTo(.5f).Within(.001f));
            Assert.That(frame.FromPoseId, Is.Not.EqualTo("sil"));
            Assert.That(frame.ToPoseId, Is.Not.EqualTo("sil"));
        }

        [Test]
        public void JapaneseSpeechStillOwnsLowerFaceWhileBlinkRemainsIndependent()
        {
            var expression = new Dictionary<string, float>
            {
                ["browInnerUp"] = 35f,
                ["mouthSmileLeft"] = 90f,
                ["eyeWideLeft"] = 70f
            };
            var speech = new Dictionary<string, float> { ["viseme_ja_FU"] = 100f };

            var pose = FacePoseComposer.ComposeResolvedSpeech(expression, 1f, speech, true, new BlinkFrame(.5f, 0f));

            Assert.That(pose["browInnerUp"], Is.EqualTo(35f));
            Assert.That(pose.ContainsKey("mouthSmileLeft"), Is.False);
            Assert.That(pose["viseme_ja_FU"], Is.EqualTo(100f));
            Assert.That(pose["eyeBlinkLeft"], Is.EqualTo(50f));
            Assert.That(pose["eyeWideLeft"], Is.EqualTo(35f));
        }

        static SpeechPoseProfile Find(IEnumerable<SpeechPoseProfile> poses, SpeechLanguage language, string id) =>
            poses.Single(pose => pose.Language == language && pose.Id == id);

        static SpeechReviewSequence FindSequence(IEnumerable<SpeechReviewSequence> sequences, string id) =>
            sequences.Single(sequence => sequence.Id == id);
    }
}
