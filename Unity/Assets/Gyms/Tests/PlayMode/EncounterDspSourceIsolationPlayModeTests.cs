using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterDspSourceIsolationPlayModeTests
    {
        [UnityTest]
        public IEnumerator DedicatedVoiceDspIsIndependentOfMusicSourcesAndListenerOnCoordinator()
        {
            var host = new GameObject("Encounter coordinator with shared music sources");
            host.AddComponent<AudioListener>();
            var configured = host.AddComponent<AudioSource>();
            configured.playOnAwake = false; configured.volume = .37f; configured.mute = false;
            configured.panStereo = -.2f; configured.priority = 47;
            var musicA = host.AddComponent<AudioSource>();
            var musicB = host.AddComponent<AudioSource>();
            var voice = host.AddComponent<EncounterVoiceController>();
            voice.Output = configured;
            int rate = AudioSettings.outputSampleRate;
            var music = AudioClip.Create("Unrelated silent nightclub music", rate * 2, 1, rate, false);
            var carrier = AudioClip.Create("Isolated silent DSP carrier", rate, 1, rate, false);
            var queue = new DspPcmPlayback(72000, rate);
            EncounterDspOutput filter = null;
            var ensure = typeof(EncounterVoiceController).GetMethod("EnsureDedicatedOutput", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Assert.That(ensure, Is.Not.Null);
                musicA.clip = music; musicA.loop = true; musicA.volume = .12f; musicA.Play();
                musicB.clip = music; musicB.loop = true; musicB.volume = .08f; musicB.Play();
                Assert.That((bool)ensure.Invoke(voice, null), Is.True);
                var output = voice.Output;
                Assert.That(output, Is.Not.SameAs(configured));
                Assert.That(output.transform.parent, Is.SameAs(configured.transform));
                Assert.That(output.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                Assert.That(output.GetComponent<AudioListener>(), Is.Null);
                Assert.That(host.GetComponent<EncounterDspOutput>(), Is.Null);
                Assert.That(output.volume, Is.EqualTo(configured.volume));
                Assert.That(output.mute, Is.EqualTo(configured.mute));
                Assert.That(output.outputAudioMixerGroup, Is.SameAs(configured.outputAudioMixerGroup));
                Assert.That(output.panStereo, Is.EqualTo(configured.panStereo));
                Assert.That(output.priority, Is.EqualTo(configured.priority));
                Assert.That((bool)ensure.Invoke(voice, null), Is.True);
                Assert.That(voice.Output, Is.SameAs(output), "Conversation retries reuse one isolated source.");
                Assert.That(host.transform.childCount, Is.EqualTo(1));
                var pcm = new byte[96000];
                for (int i = 0; i < pcm.Length / 2; i++)
                {
                    short sample = (short)(Math.Sin(i * 2 * Math.PI * 220 / 24000) * 3000);
                    pcm[i * 2] = (byte)sample; pcm[i * 2 + 1] = (byte)(sample >> 8);
                }
                Assert.That(queue.TryWritePcm16(pcm), Is.True);
                filter = output.gameObject.AddComponent<EncounterDspOutput>(); filter.Bind(queue);
                output.clip = carrier; output.loop = true; output.Play();
                int progressing = 0; long previous = 0, largest = 0;
                float deadline = Time.realtimeSinceStartup + 8;
                while (queue.Snapshot().ConsumedSamples < 24000)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                    long consumed = queue.Snapshot().ConsumedSamples;
                    if (consumed > previous) progressing++;
                    largest = Math.Max(largest, consumed - previous); previous = consumed;
                }
                Assert.That(progressing, Is.GreaterThanOrEqualTo(10));
                Assert.That(largest, Is.LessThan(6000));
                Assert.That(musicA.isPlaying && musicB.isPlaying, Is.True);
                Assert.That(musicA.timeSamples, Is.GreaterThan(0));
                Assert.That(musicB.timeSamples, Is.GreaterThan(0));
                Assert.That(musicA.clip, Is.SameAs(music)); Assert.That(musicB.clip, Is.SameAs(music));
                Assert.That(musicA.volume, Is.EqualTo(.12f)); Assert.That(musicB.volume, Is.EqualTo(.08f));
                voice.enabled = false;
                long retired = queue.Snapshot().ConsumedSamples;
                Assert.That(queue.Snapshot().Active, Is.False, "Disabling the isolated host retires its DSP queue.");
                Assert.That(voice.Output, Is.SameAs(configured), "Restore the authored output reference on disable.");
                yield return null; yield return null;
                Assert.That(host.transform.childCount, Is.Zero, "No runtime source leak after disable.");
                Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(retired));
                Assert.That(musicA.isPlaying && musicB.isPlaying, Is.True);
                voice.enabled = true;
                Assert.That((bool)ensure.Invoke(voice, null), Is.True);
                Assert.That(voice.Output, Is.Not.SameAs(configured));
                Assert.That(host.transform.childCount, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
                Debug.Log($"DSP_SOURCE_ISOLATION_PASSED: progressingFrames={progressing}; largestConsumedStep={largest}");
            }
            finally
            {
                if (voice) voice.enabled = false;
                if (filter) filter.Unbind(queue);
                queue.Deactivate();
                musicA.Stop(); musicB.Stop();
                UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(music); UnityEngine.Object.Destroy(carrier);
            }
        }
    }
}
