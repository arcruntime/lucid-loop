using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LucidLoop.CharacterArt;
using LucidLoop.LiveSpeech;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    // Opt-in paid provider smoke. No microphone, injected audio, reflected events or synthetic weights.
    public sealed class RenLiveEncounterPlayModeTests
    {
        EncounterCoordinator coordinator;
        EncounterVoiceController voice;
        RenLiveSpeechFaceAdapter adapter;
        SkinnedMeshRenderer[] meshes;
        RenSpeechLateFrameRecorder recorder;
        readonly JArray frames = new JArray();
        long previousObservedConsumed;
        int nonzeroFrames, observedFrames, droppedDiagnosticFrames;
        bool observing, progressStarved = true, progressEnded;
        const int MaxDiagnosticFrames = 2048;
        readonly List<string> statuses = new List<string>();
        readonly MemoryStream pcm = new MemoryStream();
        string evidence, screenshot;
        float deadline, began, maxSpeech, lastPcmTime;
        long accepted, consumed;
        int packets, movingFrames, generation;
        bool passed, microphoneObserved, stopped, screenshotRequested;
        const int Rate = 24000, MaxRecordingBytes = Rate * 2 * 15;

        [UnityTest, Explicit("Uses one paid Ren conversation through production local relay 8790; requires visible Game view and audio output.")]
        public IEnumerator RealRelayRenConversationDrivesConsumedSpeechAndCloses()
        {
            began = Time.realtimeSinceStartup;
            deadline = began + 100;
            evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".local", "validation", "ren-live-encounter-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(evidence);
            screenshot = Path.Combine(evidence, "conversation.png");
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            Assert.That(loading, Is.Not.Null);
            while (!loading.isDone) { Before(deadline - 20, "scene load"); yield return null; }
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            voice = UnityEngine.Object.FindFirstObjectByType<EncounterVoiceController>();
            Assert.That(coordinator, Is.Not.Null);
            Assert.That(voice, Is.Not.Null);
            voice.EnableMicrophone(false);
            voice.StatusChanged += RecordStatus;
            voice.Pcm16Output += RecordPcm;
            voice.PlaybackProgress += RecordProgress;
            voice.StreamStopped += RecordStop;
            var ren = coordinator.Characters.Single(actor => actor.Id == "ren");
            adapter = ren.GetComponentInChildren<RenLiveSpeechFaceAdapter>();
            Assert.That(adapter, Is.Not.Null);
            Assert.That(ren.GetComponentInChildren<RenLOD0Controller>(), Is.Not.Null);
            meshes = ren.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh).ToArray();
            Assert.That(meshes.Any(r => Enumerable.Range(0, r.sharedMesh.blendShapeCount).Any(i => IsSpeech(r.sharedMesh.GetBlendShapeName(i)))), Is.True);
            recorder = new GameObject("Ren live speech test observer").AddComponent<RenSpeechLateFrameRecorder>();
            recorder.Observe = ObserveLateFrame;
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            Assert.That(hud, Is.Not.Null);
            if (layout.Connection.gameObject.activeSelf) layout.ConnectionButton.GetComponent<Button>().onClick.Invoke();
            layout.Conversation.GetComponentsInChildren<Button>(true).Single(b => b.name == "Select ren").onClick.Invoke();
            Assert.That(coordinator.ConnectNew("ws://127.0.0.1:8790/game"), Is.True);
            while (!coordinator.IsReady) { Before(deadline - 55, "game ready"); yield return null; }
            // Wait for the actual authoritative world snapshot before approach eligibility.
            yield return null;
            Assert.That(SpeechWeight(), Is.LessThan(.01f), "Ren starts with neutral speech shapes.");
            Assert.That(coordinator.RequestConversation("ren"), Is.True);
            while (!voice.IsReady) { Before(deadline - 35, "Ren approach/provider ready"); yield return null; }
            generation = voice.StreamGeneration;
            Assert.That(voice.CharacterId, Is.EqualTo("ren"));
            Assert.That(adapter.IsSupported, Is.True, adapter.Diagnostic);
            hud.Rig.Present(ren);
            Assert.That(voice.SendText("Hi Ren. In one short sentence, what do you think of the music tonight?"), Is.True);
            previousObservedConsumed = consumed;
            observing = true;
            float observationEnd = Math.Min(deadline - 20, Time.realtimeSinceStartup + 35);
            while (Time.realtimeSinceStartup < observationEnd)
            {
                yield return null;
                if (movingFrames >= 5 && consumed >= Rate && consumed >= accepted &&
                    Time.realtimeSinceStartup - lastPcmTime >= 1 && ValidScreenshot()) break;
                Assert.That(voice.IsReady, Is.True, "Conversation ended before playback evidence: " + voice.Status);
            }
            Assert.That(packets, Is.GreaterThan(0), "No real accepted provider PCM.");
            Assert.That(consumed, Is.GreaterThanOrEqualTo(Rate), "No second of actual renderer consumption.");
            Assert.That(movingFrames, Is.GreaterThanOrEqualTo(5), "Ren mesh did not move during progressing playback.");
            Assert.That(microphoneObserved, Is.False);
            Assert.That(ValidScreenshot(), Is.True, "Visible Game view capture missing.");
            observing = false;
            voice.Leave();
            while (voice.IsClosing) { Before(deadline, "final usage on Leave"); yield return null; }
            yield return null; yield return null;
            Assert.That(voice.FinalUsageConfirmed, Is.True, voice.Status);
            Assert.That(stopped, Is.True);
            Assert.That(adapter.IsSupported, Is.False);
            Assert.That(SpeechWeight(), Is.LessThan(.01f), "Leave must clear Ren's actual speech shapes.");
            Assert.That(voice.MicrophoneEnabled, Is.False);
            LogAssert.NoUnexpectedReceived();
            passed = true;
            Debug.Log("REN_LIVE_ENCOUNTER_PASSED: " + evidence);
        }

        void ObserveLateFrame()
        {
            if (voice) microphoneObserved |= voice.MicrophoneEnabled;
            if (!observing || !voice) return;
            // Both consumption and the mesh pose now belong to the same rendered frame.
            long advance = consumed - previousObservedConsumed;
            previousObservedConsumed = consumed;
            float weight = SpeechWeight();
            maxSpeech = Math.Max(maxSpeech, weight);
            observedFrames++;
            if (weight > .5f) nonzeroFrames++;
            if (advance > 0 && weight > .5f)
            {
                movingFrames++;
                if (!screenshotRequested) { ScreenCapture.CaptureScreenshot(screenshot); screenshotRequested = true; }
            }
            if (frames.Count < MaxDiagnosticFrames)
                frames.Add(new JObject {
                    ["frame"] = Time.frameCount, ["seconds"] = Time.realtimeSinceStartup - began,
                    ["consumed"] = consumed, ["consumedDelta"] = advance, ["accepted"] = accepted,
                    ["starved"] = progressStarved, ["ended"] = progressEnded, ["speechPeak"] = weight,
                    ["adapterSupported"] = adapter && adapter.IsSupported,
                    ["adapterDiagnostic"] = adapter ? adapter.Diagnostic : "adapter_missing",
                    ["visibleRenderers"] = new JArray(meshes.Where(m => m && m.gameObject.activeInHierarchy && m.enabled && !m.forceRenderingOff)
                        .Select(m => m.name).Distinct().Take(16))
                });
            else droppedDiagnosticFrames++;
        }

        static bool IsSpeech(string name) => name.Split('.').Last().StartsWith("speech_", StringComparison.Ordinal);
        float SpeechWeight()
        {
            float peak = 0;
            if (meshes == null) return peak;
            foreach (var mesh in meshes)
                if (mesh && mesh.gameObject.activeInHierarchy && mesh.enabled && !mesh.forceRenderingOff && mesh.sharedMesh)
                    for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                        if (IsSpeech(mesh.sharedMesh.GetBlendShapeName(i))) peak = Math.Max(peak, mesh.GetBlendShapeWeight(i));
            return peak;
        }
        void RecordStatus(string status) { if (statuses.Count < 100) statuses.Add(status); }
        void RecordPcm(byte[] bytes, int stream)
        {
            if (voice && stream != voice.StreamGeneration) return;
            packets++; accepted += bytes.Length / 2; lastPcmTime = Time.realtimeSinceStartup;
            int remaining = MaxRecordingBytes - (int)pcm.Length;
            if (remaining > 0) pcm.Write(bytes, 0, Math.Min(bytes.Length, remaining));
        }
        void RecordProgress(int stream, long samples, int rate, bool starved, bool ended)
        {
            if (voice && stream == voice.StreamGeneration && rate == Rate)
            { consumed = Math.Max(consumed, samples); progressStarved = starved; progressEnded = ended; }
        }
        void RecordStop(int stream) { if (stream == generation) stopped = true; }
        void Before(float end, string phase)
        { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), phase + "; game=" + coordinator?.Status + "; voice=" + voice?.Status); }
        bool ValidScreenshot()
        {
            try { return File.Exists(screenshot) && new FileInfo(screenshot).Length > 24 && File.ReadAllBytes(screenshot).Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }); }
            catch (IOException) { return false; }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            observing = false;
            try
            {
                if (voice)
                {
                    voice.Leave();
                    float closeEnd = Math.Min(deadline, Time.realtimeSinceStartup + 17);
                    while (voice.IsClosing && Time.realtimeSinceStartup < closeEnd) yield return null;
                }
                if (evidence != null)
                {
                    File.WriteAllText(Path.Combine(evidence, "result.json"), new JObject {
                        ["passed"] = passed, ["utc"] = DateTime.UtcNow.ToString("O"),
                        ["scene"] = "BeforeTheDrop", ["npc"] = "ren", ["relay"] = "ws://127.0.0.1:8790/game",
                        ["acceptedPcmPackets"] = packets, ["acceptedSamples"] = accepted, ["consumedSamples"] = consumed,
                        ["movingPlaybackFrames"] = movingFrames, ["peakSpeechWeight"] = maxSpeech,
                        ["observationPhase"] = "LateUpdate order 200, after Ren compositor order 100",
                        ["observedFrames"] = observedFrames, ["nonzeroSpeechFrames"] = nonzeroFrames,
                        ["droppedDiagnosticFrames"] = droppedDiagnosticFrames, ["frames"] = frames,
                        ["microphoneObserved"] = microphoneObserved, ["finalUsageConfirmed"] = voice && voice.FinalUsageConfirmed,
                        ["streamStopped"] = stopped, ["remainingSpeechWeight"] = SpeechWeight(), ["screenshotPresent"] = ValidScreenshot(),
                        ["seconds"] = Time.realtimeSinceStartup - began, ["statuses"] = new JArray(statuses),
                        ["scope"] = "Real provider PCM through production encounter binding and actual Ren mesh. Renderer consumption does not prove acoustic output or subjective conversation quality."
                    }.ToString());
                    if (pcm.Length > 0)
                    {
                        using var writer = new BinaryWriter(File.Create(Path.Combine(evidence, "provider-output-first-15s.wav")));
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + (int)pcm.Length);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                        writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write((int)pcm.Length); writer.Write(pcm.ToArray());
                    }
                }
            }
            finally
            {
                if (recorder) { recorder.Observe = null; UnityEngine.Object.Destroy(recorder.gameObject); }
                if (voice)
                {
                    voice.StatusChanged -= RecordStatus; voice.Pcm16Output -= RecordPcm;
                    voice.PlaybackProgress -= RecordProgress; voice.StreamStopped -= RecordStop;
                }
                if (coordinator) coordinator.Disconnect();
                pcm.Dispose();
            }
        }
    }
}

