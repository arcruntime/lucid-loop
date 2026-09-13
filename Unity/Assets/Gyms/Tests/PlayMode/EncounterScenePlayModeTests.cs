using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    // Explicit: needs the real local relay running, not paid voice or a silent fixture.
    public sealed class EncounterScenePlayModeTests
    {
        EncounterCoordinator coordinator;
        float deadline;
        string evidence;
        readonly HashSet<string> phases = new HashSet<string>();

        [UnityTest, Explicit("Requires the built BeforeTheDrop scene and real localhost:8789/game relay.")]
        public IEnumerator OpeningMovementCatastropheAndRetainedReset() => RunOpening(false);

        [UnityTest, Explicit("Requires the real local relay; previews phone HUD in the Editor Game view.")]
        public IEnumerator PhoneOpeningAndExpandableRetainedClues() => RunOpening(true);

        [UnityTest, Explicit("Requires the local relay; validates approach without opening a paid voice session.")]
        public IEnumerator ApproachStartsConversationOnlyAfterServerEligibility()
        {
            deadline = Time.realtimeSinceStartup + 90f;
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) { CheckDeadline("approach scene"); yield return null; }
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var voice = UnityEngine.Object.FindFirstObjectByType<EncounterVoiceController>();
            voice.enabled = false; // Exercise the real world transport and camera, not the provider.
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            hud.Voice = null;
            int requests = 0;
            coordinator.ConversationRequested += request =>
            {
                Assert.That(coordinator.ConversationEligibility(request.CharacterId, out var eligible, out _), Is.True);
                Assert.That(eligible, Is.True);
                requests++;
            };
            string relay = Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game";
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            while (!coordinator.IsReady || !coordinator.ConversationEligibility("ren", out _, out _))
            { CheckDeadline("approach world ready"); yield return null; }
            Assert.That(coordinator.RequestConversation("ren"), Is.True);
            Assert.That(requests, Is.Zero);
            Assert.That(hud.Rig.Target, Is.Null, "Camera must stay in overview during approach.");
            while (requests == 0) { CheckDeadline("approaching Ren"); yield return null; }
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(hud.Rig.Target.Id, Is.EqualTo("ren"));
            Assert.That(coordinator.RequestConversation("luca"), Is.True);
            Assert.That(coordinator.SendDestination(new Vector3(0, 0, -8)), Is.True);
            Assert.That(coordinator.PendingConversationNpc, Is.Null);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(requests, Is.EqualTo(1), "A cancelled approach must not open a later conversation.");
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator RunOpening(bool phone)
        {
            deadline = Time.realtimeSinceStartup + 90f;
            phases.Clear();
            evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".local", "validation", (phone ? "phone-encounter-smoke-" : "encounter-smoke-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(evidence);
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            Assert.That(loading, Is.Not.Null, "Build BeforeTheDrop and enable it in the shared build scene list first.");
            while (!loading.isDone) { CheckDeadline("scene load"); yield return null; }
            yield return null; // Run the actual scene components' Start methods and create HUD.
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            Assert.That(layout, Is.Not.Null);
            layout.PreviewPhoneLayout = phone;
            yield return null;
            Assert.That(hud.Coordinator, Is.SameAs(coordinator));
            Assert.That(hud.Rig, Is.Not.Null);
            Assert.That(hud.Rig.Camera, Is.Not.Null);
            Assert.That(hud.Rig.Camera.enabled, Is.True);
            Assert.That(coordinator.Characters.Select(a => a.Id), Is.EquivalentTo(new[] { "player", "maya", "ren", "luca", "theo", "affair_partner" }));
            var actors = coordinator.Characters.ToDictionary(a => a.Id);
            var presentation = UnityEngine.Object.FindFirstObjectByType<EncounterPrimitivePresentation>();
            Assert.That(presentation, Is.Not.Null, "Apply primitive presentation to the built scene first.");
            var standingRotation = actors["luca"].Visual.localRotation;
            var standingPosition = actors["luca"].Visual.localPosition;
            var start = actors.ToDictionary(pair => pair.Key, pair => pair.Value.transform.position);
            coordinator.StateChanged += ObservePhase;
            bool worldArrived = false;
            coordinator.WorldChanged += _ => worldArrived = true;
            string relay = Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game";
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            float connectDeadline = Time.realtimeSinceStartup + 28f;
            while (!coordinator.IsReady || !worldArrived)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(connectDeadline), "Local game relay did not become ready: " + coordinator.Status);
                if (!coordinator.IsConnecting && !coordinator.IsReady) Assert.Fail("Game connection failed: " + coordinator.Status);
                CheckDeadline("relay ready/world"); yield return null;
            }
            Assert.That(coordinator.State.LoopIndex, Is.EqualTo(1));
            Assert.That(coordinator.Status, Is.EqualTo("ready"));
            Assert.That(VisibleHudText(), Does.Contain("LOOP 1").And.Contain("Aggressive"));
            // Capture initial positions AFTER receiving the authoritative placement.
            start = actors.ToDictionary(pair => pair.Key, pair => pair.Value.transform.position);
            yield return Capture("01-ready.png");
            Assert.That(coordinator.SendDestination(new Vector3(10f, 0f, -1.7f)), Is.True, "Authoritative world must accept the opening route request.");
            while (coordinator.State.Phase != "catastrophe")
            {
                Assert.That(coordinator.IsReady, Is.True, "Relay disconnected: " + coordinator.Status);
                CheckDeadline("opening causality; last phase=" + coordinator.State.Phase);
                yield return null;
            }
            yield return null;
            Assert.That(Vector3.Distance(start["player"], actors["player"].transform.position), Is.GreaterThan(2f), "Player did not visibly move through server world updates.");
            Assert.That(Vector3.Distance(start["maya"], actors["maya"].transform.position), Is.GreaterThan(1f), "Maya did not visibly follow.");
            Assert.That(Vector3.Distance(start["theo"], actors["theo"].transform.position), Is.GreaterThan(.25f), "Theo did not visibly approach.");
            Assert.That(Vector3.Distance(start["luca"], actors["luca"].transform.position), Is.GreaterThan(1f), "Luca did not visibly intervene.");
            Assert.That(phases, Does.Contain("recording").And.Contain("theo_approaching").And.Contain("luca_intervening"));
            Assert.That(VisibleHudText(), Does.Contain("disaster"));
            Assert.That(coordinator.State.PlayerDiscoveries.Count, Is.GreaterThan(0));
            var clueIds = coordinator.State.PlayerDiscoveries.Select(c => (string)c["factId"]).ToArray();
            Assert.That(clueIds, Does.Contain("shove_seen"));
            // Allow the visual fall and the last authoritative interpolation to settle.
            yield return new WaitForSecondsRealtime(presentation.FallSeconds + .15f);
            var fallenRotation = actors["luca"].Visual.localRotation;
            var fallenRoot = actors["luca"].transform.position;
            Assert.That(Quaternion.Angle(standingRotation, fallenRotation), Is.GreaterThan(80f));
            for (int frame = 0; frame < 6; frame++)
            {
                yield return null;
                Assert.That(Quaternion.Angle(fallenRotation, actors["luca"].Visual.localRotation), Is.LessThan(.01f), "Terminal fall must remain held.");
                Assert.That(Vector3.Distance(fallenRoot, actors["luca"].transform.position), Is.LessThan(.01f), "Visual fall must not displace the authoritative root.");
            }
            yield return Capture("02-catastrophe.png");
            string firstLoop = coordinator.State.LoopId;
            Assert.That(coordinator.Reset(), Is.True);
            while (coordinator.State.LoopIndex != 2) { CheckDeadline("reset"); yield return null; }
            yield return null;
            Assert.That(coordinator.State.LoopId, Is.Not.EqualTo(firstLoop));
            Assert.That(coordinator.State.Phase, Is.EqualTo("exploring"));
            Assert.That(coordinator.State.PlayerDiscoveries.Select(c => (string)c["factId"]), Is.SupersetOf(clueIds));
            Assert.That(VisibleHudText(), Does.Contain("LOOP 2").And.Contain("Aggressive"));
            Assert.That(Quaternion.Angle(standingRotation, actors["luca"].Visual.localRotation), Is.LessThan(.01f), "Loop reset must restore the standing visual.");
            Assert.That(Vector3.Distance(standingPosition, actors["luca"].Visual.localPosition), Is.LessThan(.001f));
            layout.ClueToggle.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(layout.CluesExpanded, Is.True);
            Assert.That(VisibleHudText(), Does.Contain("Theo shoved Luca"), "Retained clue must be readable after expansion.");
            yield return Capture("03-loop-two.png");
            LogAssert.NoUnexpectedReceived();
            File.WriteAllText(Path.Combine(evidence, "result.txt"), "PASS: real scene + local authoritative relay; movement, opening phases, catastrophe, HUD, screenshots, retained reset. No voice/model/phoneme/production-art acceptance claimed.\n");
            Debug.Log("ENCOUNTER_SCENE_SMOKE_PASSED: " + evidence);
        }

        [TearDown]
        public void Teardown()
        {
            if (coordinator) { coordinator.StateChanged -= ObservePhase; coordinator.Disconnect(); }
            // The TestRunner owns scene restoration; never save or destroy the user's scene here.
        }
        void ObservePhase(EncounterClientState state) { if (state.Phase != null) phases.Add(state.Phase); }
        void CheckDeadline(string stage) => Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "90-second smoke deadline: " + stage);
        static string VisibleHudText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(text => text.isActiveAndEnabled).Select(text => text.text));
        IEnumerator Capture(string name)
        {
            string path = Path.Combine(evidence, name);
            ScreenCapture.CaptureScreenshot(path);
            while (!File.Exists(path)) { CheckDeadline("screenshot " + name); yield return null; }
            // Completion is asynchronous; require readable PNG signature, not mere file existence.
            byte[] bytes = Array.Empty<byte>();
            while (bytes.Length < 24)
            {
                CheckDeadline("screenshot write " + name);
                try { bytes = File.ReadAllBytes(path); } catch (IOException) { }
                yield return null;
            }
            Assert.That(bytes.Take(8).ToArray(), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        }
    }
}
