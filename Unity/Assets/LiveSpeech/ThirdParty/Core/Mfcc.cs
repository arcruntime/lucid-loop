// Ported from HeadAudio modules/mfcc.mjs (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.
//
// Numeric policy: upstream JavaScript computes every arithmetic chain in IEEE-754
// double precision and truncates to float32 only when a value is stored into a
// Float32Array. This port mirrors that exactly: arrays marked float[] correspond to
// Float32Array storage; all intermediate arithmetic is done on doubles and cast to
// float at the point of store.

using System;

namespace SplatterfaceGames.LipSync
{
    /// <summary>Mel-frequency cepstral coefficients, ported 1:1 from mfcc.mjs.</summary>
    internal sealed class Mfcc
    {
        private readonly float[] _cossin;      // Float32Array
        private readonly int[] _bitrev;        // Uint32Array
        private readonly float[] _spectrum;    // Float32Array, complex interleaved
        private readonly float[] _powerSpec;   // Float32Array
        private readonly float[][] _melFilters; // Float32Array per band
        private readonly float[] _melEnergies; // Float32Array
        private readonly float[] _hamming;     // Float32Array
        private readonly float[][] _dctMatrix; // rows 0..MFCC_COEFF_N
        private readonly float[] _lifter;      // Float32Array

        public Mfcc(double speakerMeanHz = 150)
        {
            _cossin = BuildCosSin();
            _bitrev = BuildBitrev();
            _spectrum = new float[2 * Parameters.MfccSamplesN];
            _powerSpec = new float[Parameters.MfccSamplesN / 2];
            _melFilters = new float[Parameters.MfccMelBandsN][];
            for (int i = 0; i < _melFilters.Length; i++)
                _melFilters[i] = new float[Parameters.MfccSamplesN / 2];
            BuildMelFilterbank(speakerMeanHz);
            _melEnergies = new float[Parameters.MfccMelBandsN];
            _hamming = BuildWindowHamming();
            _dctMatrix = BuildDctMatrix();
            _lifter = BuildLifter();
        }

        private static float[] BuildCosSin()
        {
            int n = Parameters.MfccSamplesN;
            var t = new float[2 * n];
            double theta = (-2 * Math.PI) / n;
            for (int i = 0; i < n; i++)
            {
                double phase = theta * i;
                t[2 * i] = (float)Math.Cos(phase);
                t[2 * i + 1] = (float)Math.Sin(phase);
            }
            return t;
        }

        private static int[] BuildBitrev()
        {
            int n = Parameters.MfccSamplesN;
            var bitrev = new int[n];
            int bits = 0; // Math.Log2(n) — netstandard2.1 lacks Math.Log2
            for (int t = n; t > 1; t >>= 1) bits++;
            for (int i = 0; i < n; i++)
            {
                int j = 0;
                for (int k = 0; k < bits; k++)
                    j = (j << 1) | ((i >> k) & 1);
                bitrev[i] = j;
            }
            return bitrev;
        }

        // In-place radix-2 Cooley–Tukey FFT on real input; includes Hamming window.
        private void HammingAndFft(float[] realInput)
        {
            int n = Parameters.MfccSamplesN;
            float[] cossin = _cossin;
            int[] bitrev = _bitrev;
            float[] hamming = _hamming;
            float[] spectrum = _spectrum;

            for (int i = 0; i < n; i++)
            {
                int idx = bitrev[i];
                spectrum[2 * idx] = (float)(realInput[i] * hamming[i]);
                spectrum[2 * idx + 1] = 0f;
            }

            for (int i = 1; i < n; i <<= 1)
            {
                int step = i << 1;
                int delta = n / step;
                for (int k = 0; k < i; k++)
                {
                    int twBase = 2 * (k * delta);
                    double twR = cossin[twBase];
                    double twI = cossin[twBase + 1];
                    for (int j = k; j < n; j += step)
                    {
                        int i0 = j << 1;
                        int i1 = (j + i) << 1;
                        double s0R = spectrum[i0];
                        double s0I = spectrum[i0 + 1];
                        double s1R = spectrum[i1];
                        double s1I = spectrum[i1 + 1];
                        double v1R = s1R * twR - s1I * twI;
                        double v1I = s1R * twI + s1I * twR;
                        spectrum[i0] = (float)(s0R + v1R);
                        spectrum[i0 + 1] = (float)(s0I + v1I);
                        spectrum[i1] = (float)(s0R - v1R);
                        spectrum[i1 + 1] = (float)(s0I - v1I);
                    }
                }
            }
        }

        private static float[] BuildWindowHamming()
        {
            int n = Parameters.MfccSamplesN;
            var w = new float[n];
            for (int i = 0; i < n; i++)
                w[i] = (float)(0.54 - 0.46 * Math.Cos((2 * Math.PI * i) / (n - 1)));
            return w;
        }

        private void BuildMelFilterbank(double f0)
        {
            float[][] filters = _melFilters;
            const double f0Ref = 150;
            double warp = Math.Min(Math.Max(f0 / f0Ref, 0.6), 1.8);
            const double lowFreq = 30;
            const double highFreq = 7800;
            Func<double, double> mel = f => 2595 * Math.Log10(1 + f / 700);
            Func<double, double> melInv = m => 700 * (Math.Pow(10, m / 2595) - 1);
            double melLow = mel(lowFreq);
            double melHigh = mel(highFreq);

            int bandN = Parameters.MfccMelBandsN;
            var melPoints = new double[bandN + 2];
            double melRange = melHigh - melLow;
            for (int i = 0; i < bandN + 2; i++)
            {
                double m = melLow + (melRange / (bandN + 1)) * i;
                double mWarped = melLow + (m - melLow) * warp;
                melPoints[i] = melInv(mWarped);
            }

            var bin = new int[bandN + 2];
            for (int i = 0; i < bandN + 2; i++)
                bin[i] = (int)Math.Floor((melPoints[i] / Parameters.AudioSampleRate) * Parameters.MfccSamplesN);

            for (int i = 0; i < bandN; i++)
            {
                float[] fbank = filters[i];
                for (int k = bin[i]; k < bin[i + 1]; k++)
                    fbank[k] = (float)((double)(k - bin[i]) / (bin[i + 1] - bin[i]));
                for (int k = bin[i + 1]; k < bin[i + 2]; k++)
                    fbank[k] = (float)((double)(bin[i + 2] - k) / (bin[i + 2] - bin[i + 1]));
            }
        }

        private static float[][] BuildDctMatrix()
        {
            int bandN = Parameters.MfccMelBandsN;
            double scale = Math.Sqrt(2.0 / bandN);
            var mat = new float[Parameters.MfccCoeffN + 1][];
            for (int i = 0; i <= Parameters.MfccCoeffN; i++)
            {
                var row = new float[bandN];
                for (int j = 0; j < bandN; j++)
                    row[j] = (float)(scale * Math.Cos((Math.PI * i * (j + 0.5)) / bandN));
                mat[i] = row;
            }
            return mat;
        }

        private static float[] BuildLifter()
        {
            var lifter = new float[Parameters.MfccCoeffN];
            for (int i = 1; i <= Parameters.MfccCoeffN; i++)
                lifter[i - 1] = (float)(1 + (Parameters.MfccLifter / 2.0) * Math.Sin((Math.PI * i) / Parameters.MfccLifter));
            return lifter;
        }

        /// <summary>
        /// Compute MFCC feature vector. Returns log energy via <paramref name="le"/>;
        /// the 12 coefficients (excluding log energy) are written to <paramref name="outVec"/>.
        /// </summary>
        public void Compute(float[] block, float[] outVec, out double le)
        {
            int n2 = Parameters.MfccSamplesN / 2;

            HammingAndFft(block);

            float[] spectrum = _spectrum;
            float[] power = _powerSpec;
            double totalEnergy = 0;
            for (int k = 0; k < n2; k++)
            {
                double re = spectrum[2 * k];
                double im = spectrum[2 * k + 1];
                float p = (float)((re * re + im * im) / Parameters.MfccSamplesN);
                power[k] = p;
                totalEnergy += p;
            }

            le = Math.Log10(totalEnergy + 1e-10);

            float[][] filters = _melFilters;
            float[] energies = _melEnergies;
            for (int m = 0; m < Parameters.MfccMelBandsN; m++)
            {
                float[] f = filters[m];
                double sum = 0;
                for (int k = 0; k < f.Length; k++)
                    sum += (double)power[k] * f[k];
                energies[m] = (float)Math.Log10(sum + 1e-10);
            }

            float[][] dct = _dctMatrix;
            float[] lifter = _lifter;
            for (int i = 0; i < Parameters.MfccCoeffN; i++)
            {
                float[] row = dct[i + 1];
                double sum = 0;
                for (int j = 0; j < Parameters.MfccMelBandsN; j++)
                    sum += (double)row[j] * energies[j];
                outVec[i] = (float)(sum * lifter[i]);
            }
        }
    }
}
