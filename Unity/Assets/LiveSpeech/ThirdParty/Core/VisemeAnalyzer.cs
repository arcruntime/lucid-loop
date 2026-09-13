// Ported from HeadAudio modules/processor.mjs (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.
//
// Pipeline (identical to upstream Processor.process):
//   input samples -> mono downmix (Web Audio 'speakers' average) -> pre-emphasis
//   (0.97) -> polyphase resampler to 16 kHz (32-tap Hamming-sinc, 64 phases) ->
//   512-sample window / 256 hop -> MFCC(12) -> tanh compression -> VAD gate ->
//   Mahalanobis Gaussian-prototype classifier -> 6-frame majority vote.
//
// Precision mirrors upstream: arithmetic in double, float32 storage where upstream
// uses Float32Array (polyphase coeffs, block, feature vector, model arrays);
// ring buffers that upstream keeps as plain JS number arrays stay double here.

using System;
using System.Collections.Generic;

namespace SplatterfaceGames.LipSync
{
    /// <summary>
    /// Streaming audio-driven viseme analyzer. Accepts interleaved normalized PCM at
    /// any input sample rate >= 16 kHz; internally resamples to the model rate
    /// (16 kHz), extracts MFCC features per 32 ms window every 16 ms, and classifies
    /// against Gaussian prototypes. All state persists across chunk boundaries, so
    /// identical PCM yields identical frames regardless of chunking.
    /// </summary>
    public sealed class VisemeAnalyzer : IDisposable
    {
        /// <summary>
        /// Upstream internal viseme id -> contract Labels index.
        /// Upstream order: aa,E,I,O,U,PP,SS,TH,DD,FF,kk,nn,RR,CH,sil.
        /// Contract order:  sil,PP,FF,TH,DD,kk,CH,SS,nn,RR,aa,E,I,O,U.
        /// </summary>
        private static readonly int[] UpstreamToContract =
            { 10, 11, 12, 13, 14, 1, 7, 3, 4, 2, 5, 8, 9, 6, 0 };

        private readonly int _inputChannels;
        private readonly GaussianClassifier _classifier;
        private readonly Mfcc _mfcc;

        // Pre-emphasis / downsampling state (processor.mjs)
        private double _preemphasisPrevValue;
        private readonly double _downsampleRatio;
        private double _downsamplePhaseAccumulator;
        private readonly float[][] _downsamplePolyFilter;
        private readonly RingBuffer<double> _ringPolyFilter; // plain Array<number> upstream
        private readonly RingBuffer<float> _ringSamples;   // upstream stores doubles, but the
        // only consumer is the Float32Array block, so storing float at Add is identical.
        private readonly float[] _block;                   // Float32Array upstream
        private readonly RingBuffer<float[]> _ringVectors; // Array of Float32Array, full

        private readonly VadGate _vadGate;

        // Input frame assembly (for interleaved multi-channel input)
        private readonly float[] _pendingFrame;
        private int _pendingCount;

        private long _sampleCount; // downsampled samples emitted (upstream sampleCount)
        private bool _disposed;

        /// <summary>Input sample rate in Hz (must be >= 16000, as upstream).</summary>
        public int InputSampleRate { get; }

        /// <summary>Total input sample frames consumed since construction/Reset.</summary>
        public long InputSamplesSeen { get; private set; }

        public VisemeAnalyzer(GaussianModel model, int inputSampleRate, int inputChannels)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (inputChannels < 1) throw new ArgumentOutOfRangeException(nameof(inputChannels));
            // Upstream phase index overflows the 64-entry table for input rates below
            // the model rate, i.e. upstream does not support upsampling. Fail loudly.
            if (inputSampleRate < Parameters.AudioSampleRate)
                throw new ArgumentOutOfRangeException(nameof(inputSampleRate),
                    inputSampleRate, "Input rates below the 16 kHz model rate are unsupported upstream.");

            InputSampleRate = inputSampleRate;
            _inputChannels = inputChannels;
            _pendingFrame = new float[inputChannels];

            _downsampleRatio = (double)inputSampleRate / Parameters.AudioSampleRate;
            _downsamplePolyFilter = DesignPolyphaseFilter();
            _ringPolyFilter = new RingBuffer<double>(Parameters.AudioDownsampleFilterN, () => 0.0, true);
            _ringSamples = new RingBuffer<float>(Parameters.MfccSamplesN, () => 0f);
            _block = new float[Parameters.MfccSamplesN];
            _ringVectors = new RingBuffer<float[]>(3,
                () => new float[Parameters.MfccCoeffNWithDeltas], true);

            _mfcc = new Mfcc();
            _vadGate = new VadGate();
            _classifier = new GaussianClassifier();
            _classifier.Import(model);
        }

        /// <summary>
        /// Polyphase low-pass filter table for downsampling. Each phase is a
        /// Hamming-windowed sinc shifted by a fractional offset (processor.mjs).
        /// </summary>
        private static float[][] DesignPolyphaseFilter()
        {
            int filterN = Parameters.AudioDownsampleFilterN;
            int phaseN = Parameters.AudioDownsamplePhaseN;
            var table = new float[phaseN][];
            double mid = (filterN - 1) / 2.0;
            const double cutoff = 0.45; // normalized to Nyquist of output
            for (int p = 0; p < phaseN; p++)
            {
                double phase = (double)p / phaseN;
                var coeffs = new float[filterN];
                for (int i = 0; i < filterN; i++)
                {
                    double x = i - mid - phase;
                    double c = x == 0 ? 2 * cutoff : Math.Sin(2 * Math.PI * cutoff * x) / (Math.PI * x);
                    c *= 0.54 - 0.46 * Math.Cos((2 * Math.PI * i) / (filterN - 1)); // Hamming
                    coeffs[i] = (float)c;
                }
                table[p] = coeffs;
            }
            return table;
        }

        /// <summary>
        /// Feed interleaved normalized samples (-1..1) at the constructor's rate and
        /// channel count. Returns the viseme frames completed during this chunk
        /// (one per 16 ms model hop once the first 32 ms window fills).
        /// </summary>
        public List<VisemeFrame> ProcessChunk(ReadOnlySpan<float> interleaved)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisemeAnalyzer));

            var frames = new List<VisemeFrame>();
            int pos = 0;

            // Fast path: mono input with no pending partial frame.
            if (_inputChannels == 1 && _pendingCount == 0)
            {
                while (pos < interleaved.Length)
                {
                    InputSamplesSeen++;
                    ProcessSample(interleaved[pos++], frames);
                }
                return frames;
            }

            while (pos < interleaved.Length)
            {
                while (_pendingCount < _inputChannels && pos < interleaved.Length)
                    _pendingFrame[_pendingCount++] = interleaved[pos++];
                if (_pendingCount < _inputChannels) break; // incomplete frame, wait for more
                _pendingCount = 0;

                // Downmix to mono. Upstream receives mono Float32Array data from the
                // Web Audio graph ('speakers' interpretation averages channels), so the
                // mixed sample enters the pipeline at float32 precision.
                double mono = 0;
                for (int c = 0; c < _inputChannels; c++) mono += _pendingFrame[c];
                float monoF = (float)(mono / _inputChannels);

                InputSamplesSeen++;
                ProcessSample(monoF, frames);
            }
            return frames;
        }

        /// <summary>End of stream. All frames are emitted eagerly inside ProcessChunk,
        /// so there is no buffered tail to drain (same as upstream, which only
        /// analyzes complete windows). Returns an empty list.</summary>
        public List<VisemeFrame> Flush()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisemeAnalyzer));
            return new List<VisemeFrame>();
        }

        /// <summary>Return the analyzer to freshly-constructed state (all buffers,
        /// counters, VAD and classifier history cleared).</summary>
        public void Reset()
        {
            _preemphasisPrevValue = 0;
            _downsamplePhaseAccumulator = 0;
            _ringPolyFilter.ResetFilled(0.0);
            _ringSamples.Clear();
            Array.Clear(_block, 0, _block.Length);
            _ringVectors.Clear();
            _vadGate.Reset();
            _classifier.Reset();
            _pendingCount = 0;
            _sampleCount = 0;
            InputSamplesSeen = 0;
        }

        public void Dispose() => _disposed = true;

        // One mono input sample through pre-emphasis + polyphase resampler,
        // then window/MFCC/classify per model-rate hop. Mirrors Processor.process.
        private void ProcessSample(double input, List<VisemeFrame> frames)
        {
            double d0 = input;

            if (Parameters.AudioPreemphasisEnabled)
            {
                double dOrig = d0;
                d0 -= Parameters.AudioPreemphasisAlpha * _preemphasisPrevValue;
                _preemphasisPrevValue = dOrig;
            }

            _ringPolyFilter.Add(d0);
            _downsamplePhaseAccumulator += 1 / _downsampleRatio;
            while (_downsamplePhaseAccumulator >= 1)
            {
                double phaseFraction = (_downsamplePhaseAccumulator - 1) * Parameters.AudioDownsamplePhaseN;
                int phaseIndex = (int)Math.Floor(phaseFraction);
                if (phaseIndex >= Parameters.AudioDownsamplePhaseN)
                    phaseIndex = Parameters.AudioDownsamplePhaseN - 1; // unreachable for rate>=16k
                float[] coeffs = _downsamplePolyFilter[phaseIndex];

                double d1 = 0;
                RingBuffer<double> filter = _ringPolyFilter;
                for (int j = 0; j < Parameters.AudioDownsampleFilterN; j++)
                    d1 += filter.GetHead(j) * coeffs[j];

                _downsamplePhaseAccumulator -= 1;

                _ringSamples.Add((float)d1);
                _sampleCount++;

                if (_ringSamples.IsFull())
                {
                    float[] block = _block;
                    _ringSamples.GetLatest(block, Parameters.MfccSamplesHop);

                    float[] v = _ringVectors.Allocate();
                    _mfcc.Compute(block, v, out double le);

                    if (Parameters.MfccCompressionEnabled)
                    {
                        double scale = 1.0 / Parameters.MfccCompressionTanhR;
                        for (int j = 0; j < Parameters.MfccCoeffN; ++j)
                        {
                            double y = v[j] * scale;
                            v[j] = (float)(Parameters.MfccCompressionTanhR * Math.Tanh(y));
                        }
                    }

                    // MFCC deltas are disabled upstream (parameters.mjs) — skipped identically.

                    var (_, inactive) = _vadGate.Process(le);

                    if (inactive != 0)
                    {
                        // Upstream posts 'ended' here and skips classification; the
                        // effective output is silence for this window.
                        frames.Add(MakeFrame(_sampleCount, Parameters.ModelVisemeSil, null));
                        continue;
                    }

                    int? viseme = _classifier.Predict(v, out double[] distances);
                    frames.Add(MakeFrame(_sampleCount, viseme ?? Parameters.ModelVisemeSil, distances));
                }
            }
        }

        // Map an upstream frame result to the contract VisemeFrame.
        private VisemeFrame MakeFrame(long sampleCount, int upstreamViseme, double[]? distances)
        {
            int labelIndex = UpstreamToContract[upstreamViseme];
            var frame = new VisemeFrame
            {
                LabelIndex = labelIndex,
                Label = VisemeLabels.Labels[labelIndex],
                // Window center on the input timeline (upstream event time
                // t = sampleCount/rate - vectorDuration/2, i.e. center of the 512-sample window).
                TimeMs = (sampleCount - Parameters.MfccSamplesHop) * 1000.0 / Parameters.AudioSampleRate
            };
            frame.InputSamplePos = (long)Math.Round(
                frame.TimeMs * InputSampleRate / 1000.0, MidpointRounding.AwayFromZero);

            float[] w = frame.Weights;
            if (distances == null)
            {
                // VAD-inactive: upstream emits no distances; output is silence.
                w[0] = 1f; // sil
                frame.Confidence = 1f;
                return frame;
            }

            ComputeWeights(distances, w);
            frame.Confidence = w[labelIndex];
            return frame;
        }

        /// <summary>
        /// Per-viseme distribution from per-prototype Mahalanobis distances.
        /// Same derivation as the reference harness (harness/engine.mjs
        /// weightsFromDistances): d_v = min distance among prototypes of viseme v
        /// (distances already include upstream's /silSensitivity scaling), then
        /// w_v = exp(-(d_v - dmin)/2) normalized over the 15 labels — a softmax over
        /// squared-Mahalanobis likelihoods with equal priors. All-zero when every
        /// likelihood underflows, matching the reference convention.
        /// </summary>
        private void ComputeWeights(double[] distances, float[] w)
        {
            var minD = new double[Parameters.ModelVisemesN];
            for (int i = 0; i < minD.Length; i++) minD[i] = double.PositiveInfinity;
            var protos = _classifier.Prototypes;
            for (int i = 0; i < distances.Length; i++)
            {
                int v = protos[i].Viseme;
                if (distances[i] < minD[v]) minD[v] = distances[i];
            }
            double dmin = double.PositiveInfinity;
            for (int v = 0; v < Parameters.ModelVisemesN; v++)
                if (minD[v] < dmin) dmin = minD[v];
            var scores = new double[Parameters.ModelVisemesN];
            double sum = 0;
            for (int v = 0; v < Parameters.ModelVisemesN; v++)
            {
                double d = minD[v];
                scores[v] = double.IsInfinity(d) ? 0 : Math.Exp(-(d - dmin) / 2);
                sum += scores[v];
            }
            for (int v = 0; v < Parameters.ModelVisemesN; v++)
                w[UpstreamToContract[v]] = sum > 0 ? (float)(scores[v] / sum) : 0f;
        }

        // Exposed for tests/diagnostics.
        internal GaussianClassifier Classifier => _classifier;
    }
}
