// Ported from HeadAudio modules/training.mjs (loadModel/decodeBinaryRecord) and
// modules/parameters.mjs record layout constants.
// (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace SplatterfaceGames.LipSync
{
    /// <summary>One Gaussian prototype record decoded from the model binary.</summary>
    public sealed class GaussianPrototype
    {
        /// <summary>IPA phoneme (0–2 code points packed in the record header).</summary>
        public string Phoneme = "";
        /// <summary>Prototype group id (header byte 5).</summary>
        public int Group;
        /// <summary>Upstream viseme id 0..14, HeadAudio internal order (aa=0 … sil=14).</summary>
        public int Viseme;
        /// <summary>Mean vector, length MFCC_COEFF_N_WITH_DELTAS (12).</summary>
        public float[] Mu = Array.Empty<float>();
        /// <summary>Packed lower-triangular inverse covariance, length 78.</summary>
        public float[] SigmaInvLower = Array.Empty<float>();
    }

    /// <summary>
    /// Pre-trained Gaussian prototype model. Parses the HeadAudio binary format:
    /// consecutive fixed-size records of RECORD_OFFSET (368) bytes each.
    /// Header (8 bytes): phoneme code points as a big-endian uint32 (cp1&lt;&lt;16|cp2),
    /// group at byte 5, viseme at byte 7. Then mu[12] and sigmaInvLower[78] as
    /// little-endian float32.
    /// </summary>
    public sealed class GaussianModel
    {
        private readonly GaussianPrototype[] _prototypes;

        private GaussianModel(GaussianPrototype[] prototypes) => _prototypes = prototypes;

        /// <summary>Prototypes in file order.</summary>
        public IReadOnlyList<GaussianPrototype> Prototypes => _prototypes;

        /// <summary>Number of prototypes (39 for the bundled en-mixed model).</summary>
        public int Count => _prototypes.Length;

        /// <summary>Parse a model file (e.g. model-en-mixed.bin).</summary>
        public static GaussianModel Load(string path)
        {
            using var fs = File.OpenRead(path);
            return Load(fs);
        }

        /// <summary>Parse a model from a stream.</summary>
        public static GaussianModel Load(Stream s)
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            byte[] buf = ms.ToArray();
            int len = buf.Length;

            var model = new List<GaussianPrototype>();
            int pos = 0;
            while (pos + Parameters.RecordOffset <= len)
            {
                model.Add(DecodeRecord(buf, pos));
                pos += Parameters.RecordOffset;
            }
            if (model.Count == 0)
                throw new InvalidDataException("Model is empty");
            return new GaussianModel(model.ToArray());
        }

        private static GaussianPrototype DecodeRecord(byte[] buf, int pos)
        {
            // Header: DataView in upstream reads big-endian for the packed phoneme.
            uint phPacked = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(pos, 4));
            uint ph1 = phPacked >> 16;
            uint ph2 = phPacked & 0xFFFF;
            string phoneme = ph2 == 0
                ? char.ConvertFromUtf32((int)ph1)
                : char.ConvertFromUtf32((int)ph1) + char.ConvertFromUtf32((int)ph2);
            int group = buf[pos + 5];
            int viseme = buf[pos + 7];

            var mu = new float[Parameters.RecordMuLen];
            for (int i = 0; i < mu.Length; i++)
                mu[i] = BinaryPrimitives.ReadInt32LittleEndian(
                    buf.AsSpan(pos + Parameters.RecordMuOffset + i * 4, 4)).Int32ToSingle();

            var sigma = new float[Parameters.RecordSigmaInvLowerLen];
            for (int i = 0; i < sigma.Length; i++)
                sigma[i] = BinaryPrimitives.ReadInt32LittleEndian(
                    buf.AsSpan(pos + Parameters.RecordSigmaInvLowerOffset + i * 4, 4)).Int32ToSingle();

            return new GaussianPrototype
            {
                Phoneme = phoneme,
                Group = group,
                Viseme = viseme,
                Mu = mu,
                SigmaInvLower = sigma
            };
        }
    }

    internal static class BitHelpers
    {
        // BitConverter.Int32BitsToSingle equivalent, kept explicit for netstandard2.1.
        public static float Int32ToSingle(this int i) => BitConverter.Int32BitsToSingle(i);
    }
}
