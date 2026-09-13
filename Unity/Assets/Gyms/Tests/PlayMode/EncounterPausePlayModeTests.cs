using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterPausePlayModeTests
    {
        EncounterCoordinator coordinator;
        [UnityTest, Explicit("Requires the authoritative relay on 8789 and audio fixture on 8790; no provider.")]
        public IEnumerator PauseClosesVoiceAndResynchronizesAcrossReconnect()
        {
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var voice = UnityEngine.Object.FindFirstObjectByType<EncounterVoiceController>();
            string relay = Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game";
            int starts = 0, pauses = 0;
            voice.StreamStarted += _ => starts++;
            coordinator.PauseChanged += _ => pauses++;
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            float deadline = Time.realtimeSinceStartup + 25;
            while (!coordinator.IsReady) { Before(deadline); yield return null; }
            var request = (EncounterConversationRequest)Activator.CreateInstance(typeof(EncounterConversationRequest),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    "ws://127.0.0.1:8790/live", "maya", coordinator.State.LoopId,
                    new JObject { ["type"] = "gym.start", ["character"] = "maya" } }, null);
            voice.Begin(request);
            while (!voice.IsReady) { Before(deadline); yield return null; }
            Assert.That(coordinator.Pause(true), Is.True);
            Assert.That(voice.Output.isPlaying, Is.False, "Pause stops playback before the server acknowledgement.");
            while (!coordinator.IsPaused || !voice.FinalUsageConfirmed || voice.IsClosing)
            { Before(deadline); yield return null; }
            Assert.That(voice.MicrophoneEnabled, Is.False);
            Assert.That(coordinator.RequestConversation("maya"), Is.False);
            Assert.That(coordinator.SendDestination(new Vector3(-2, 0, -4)), Is.False);
            string loop = coordinator.State.LoopId;
            coordinator.Disconnect();
            Assert.That(coordinator.IsPaused, Is.False, "Disconnected local state must not label a new night paused.");
            Assert.That(coordinator.Resume(relay), Is.True);
            while (!coordinator.IsReady || !coordinator.IsPaused) { Before(deadline); yield return null; }
            Assert.That(coordinator.State.LoopId, Is.EqualTo(loop));
            Assert.That(coordinator.Pause(false), Is.True);
            while (coordinator.IsPaused) { Before(deadline); yield return null; }
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(starts, Is.EqualTo(1), "Resume must not reopen paid voice.");
            Assert.That(voice.IsReady, Is.False);
            Assert.That(coordinator.Pause(true), Is.True);
            while (!coordinator.IsPaused) { Before(deadline); yield return null; }
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            int priorPauses = pauses;
            while (!coordinator.IsReady || pauses == priorPauses) { Before(deadline); yield return null; }
            Assert.That(coordinator.State.LoopId, Is.Not.EqualTo(loop));
            Assert.That(coordinator.IsPaused, Is.False, "A fresh game reports its own unpaused state.");
            LogAssert.NoUnexpectedReceived();
        }
        static void Before(float deadline) => Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Pause lifecycle timeout");
        [TearDown] public void TearDown() { if (coordinator) coordinator.Disconnect(); }
    }
}
