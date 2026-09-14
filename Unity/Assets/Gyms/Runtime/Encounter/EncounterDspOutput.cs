using System;
using System.Threading;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // Owns no Unity lifecycle or network state. The AudioSource runs a silent,
    // non-streaming clip; this filter supplies live PCM in actual DSP blocks.
    public sealed class EncounterDspOutput : MonoBehaviour
    {
        DspPcmPlayback playback;
        public void Bind(DspPcmPlayback stream)
        {
            var previous = Interlocked.Exchange(ref playback, stream);
            if (!ReferenceEquals(previous, stream)) previous?.Deactivate();
        }
        public void Unbind(DspPcmPlayback stream)
        {
            Interlocked.CompareExchange(ref playback, null, stream);
            stream?.Deactivate();
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            var stream = Volatile.Read(ref playback);
            if (stream == null) { Array.Clear(data, 0, data.Length); return; }
            stream.Render(data, channels);
        }
        void OnDisable() => Interlocked.Exchange(ref playback, null)?.Deactivate();
    }
}
