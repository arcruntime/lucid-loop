using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.CharacterArt.PlayModeTests
{
    public sealed class CharacterSpeechFramePlayModeTests
    {
        readonly List<Object> owned = new List<Object>();
        float savedCaptureDelta;

        [SetUp]
        public void SetUp() { savedCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = .02f; }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = savedCaptureDelta;
            foreach (var item in owned) if (item) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator MixedSnapshotsClearAbsentTargetsAndResetReleasesExpression()
        {
            var driver = Fixture(out var renderer);
            Assert.That(driver.ApplySpeechFrame(new[] { En("l", .35f), Ja("u", .65f) }), Is.True);
            yield return null;
            AssertWeight(renderer, "viseme_en_L", 35f);
            AssertWeight(renderer, "viseme_ja_U", 65f);
            AssertWeight(renderer, "mouthSmileLeft", 0f);
            Assert.That(driver.ApplySpeechFrame(new[] { En("a", .5f) }), Is.True);
            AssertWeight(renderer, "viseme_en_L", 0f);
            AssertWeight(renderer, "viseme_ja_U", 0f);
            AssertWeight(renderer, "viseme_aa", 50f);
            Assert.That(driver.ApplySpeechFrame(System.Array.Empty<SpeechPoseWeight>()), Is.True);
            AssertWeight(renderer, "viseme_aa", 0f);
            AssertWeight(renderer, "mouthSmileLeft", 75f);
            Assert.That(driver.ApplySpeechFrame(new[] { Ja("u") }), Is.True);
            driver.ResetSpeech();
            yield return null;
            AssertWeight(renderer, "viseme_ja_U", 0f);
            AssertWeight(renderer, "mouthSmileLeft", 75f);
            Assert.That(driver.ActiveExpression.Id, Is.EqualTo("engaged"));
        }

        [UnityTest]
        public IEnumerator InvalidAndMissingBindingFramesClearSpeechButPreserveBlinkGazeAndExpression()
        {
            var driver = Fixture(out var renderer);
            driver.ManualBlink(BlinkEyes.Left);
            yield return null;
            var blink = Weight(renderer, "eyeBlinkLeft");
            Assert.That(blink, Is.GreaterThan(0f), "A real idle-blink state must be active for this ownership check.");
            var gaze = Weight(renderer, "eyeLookOutLeft");
            Assert.That(gaze, Is.GreaterThan(0f));
            var invalid = new[] { En("a", float.NaN), En("missing"), Ja("fu") };
            var diagnostics = new[] { "INVALID_SPEECH_WEIGHT", "UNKNOWN_SPEECH_POSE", "SPEECH_TARGET_MISSING" };
            for (var i = 0; i < invalid.Length; i++)
            {
                Assert.That(driver.ApplySpeechFrame(new[] { En("l", .4f), Ja("u", .6f) }), Is.True);
                LogAssert.Expect(LogType.Warning, new Regex(diagnostics[i]));
                Assert.That(driver.ApplySpeechFrame(new[] { En("a", .3f), invalid[i] }), Is.False);
                Assert.That(driver.LastSpeechDiagnostic, Does.StartWith(diagnostics[i]));
                AssertWeight(renderer, "viseme_en_L", 0f);
                AssertWeight(renderer, "viseme_ja_U", 0f);
                AssertWeight(renderer, "viseme_aa", 0f);
                AssertWeight(renderer, "mouthSmileLeft", 75f);
                AssertWeight(renderer, "browInnerUp", 30f);
                AssertWeight(renderer, "eyeBlinkLeft", blink);
                AssertWeight(renderer, "eyeLookOutLeft", gaze);
                Assert.That(driver.ActiveExpression.Id, Is.EqualTo("engaged"));
                Assert.That(driver.AutomaticBlink, Is.False);
            }
            driver.ResetSpeech();
            AssertWeight(renderer, "eyeBlinkLeft", blink);
            AssertWeight(renderer, "eyeLookOutLeft", gaze);
            yield return null;
            Assert.That(Weight(renderer, "eyeBlinkLeft"), Is.GreaterThan(blink), "Speech rejection must not restart/cancel the continuing blink.");
        }

        CharacterFaceDriver Fixture(out SkinnedMeshRenderer renderer)
        {
            var root = Own(new GameObject("Weighted speech renderer fixture"));
            var mesh = Own(new Mesh { name = "Minimal real speech blendshape mesh" });
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            foreach (var name in new[] { "viseme_aa", "viseme_en_L", "viseme_ja_U", "mouthSmileLeft", "browInnerUp", "eyeBlinkLeft", "eyeBlinkRight", "eyeLookOutLeft" })
                mesh.AddBlendShapeFrame(name, 100f, new[] { Vector3.forward * .01f, Vector3.zero, Vector3.zero }, new Vector3[3], new Vector3[3]);
            renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            var profile = Own(ScriptableObject.CreateInstance<CharacterProfile>());
            profile.Id = "weighted-speech-test";
            profile.SpeechPoses = BilingualSpeechLibrary.CreateDefaultPoses();
            profile.Expressions = new[] { new ExpressionPreset { Id = "engaged", BlendshapeWeights = new[] {
                new BlendshapeWeight("mouthSmileLeft", 75f), new BlendshapeWeight("browInnerUp", 30f),
                new BlendshapeWeight("eyeLookOutLeft", 25f)
            } } };
            var driver = root.AddComponent<CharacterFaceDriver>();
            driver.Profile = profile;
            driver.AutomaticBlink = false;
            driver.SetExpression("engaged");
            return driver;
        }

        T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        static SpeechPoseWeight En(string id, float weight = 1f) => new SpeechPoseWeight(SpeechLanguage.English, id, weight);
        static SpeechPoseWeight Ja(string id, float weight = 1f) => new SpeechPoseWeight(SpeechLanguage.Japanese, id, weight);
        static float Weight(SkinnedMeshRenderer renderer, string shape) => renderer.GetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(shape));
        static void AssertWeight(SkinnedMeshRenderer renderer, string shape, float value) => Assert.That(Weight(renderer, shape), Is.EqualTo(value).Within(.001f), shape);
    }
}
