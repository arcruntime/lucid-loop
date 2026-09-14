using System;
using System.Collections.Generic;
using System.Reflection;
using LucidLoop.LiveSpeech;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterSpeechBindingTests
    {
        GameObject host;
        EncounterVoiceController voice;
        EncounterCoordinator coordinator;
        EncounterSpeechBinding binding;
        readonly List<Mesh> meshes = new List<Mesh>();
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            host = new GameObject("Encounter speech binding test");
            coordinator = host.AddComponent<EncounterCoordinator>();
            voice = host.AddComponent<EncounterVoiceController>();
            binding = host.AddComponent<EncounterSpeechBinding>();
            binding.enabled = false;
            binding.Voice = voice;
            binding.Coordinator = coordinator;
            binding.enabled = true;
            Lifecycle("OnEnable");
        }

        [TearDown] public void TearDown()
        {
            Lifecycle("OnDisable");
            Object.DestroyImmediate(host);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        RenLiveSpeechFaceAdapter Actor(string id, out CharacterActor actor)
        {
            var root = new GameObject(id);
            root.transform.SetParent(host.transform, false);
            actor = root.AddComponent<CharacterActor>(); actor.Id = id;
            return Face(root.transform, "Active face");
        }

        RenLiveSpeechFaceAdapter Face(Transform parent, string name)
        {
            var face = new GameObject(name);
            face.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Test speech shapes" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("speech_A", 100, new[] { Vector3.up, Vector3.zero, Vector3.zero }, null, null);
            meshes.Add(mesh);
            var renderer = face.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            var adapter = face.AddComponent<RenLiveSpeechFaceAdapter>();
            adapter.Renderers = new[] { renderer };
            return adapter;
        }

        void Lifecycle(string method)
            => typeof(EncounterSpeechBinding).GetMethod(method, PrivateInstance).Invoke(binding, null);

        void Begin(string actorId, int generation)
        {
            typeof(EncounterVoiceController).GetProperty(nameof(EncounterVoiceController.CharacterId))
                .GetSetMethod(true).Invoke(voice, new object[] { actorId });
            Emit("StreamStarted", generation);
        }

        // Drive the real subscribed event delegates without opening a network connection or microphone.
        void Emit(string eventName, params object[] args)
        {
            var listeners = (Delegate)typeof(EncounterVoiceController).GetField(eventName, PrivateInstance).GetValue(voice);
            Assert.That(listeners, Is.Not.Null, eventName + " should have a binding subscriber");
            listeners.DynamicInvoke(args);
        }

        static void OpenMouth(RenLiveSpeechFaceAdapter adapter)
        {
            var weights = new float[15]; weights[10] = 1;
            adapter.ApplyCanonicalWeights(weights);
            Assert.That(Mouth(adapter), Is.EqualTo(100).Within(.001));
        }
        static float Mouth(RenLiveSpeechFaceAdapter adapter) => adapter.Renderers[0].GetBlendShapeWeight(0);
        static void AssertStarted(RenLiveSpeechFaceAdapter adapter)
            => Assert.That(adapter.IsSupported, Is.True, adapter.Diagnostic);

        [Test] public void SelectsOnlySpeakingActorsActiveRenFace()
        {
            var other = Actor("maya", out var maya);
            var ren = Actor("ren", out var actor);
            var hidden = Face(actor.transform, "Retired face");
            hidden.transform.SetAsFirstSibling(); hidden.gameObject.SetActive(false);
            coordinator.Characters = new[] { maya, actor };
            Begin("ren", 10);
            AssertStarted(ren);
            Assert.That(other.IsSupported, Is.False);
            Assert.That(hidden.IsSupported, Is.False);
        }

        [Test] public void ChangingSpeakerClearsPreviousFaceAndStartsNextActor()
        {
            var ren = Actor("ren", out var actor);
            var next = Actor("maya", out var maya);
            coordinator.Characters = new[] { actor, maya };
            Begin("ren", 10); AssertStarted(ren); OpenMouth(ren);
            Begin("maya", 11);
            Assert.That(ren.IsSupported, Is.False);
            Assert.That(Mouth(ren), Is.Zero);
            AssertStarted(next);
            OpenMouth(next);
            Begin("missing-actor", 12);
            Assert.That(next.IsSupported, Is.False);
            Assert.That(Mouth(next), Is.Zero);
            Assert.That(binding.Diagnostic, Is.EqualTo("authored_speaker_face_missing"));
        }

        [Test] public void StaleStopAndProgressCannotResetCurrentGeneration()
        {
            var ren = Actor("ren", out var actor); coordinator.Characters = new[] { actor };
            Begin("ren", 10); Begin("ren", 11); AssertStarted(ren); OpenMouth(ren);
            Emit("StreamStopped", 10);
            Emit("PlaybackProgress", 10, 0L, 48000, true, true);
            AssertStarted(ren);
            Assert.That(Mouth(ren), Is.EqualTo(100).Within(.001));
            Emit("StreamStopped", 11);
            Assert.That(ren.IsSupported, Is.False);
            Assert.That(Mouth(ren), Is.Zero);
        }

        [Test] public void UnsupportedPlaybackRateResetsFaceAndRejectsSubsequentPackets()
        {
            var ren = Actor("ren", out var actor); coordinator.Characters = new[] { actor };
            Begin("ren", 20); AssertStarted(ren); OpenMouth(ren);
            Emit("PlaybackProgress", 20, 480L, 48000, false, false);
            Assert.That(binding.Diagnostic, Is.EqualTo("unsupported_playback_rate"));
            Assert.That(ren.IsSupported, Is.False);
            Assert.That(Mouth(ren), Is.Zero);
            Emit("Pcm16Output", new byte[960], 20);
            Emit("PlaybackProgress", 20, 960L, 24000, false, false);
            Assert.That(ren.IsSupported, Is.False);
            Assert.That(binding.Diagnostic, Is.EqualTo("unsupported_playback_rate"));
        }

        [Test] public void PlaybackEndAndDisableBothResetSpeech()
        {
            var ren = Actor("ren", out var actor); coordinator.Characters = new[] { actor };
            Begin("ren", 30); AssertStarted(ren); OpenMouth(ren);
            Emit("PlaybackProgress", 30, 480L, 24000, false, true);
            Assert.That(ren.IsSupported, Is.False); Assert.That(Mouth(ren), Is.Zero);
            Begin("ren", 31); AssertStarted(ren); OpenMouth(ren);
            binding.enabled = false;
            Lifecycle("OnDisable");
            Assert.That(ren.IsSupported, Is.False); Assert.That(Mouth(ren), Is.Zero);
        }
    }
}
