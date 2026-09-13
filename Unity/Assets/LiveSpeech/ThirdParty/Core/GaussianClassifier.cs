// Ported from HeadAudio modules/classifier.mjs (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.

using System;

namespace SplatterfaceGames.LipSync
{
    /// <summary>
    /// Gaussian prototype model with Mahalanobis classifier. Ported 1:1, including
    /// the 6-slot majority-vote ring and its highest-index-wins tie-breaking.
    /// </summary>
    internal sealed class GaussianClassifier
    {
        private GaussianPrototype[] _prototypes = Array.Empty<GaussianPrototype>();
        private int _prototypesN;

        private readonly float[] _vectorDiff = new float[Parameters.MfccCoeffNWithDeltas];

        private double[] _distances = Array.Empty<double>();
        private readonly RingBuffer<int> _ringPredictions =
            new RingBuffer<int>(6, () => Parameters.ModelVisemeSil, true);
        private readonly int[] _counts = new int[Parameters.ModelVisemesN];
        private int _predictionLast = Parameters.ModelVisemeSil;
        private readonly double _silSensitivity;

        public GaussianClassifier(double silSensitivity = 1.2)
        {
            _silSensitivity = silSensitivity;
        }

        /// <summary>Loaded prototypes (upstream id order as stored in the model file).</summary>
        public GaussianPrototype[] Prototypes => _prototypes;

        /// <summary>Packed lower-triangular index for (i,j); symmetric like upstream lookup.</summary>
        private static int SigmaIndex(int i, int j)
        {
            if (j > i) (i, j) = (j, i);
            return i * (i + 1) / 2 + j;
        }

        public void Import(GaussianModel model, bool reset = true)
        {
            if (reset)
            {
                _prototypes = Array.Empty<GaussianPrototype>();
                _prototypesN = 0;
            }
            // Upstream supports group-filtered merging; the port loads one model set.
            var list = new GaussianPrototype[_prototypesN + model.Count];
            Array.Copy(_prototypes, list, _prototypesN);
            for (int i = 0; i < model.Count; i++)
                list[_prototypesN + i] = model.Prototypes[i];
            _prototypes = list;
            _prototypesN = list.Length;
            _distances = new double[_prototypesN];
            for (int i = 0; i < _prototypesN; i++) _distances[i] = double.PositiveInfinity;
        }

        public void Reset()
        {
            for (int i = 0; i < 6; i++) _ringPredictions.Buf[i] = Parameters.ModelVisemeSil;
            _predictionLast = Parameters.ModelVisemeSil;
            Array.Clear(_counts, 0, _counts.Length);
        }

        /// <summary>Mahalanobis distance between v and a prototype.</summary>
        public double DistanceMahalanobis(float[] v, float[] mu, float[] sigmaInvLower)
        {
            int n = Parameters.MfccCoeffNWithDeltas;
            float[] diff = _vectorDiff;
            for (int i = 0; i < n; ++i)
                diff[i] = v[i] - mu[i];

            double d = 0;
            for (int i = 0; i < n; ++i)
            {
                int ii = SigmaIndex(i, i);
                double diffI = diff[i];
                double sum = (double)sigmaInvLower[ii] * diffI * diffI;
                for (int j = 0; j < i; ++j)
                {
                    int ij = SigmaIndex(i, j);
                    sum += 2 * (double)sigmaInvLower[ij] * diffI * diff[j];
                }
                d += sum;
            }
            return d;
        }

        /// <summary>
        /// Predict the viseme for a feature vector.
        /// Returns null when the majority vote repeats silence, matching upstream.
        /// </summary>
        public int? Predict(float[] vector, out double[] distances)
        {
            distances = _distances;
            int n = _prototypesN;
            if (n == 0) return null;

            double minD = double.PositiveInfinity;
            int minP = 0;
            for (int i = 0; i < n; i++)
            {
                GaussianPrototype p = _prototypes[i];
                double d = DistanceMahalanobis(vector, p.Mu, p.SigmaInvLower);
                if (p.Viseme == Parameters.ModelVisemeSil)
                    d /= _silSensitivity;
                distances[i] = d;
                if (d <= minD) { minD = d; minP = i; }
            }

            // Majority vote over the last 6 raw predictions.
            RingBuffer<int> ring = _ringPredictions;
            int minV = _prototypes[minP].Viseme;
            ring.Add(minV);
            int m = ring.Capacity;
            int[] counts = _counts;
            Array.Clear(counts, 0, counts.Length);
            for (int i = 0; i < m; i++)
                counts[ring.Buf[i]]++;
            int viseme = 0;
            int maxCount = 0;
            for (int i = 0; i < Parameters.ModelVisemesN; i++)
            {
                int count = counts[i];
                if (count >= maxCount) { viseme = i; maxCount = count; }
            }

            if (viseme == _predictionLast && viseme == Parameters.ModelVisemeSil)
            {
                distances = _distances;
                return null;
            }
            _predictionLast = viseme;
            return viseme;
        }
    }
}
