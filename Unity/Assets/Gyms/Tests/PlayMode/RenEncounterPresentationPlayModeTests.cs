using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using LucidLoop.CharacterArt;
using LucidLoop.LiveSpeech;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    // Deterministic presentation validation: no relay, microphone, provider, or acoustic-quality claim.
    public sealed class RenEncounterPresentationPlayModeTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        EncounterVoiceController voice;
        GymCamera rig;
        string evidence;

        [UnityTest, Explicit("Loads the authored BeforeTheDrop scene and captures the Game view.")]
        public IEnumerator AssembledRenKeepsSpeechBlinkAndConversationFraming()
        {
            var load = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;
            var coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            voice = UnityEngine.Object.FindFirstObjectByType<EncounterVoiceController>();
            var binding = UnityEngine.Object.FindFirstObjectByType<EncounterSpeechBinding>();
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(voice, Is.Not.Null);
            Assert.That(binding.Voice, Is.SameAs(voice));
            Assert.That(binding.Coordinator, Is.SameAs(coordinator));
            Assert.That(coordinator.IsReady, Is.False, "This test must not open a relay connection.");
            voice.enabled = false;
            var ren = coordinator.Characters.Single(actor => actor.Id == "ren");
            var controller = ren.GetComponentInChildren<RenLOD0Controller>();
            var adapter = ren.GetComponentInChildren<RenLiveSpeechFaceAdapter>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.FaceController, Is.SameAs(controller));
            Assert.That(ren.GetComponentsInChildren<RenLOD0Controller>().Length, Is.EqualTo(1));
            Assert.That(ren.GetComponentsInChildren<RenLiveSpeechFaceAdapter>().Length, Is.EqualTo(1));
            Assert.That(controller.ShowControls, Is.False);
            Assert.That(ren.ConversationFaceAnchor, Is.Not.Null);
            Assert.That(ren.ConversationFaceAnchor.IsChildOf(controller.transform), Is.True);
            var visible = controller.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(renderer => renderer.enabled && !renderer.forceRenderingOff && renderer.sharedMesh).ToArray();
            Assert.That(visible.Length, Is.GreaterThan(0));
            var bounds = visible[0].bounds;
            foreach (var renderer in visible.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.y, Is.InRange(1f, 2.5f), "Ren should retain plausible world-scale stature.");
            var expanded = bounds; expanded.Expand(.3f);
            Assert.That(expanded.Contains(ren.FacePosition), Is.True, "Conversation anchor must remain on the assembled character.");
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            Assert.That(layout, Is.Not.Null);
            // Use the actual controls so screenshots preserve honest disconnected HUD state.
            if (layout.Connection.gameObject.activeSelf)
                layout.ConnectionButton.GetComponent<Button>().onClick.Invoke();
            Assert.That(layout.Connection.gameObject.activeSelf, Is.False);
            var selectRen = layout.Conversation.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Select ren");
            Assert.That(selectRen.isActiveAndEnabled && selectRen.interactable, Is.True);
            selectRen.onClick.Invoke();
            Assert.That(hud.SelectedNpcId, Is.EqualTo("ren"));
            Assert.That(coordinator.IsReady, Is.False);
            rig = hud.Rig;
            Assert.That(rig, Is.Not.Null);
            rig.Immediate = true;
            rig.Overview(); rig.Apply(1);
            Assert.That(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(rig.Camera), bounds), Is.True);
            evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".local", "validation", "ren-encounter-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(evidence);
            yield return Capture("01-overview.png");
            rig.Present(ren); rig.Apply(1);
            var anchor = rig.Camera.WorldToViewportPoint(ren.FacePosition);
            Assert.That(anchor.z, Is.GreaterThan(0));
            Assert.That(anchor.x, Is.InRange(.1f, .65f), "Face should occupy the conversation side of the frame.");
            Assert.That(anchor.y, Is.InRange(.2f, .85f));
            Assert.That(visible.All(renderer => !renderer.forceRenderingOff), Is.True);

            controller.IdleBlink = false;
            controller.BlinkL = .75f; controller.BlinkR = .25f;
            typeof(EncounterVoiceController).GetProperty(nameof(EncounterVoiceController.CharacterId))
                .GetSetMethod(true).Invoke(voice, new object[] { "ren" });
            Emit("StreamStarted", 101);
            Assert.That(adapter.IsSupported, Is.True, adapter.Diagnostic);
            Assert.That(binding.Diagnostic, Is.Empty);
            yield return null; yield return null;
            var weights = new float[15]; weights[10] = 1;
            AssertWeights(visible, "speech_A", 0);
            adapter.ApplyCanonicalWeights(weights);
            AssertWeights(visible, "speech_A", 0); // Adapter submits; only controller LateUpdate writes the actual mesh.
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
                AssertWeights(visible, "speech_A", 100);
                AssertWeights(visible, "eyeBlinkL", 75);
                AssertWeights(visible, "eyeBlinkR", 25);
            }
            yield return Capture("02-conversation-a-blink.png");
            weights[1] = 1; // MBP closure must suppress the still-present A input.
            adapter.ApplyCanonicalWeights(weights);
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
                AssertWeights(visible, "speech_MBP", 100);
                AssertWeights(visible, "speech_A", 0);
                AssertWeights(visible, "eyeBlinkL", 75);
            }
            yield return Capture("03-conversation-mbp.png");
            Emit("StreamStopped", 100); // Retired generation cannot erase current speech.
            yield return null;
            AssertWeights(visible, "speech_MBP", 100);
            Emit("StreamStopped", 101);
            yield return null; yield return null;
            Assert.That(adapter.IsSupported, Is.False);
            AssertWeights(visible, "speech_MBP", 0);
            AssertWeights(visible, "speech_A", 0);
            AssertWeights(visible, "eyeBlinkL", 75);
            controller.BlinkL = 0; controller.BlinkR = 0;
            yield return null; yield return null;
            yield return Capture("04-conversation-rest.png");
            LogAssert.NoUnexpectedReceived();
            File.WriteAllText(Path.Combine(evidence, "result.txt"), "PASS: actual BeforeTheDrop Ren prefab/controller/adapter, bounds and conversation anchor; subscribed voice start/stop generation routing; deterministic A/blink/MBP/reset across real frames. No network, microphone, acoustic inference accuracy, or natural-conversation quality acceptance.\n");
            Debug.Log("REN_ENCOUNTER_PRESENTATION_PASSED: " + evidence);
        }

        void Emit(string eventName, params object[] args)
        {
            var listeners = (Delegate)typeof(EncounterVoiceController).GetField(eventName, PrivateInstance).GetValue(voice);
            Assert.That(listeners, Is.Not.Null, eventName + " must be subscribed by the authored scene binding");
            listeners.DynamicInvoke(args);
        }
        static void AssertWeights(SkinnedMeshRenderer[] renderers, string suffix, float expected)
        {
            int found = 0;
            foreach (var renderer in renderers)
                for (int index = 0; index < renderer.sharedMesh.blendShapeCount; index++)
                    if (renderer.sharedMesh.GetBlendShapeName(index).Split('.').Last() == suffix)
                    {
                        found++;
                        Assert.That(renderer.GetBlendShapeWeight(index), Is.EqualTo(expected).Within(.01f), renderer.name + ": " + suffix);
                    }
            Assert.That(found, Is.GreaterThan(0), "Actual active Ren mesh must contain " + suffix);
        }
        IEnumerator Capture(string name)
        {
            var path = Path.Combine(evidence, name);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 20;
            byte[] bytes = Array.Empty<byte>();
            while (bytes.Length < 24)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Game view screenshot timed out: " + name);
                yield return null;
                try { if (File.Exists(path)) bytes = File.ReadAllBytes(path); } catch (IOException) { }
            }
            Assert.That(bytes.Take(8).ToArray(), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        }
        [TearDown] public void Cleanup()
        {
            if (rig) rig.Overview();
            // Test runner restores the user's scene; never save scene mutations.
        }
    }
}
