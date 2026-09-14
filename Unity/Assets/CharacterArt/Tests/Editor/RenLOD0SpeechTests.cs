using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LucidLoop.CharacterArt.Tests
{
    public sealed class RenLOD0SpeechTests
    {
        GameObject root;
        Mesh mesh;
        SkinnedMeshRenderer renderer;
        RenLOD0Controller controller;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Assembled speech test");
            renderer = root.AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh { vertices = new[] { Vector3.zero } };
            foreach (var name in new[] { "Face.speech_A", "Face.speech_MBP", "Face.speech_FV", "Face.eyeBlinkL", "Face.eyeBlinkR", "Hair.capOn", "Face.jawOpen", "Face.lipSeal" })
                mesh.AddBlendShapeFrame(name, 100, new[] { Vector3.up * .001f }, null, null);
            renderer.sharedMesh = mesh;
            controller = root.AddComponent<RenLOD0Controller>();
            controller.LOD0 = root;
            controller.Cap = new GameObject("Cap");
            controller.Cap.transform.SetParent(root.transform);
            controller.IdleBlink = false;
            controller.IdleActing = false;
            controller.ShowControls = false;
            // This EditMode fixture invokes the runtime initializer explicitly.
            // Unity warns even for this renderer's empty material array.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                "Instantiating material due to calling renderer.material during edit mode. This will leak materials into the scene. You most likely want to use renderer.sharedMaterial instead.");
            Invoke("Start");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
        }

        void Invoke(string name) => typeof(RenLOD0Controller).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        float Weight(string name) => renderer.GetBlendShapeWeight(mesh.GetBlendShapeIndex(name));

        [Test]
        public void FullSpeechSnapshotReplacesPreviousPoseWithoutTouchingBlinkOrCap()
        {
            controller.BlinkL = .75f;
            controller.SetSpeechWeights(new Dictionary<string, float> { ["speech_A"] = 1, ["eyeBlinkR"] = 1, ["capOn"] = 0 });
            Invoke("LateUpdate");
            Assert.That(Weight("Face.speech_A"), Is.EqualTo(100));
            Assert.That(Weight("Face.eyeBlinkL"), Is.EqualTo(75));
            Assert.That(Weight("Face.eyeBlinkR"), Is.Zero);
            Assert.That(Weight("Hair.capOn"), Is.EqualTo(100));

            controller.Cap.SetActive(false);
            controller.SetSpeechWeights(new Dictionary<string, float> { ["speech_FV"] = .8f });
            Invoke("LateUpdate");
            Assert.That(Weight("Face.speech_A"), Is.Zero);
            Assert.That(Weight("Face.speech_FV"), Is.EqualTo(80).Within(.001f));
            Assert.That(Weight("Face.eyeBlinkL"), Is.EqualTo(75));
            Assert.That(Weight("Hair.capOn"), Is.Zero);
        }

        [Test]
        public void BilabialContactUsesAssemblyMixerAndNullSnapshotClearsMouth()
        {
            controller.SetSpeechWeights(new Dictionary<string, float> { ["speech_MBP"] = 1 });
            Invoke("LateUpdate");
            Assert.That(Weight("Face.speech_MBP"), Is.EqualTo(100));
            Assert.That(Weight("Face.speech_A"), Is.Zero);
            Assert.That(Weight("Face.jawOpen"), Is.Zero);

            controller.SetSpeechWeights(null);
            Invoke("LateUpdate");
            Assert.That(Weight("Face.speech_MBP"), Is.Zero);
            Assert.That(Weight("Face.speech_A"), Is.Zero);
            Assert.That(Weight("Hair.capOn"), Is.EqualTo(100));
        }

        [Test]
        public void MixedContactIsNormalizedOnceAndSuppressesOpenMouth()
        {
            controller.SetSpeechWeights(new Dictionary<string, float> { ["speech_MBP"] = 1, ["speech_A"] = 1 });
            Invoke("LateUpdate");
            Assert.That(Weight("Face.speech_MBP"), Is.EqualTo(50).Within(.001f));
            Assert.That(Weight("Face.speech_A"), Is.EqualTo(25).Within(.001f));
        }

        [Test]
        public void InvalidAndEmptySnapshotsCannotLeaveStaleSpeech()
        {
            controller.SetSpeechWeights(new Dictionary<string, float> { ["speech_A"] = 1 });
            Invoke("LateUpdate");
            controller.SetSpeechWeights(new Dictionary<string, float> { ["speech_A"] = float.NaN, ["speech_FV"] = float.PositiveInfinity, ["speech_MBP"] = -1 });
            Invoke("LateUpdate");
            Assert.That(Weight("Face.speech_A"), Is.Zero);
            Assert.That(Weight("Face.speech_FV"), Is.Zero);
            Assert.That(Weight("Face.speech_MBP"), Is.Zero);
        }
    }
}
