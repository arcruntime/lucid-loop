using System;
using System.Collections.Generic;
using System.IO;
using LucidLoop.CharacterArt;
using SplatterfaceGames.LipSync;
using UnityEngine;

namespace LucidLoop.LiveSpeech
{
    /// <summary>Native output PCM -> pinned English analyzer -> artist-owned speech snapshot.
    /// All methods run on Unity's main thread. This component does not own audio playback.</summary>
    public sealed class LiveSpeechFaceAdapter : MonoBehaviour
    {
        public CharacterFaceDriver Face;
        [Min(0)] public float OutputLatencyMs = 40;
        readonly float[] weights = new float[EnglishSpeechStream.LabelCount];
        readonly List<SpeechPoseWeight> poses = new List<SpeechPoseWeight>(EnglishSpeechStream.LabelCount);
        EnglishSpeechStream stream;
        long generation;
        public string Diagnostic { get; private set; } = string.Empty;
        public bool IsSupported { get; private set; }

        public bool BeginStream(long streamGeneration, SpeechLanguage language)
        {
            ResetSpeech(); generation = streamGeneration;
            if (language != SpeechLanguage.English)
            {
                Diagnostic = "LANGUAGE_UNSUPPORTED: live Japanese producer is not installed; no English fallback applied.";
                return false;
            }
            try
            {
                var asset = Resources.Load<TextAsset>("LiveSpeech/model-en-mixed");
                if (!asset) { Diagnostic = "MODEL_MISSING: LiveSpeech/model-en-mixed"; return false; }
                using var bytes = new MemoryStream(asset.bytes, false);
                stream = new EnglishSpeechStream(GaussianModel.Load(bytes));
                stream.Begin(generation);
                IsSupported = true; Diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            { ResetSpeech(); Diagnostic = "MODEL_LOAD_FAILED: " + exception.GetType().Name; return false; }
        }

        /// <summary>Call only for packets accepted by the playback queue, in identical order.</summary>
        public bool PushPcm16(byte[] bytes, long streamGeneration)
        {
            if (!IsSupported || streamGeneration != generation) return false;
            if (stream.PushPcm16(bytes, streamGeneration)) return true;
            Diagnostic = stream.Diagnostic;
            IsSupported = false; if (Face) Face.ResetSpeech();
            return false;
        }

        public void UpdatePlayback(long consumedSamples, long streamGeneration, bool starved, bool ended)
        {
            if (streamGeneration != generation) return;
            if (!IsSupported) return;
            if (!Face) { Diagnostic = "FACE_UNBOUND: bind the active authored CharacterFaceDriver."; return; }
            if (!stream.Sample(consumedSamples, streamGeneration, OutputLatencyMs, starved, ended, weights))
            {
                Face.ResetSpeech();
                if (!string.IsNullOrEmpty(stream.Diagnostic)) { Diagnostic = stream.Diagnostic; IsSupported = false; }
                return;
            }
            poses.Clear();
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0) continue;
                if (!CanonicalSpeechPoseMap.TryMap(i, SpeechLanguage.English, weights[i], out var pose))
                { Face.ResetSpeech(); Diagnostic = "POSE_MAPPING_FAILED: " + i; return; }
                poses.Add(pose);
            }
            if (!Face.ApplySpeechFrame(poses)) Diagnostic = Face.LastSpeechDiagnostic;
            else Diagnostic = string.Empty;
        }

        public void ResetSpeech()
        {
            stream?.Dispose(); stream = null; IsSupported = false;
            poses.Clear(); if (Face) Face.ResetSpeech();
        }
        void OnDisable() => ResetSpeech();
    }
}
