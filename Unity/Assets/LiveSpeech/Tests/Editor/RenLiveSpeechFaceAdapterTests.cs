using NUnit.Framework;
using UnityEngine;

namespace LucidLoop.LiveSpeech.Tests
{
    public sealed class RenLiveSpeechFaceAdapterTests
    {
        GameObject actor;
        Mesh mesh;
        SkinnedMeshRenderer face;
        RenLiveSpeechFaceAdapter adapter;
        [SetUp] public void Setup()
        {
            actor = new GameObject("Ren test"); face = actor.AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            var delta = new Vector3[3];
            foreach (string name in new[] { "speech_A", "speech_MBP", "speech_L", "eyeBlinkL", "emotion_amused" })
                mesh.AddBlendShapeFrame(name, 100, delta, delta, delta);
            face.sharedMesh = mesh; adapter = actor.AddComponent<RenLiveSpeechFaceAdapter>(); adapter.Renderers = new[] { face };
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(actor); Object.DestroyImmediate(mesh); }
        [Test] public void BilabialSealExcludesVowelsAndPreservesOtherFacialChannels()
        {
            face.SetBlendShapeWeight(3, 65); face.SetBlendShapeWeight(4, 40);
            var weights = new float[15]; weights[1] = .8f; weights[10] = .9f; adapter.ApplyCanonicalWeights(weights);
            Assert.That(face.GetBlendShapeWeight(0), Is.Zero); Assert.That(face.GetBlendShapeWeight(1), Is.EqualTo(100));
            Assert.That(face.GetBlendShapeWeight(3), Is.EqualTo(65)); Assert.That(face.GetBlendShapeWeight(4), Is.EqualTo(40));
            adapter.ResetSpeech(); Assert.That(face.GetBlendShapeWeight(1), Is.Zero); Assert.That(face.GetBlendShapeWeight(3), Is.EqualTo(65));
        }
        [Test] public void CompleteSnapshotsRemovePreviousContactAndNormalizeSharedTargets()
        {
            var weights = new float[15]; weights[1] = 1; adapter.ApplyCanonicalWeights(weights);
            weights[1] = 0; weights[4] = .8f; weights[8] = .8f; weights[10] = .4f; adapter.ApplyCanonicalWeights(weights);
            Assert.That(face.GetBlendShapeWeight(1), Is.Zero);
            Assert.That(face.GetBlendShapeWeight(2), Is.EqualTo(80).Within(.001));
            Assert.That(face.GetBlendShapeWeight(0), Is.EqualTo(20).Within(.001));
        }
        [Test] public void PrefixedFbxNamesBindAndMeshReplacementInvalidatesCache()
        {
            var weights = new float[15]; weights[10] = 1; adapter.ApplyCanonicalWeights(weights);
            var replacement = new Mesh { vertices = mesh.vertices, triangles = mesh.triangles };
            var delta = new Vector3[3];
            foreach (string name in new[] { "RenHeadSkin.speech_MBP", "RenHeadSkin.speech_A", "RenHeadSkin.eyeBlinkL" })
                replacement.AddBlendShapeFrame(name, 100, delta, delta, delta);
            face.sharedMesh = replacement; Object.DestroyImmediate(mesh); mesh = replacement;
            face.SetBlendShapeWeight(2, 60); adapter.ApplyCanonicalWeights(weights);
            Assert.That(face.GetBlendShapeWeight(0), Is.Zero); Assert.That(face.GetBlendShapeWeight(1), Is.EqualTo(100));
            adapter.ResetSpeech(); Assert.That(face.GetBlendShapeWeight(1), Is.Zero); Assert.That(face.GetBlendShapeWeight(2), Is.EqualTo(60));
        }
        [Test] public void HotReloadPartialCacheRestorationRebuildsMissingBindings()
        {
            var weights = new float[15]; weights[10] = 1; adapter.ApplyCanonicalWeights(weights);
            Assert.That(adapter.BoundSpeechShapeCount, Is.EqualTo(3));
            // Reproduce the former hot-reload state: restored renderer/mesh arrays,
            // freshly constructed empty readonly binding list.
            var field = typeof(RenLiveSpeechFaceAdapter).GetField("bindings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((System.Collections.IList)field.GetValue(adapter)).Clear();
            face.SetBlendShapeWeight(0, 0);
            adapter.ApplyCanonicalWeights(weights);
            Assert.That(face.GetBlendShapeWeight(0), Is.EqualTo(100));
            Assert.That(adapter.BoundSpeechShapeCount, Is.EqualTo(3));
        }
    }
}
