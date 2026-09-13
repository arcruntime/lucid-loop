using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterAudioDevicePlayModeTests
    {
        [UnityTest, Explicit("Requires the localhost:8790 protocol fixture; no provider or microphone permission.")]
        public IEnumerator DeviceChangeStopsPlaybackAndClosesWithoutAutomaticReconnect()
        {
            var host = new GameObject("Audio device lifecycle test");
            host.AddComponent<AudioListener>();
            var voice = host.AddComponent<EncounterVoiceController>();
            voice.Output = host.AddComponent<AudioSource>();
            int starts = 0, stops = 0;
            voice.StreamStarted += _ => starts++;
            voice.StreamStopped += _ => stops++;
            var changed = typeof(EncounterVoiceController).GetMethod("OnAudioConfigurationChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                var request = (EncounterConversationRequest)Activator.CreateInstance(typeof(EncounterConversationRequest),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    Environment.GetEnvironmentVariable("LUCID_LOOP_AUDIO_FIXTURE_URL") ?? "ws://127.0.0.1:8790/live",
                    "maya", "fixture-loop", new JObject { ["type"] = "gym.start", ["character"] = "maya" } }, null);
                voice.Begin(request);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!voice.IsReady)
                { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), voice.Status); yield return null; }
                changed.Invoke(voice, new object[] { false });
                yield return null;
                Assert.That(voice.IsReady, Is.True, "Internal microphone/audio setup notification is not device removal.");
                changed.Invoke(voice, new object[] { true });
                deadline = Time.realtimeSinceStartup + 20;
                while (!voice.FinalUsageConfirmed || voice.IsClosing)
                { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), voice.Status); yield return null; }
                Assert.That(voice.IsReady, Is.False);
                Assert.That(voice.MicrophoneEnabled, Is.False);
                Assert.That(voice.Output.isPlaying, Is.False);
                Assert.That(voice.Output.clip, Is.Null);
                Assert.That(stops, Is.EqualTo(1));
                StringAssert.Contains("Audio device changed", voice.Status);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(starts, Is.EqualTo(1), "A device change must not open a new paid session.");
                voice.Begin(request);
                deadline = Time.realtimeSinceStartup + 20;
                while (!voice.IsReady)
                { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), voice.Status); yield return null; }
                Assert.That(starts, Is.EqualTo(2), "Explicit user restart must work.");
                StringAssert.DoesNotContain("Audio device changed", voice.Status);
                voice.Leave();
                deadline = Time.realtimeSinceStartup + 20;
                while (!voice.FinalUsageConfirmed || voice.IsClosing)
                { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), voice.Status); yield return null; }
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.Destroy(host); }
        }
    }
}
