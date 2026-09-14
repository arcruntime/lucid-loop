using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterDspOutputPlayModeTests
    {
        [UnityTest]
        public IEnumerator DspOutputConsumesInSmallStepsAndRetiredGenerationStaysSilent()
        {
            var host = new GameObject("DSP playback test");
            var source = host.AddComponent<AudioSource>();
            var listener = new GameObject("DSP playback listener");
            listener.AddComponent<AudioListener>();
            var filter = host.AddComponent<EncounterDspOutput>();
            int outputRate = AudioSettings.outputSampleRate;
            var queue = new DspPcmPlayback(72000, outputRate);
            var pcm = new byte[96000];
            for (int i = 0; i < pcm.Length / 2; i++)
            {
                short value = (short)(Math.Sin(i * 2 * Math.PI * 220 / 24000) * 3000);
                pcm[i * 2] = (byte)value; pcm[i * 2 + 1] = (byte)(value >> 8);
            }
            var carrier = AudioClip.Create("DSP test silent carrier", outputRate, 1, outputRate, false);
            try
            {
                Assert.That(queue.TryWritePcm16(pcm), Is.True);
                Assert.That(queue.Snapshot().ConsumedSamples, Is.Zero, "Accepting input must not advance output.");
                filter.Bind(queue);
                source.clip = carrier; source.loop = true; source.Play();
                long previous = 0, largestStep = 0;
                int progressingFrames = 0;
                float end = Time.realtimeSinceStartup + 8;
                while (queue.Snapshot().ConsumedSamples < 24000)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "DSP did not consume a second of audio.");
                    yield return null;
                    long consumed = queue.Snapshot().ConsumedSamples;
                    if (consumed > previous) progressingFrames++;
                    largestStep = Math.Max(largestStep, consumed - previous);
                    previous = consumed;
                }
                Assert.That(progressingFrames, Is.GreaterThanOrEqualTo(10), "Output clock must track rendered blocks, not 400ms clip reads.");
                Assert.That(largestStep, Is.LessThan(6000), "Output progress jumped at least 250ms in a rendered frame.");
                filter.Unbind(queue);
                var retired = queue.Snapshot();
                Assert.That(retired.Active, Is.False);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(queue.Snapshot().ConsumedSamples, Is.EqualTo(retired.ConsumedSamples));
                Assert.That(queue.TryWritePcm16(pcm), Is.False);
                source.Stop(); source.clip = null;
                LogAssert.NoUnexpectedReceived();
                Debug.Log($"DSP_OUTPUT_PASSED: progressingFrames={progressingFrames}; largestConsumedStep={largestStep}; outputRate={outputRate}");
            }
            finally
            {
                filter.Unbind(queue); source.Stop(); source.clip = null;
                UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(listener); UnityEngine.Object.Destroy(carrier);
            }
        }
    }
}
