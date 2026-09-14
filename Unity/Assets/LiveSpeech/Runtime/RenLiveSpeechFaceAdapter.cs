using System;
using System.Collections.Generic;
using System.IO;
using SplatterfaceGames.LipSync;
using UnityEngine;

namespace LucidLoop.LiveSpeech
{
    /// <summary>Main-thread adapter for Ren's assembled speech_* schema; never owns blink or emotion keys.</summary>
    public sealed class RenLiveSpeechFaceAdapter : MonoBehaviour
    {
        public SkinnedMeshRenderer[] Renderers = Array.Empty<SkinnedMeshRenderer>();
        public LucidLoop.CharacterArt.RenLOD0Controller FaceController;
        [Min(0)] public float OutputLatencyMs = 40;
        // Acoustic DD/NN share the closest authored tongue-contact L shape; KK uses a narrow opening.
        // The pinned 15-label model does not distinguish English L acoustically.
        static readonly string[] Names = { null, "speech_MBP", "speech_FV", "speech_TH", "speech_L", "speech_I", "speech_CH_SH_J", "speech_SZ", "speech_L", "speech_R", "speech_A", "speech_E", "speech_I", "speech_O", "speech_U" };
        [NonSerialized] readonly float[] weights = new float[EnglishSpeechStream.LabelCount];
        [NonSerialized] readonly Dictionary<string, float> mapped = new Dictionary<string, float>();
        [NonSerialized] readonly List<Binding> bindings = new List<Binding>();
        [NonSerialized] SkinnedMeshRenderer[] boundRenderers = Array.Empty<SkinnedMeshRenderer>();
        [NonSerialized] Mesh[] boundMeshes = Array.Empty<Mesh>();
        struct Binding { public SkinnedMeshRenderer Renderer; public int Index; public string SemanticName; }
        [NonSerialized] EnglishSpeechStream stream;
        [NonSerialized] GaussianModel model;
        [NonSerialized] long generation;
        public bool IsSupported => stream != null;
        public int BoundSpeechShapeCount { get { EnsureBindings(); return bindings.Count; } }
        public string Diagnostic { get; private set; } = string.Empty;
        public bool BeginStream(long streamGeneration)
        {
            ResetSpeech(); generation = streamGeneration;
            try
            {
                if (model == null)
                {
                    var asset = Resources.Load<TextAsset>("LiveSpeech/model-en-mixed");
                    if (!asset) { Diagnostic = "English speech model missing."; return false; }
                    using var bytes = new MemoryStream(asset.bytes, false);
                    model = GaussianModel.Load(bytes);
                }
                stream = new EnglishSpeechStream(model); stream.Begin(generation);
                Diagnostic = string.Empty; return true;
            }
            catch (Exception e) { ResetSpeech(); Diagnostic = "English model failed: " + e.GetType().Name; return false; }
        }
        public bool PushPcm16(byte[] pcm, long streamGeneration)
        {
            if (stream == null || generation != streamGeneration) return false;
            if (stream.PushPcm16(pcm, streamGeneration)) return true;
            Diagnostic = stream.Diagnostic; ResetSpeech(); return false;
        }
        public void UpdatePlayback(long consumedSamples, long streamGeneration, bool starved)
        {
            if (stream == null || generation != streamGeneration) return;
            if (!stream.Sample(consumedSamples, streamGeneration, OutputLatencyMs, starved, false, weights))
            { ClearShapes(); if (!string.IsNullOrEmpty(stream.Diagnostic)) Diagnostic = stream.Diagnostic; return; }
            ApplyCanonicalWeights(weights);
        }
        /// <summary>One complete snapshot. Bilabial closure excludes all open-mouth shapes.</summary>
        public void ApplyCanonicalWeights(float[] frame)
        {
            ClearShapes();
            if (frame == null || frame.Length != 15) return;
            mapped.Clear();
            if (!float.IsNaN(frame[1]) && !float.IsInfinity(frame[1]) && frame[1] >= .5f) mapped["speech_MBP"] = 1;
            else
            {
                float sum = 0;
                for (int i = 1; i < 15; i++)
                {
                    float value = float.IsNaN(frame[i]) || float.IsInfinity(frame[i]) ? 0 : Mathf.Clamp01(frame[i]);
                    if (i == 5) value *= .25f;
                    if (value <= 0) continue;
                    mapped.TryGetValue(Names[i], out float prior); mapped[Names[i]] = prior + value; sum += value;
                }
                if (sum > 1)
                {
                    // Reuse the fixed name list rather than allocate a key copy per frame.
                    for (int i = 1; i < 15; i++)
                    { bool first = true; for (int j = 1; j < i; j++) if (Names[j] == Names[i]) first = false;
                      if (first && mapped.ContainsKey(Names[i])) mapped[Names[i]] /= sum; }
                }
            }
            if (FaceController) { FaceController.SetSpeechWeights(mapped); return; }
            foreach (var binding in bindings)
                if (mapped.TryGetValue(binding.SemanticName, out float weight)) binding.Renderer.SetBlendShapeWeight(binding.Index, weight * 100);
        }
        void EnsureBindings()
        {
            int count = Renderers == null ? 0 : Renderers.Length;
            // Unity hot reload can restore serializable private arrays while recreating a
            // nonserializable binding list. Empty cache + assigned renderers must rebuild.
            bool changed = boundRenderers == null || boundMeshes == null || boundRenderers.Length != count || boundMeshes.Length != count || (count > 0 && bindings.Count == 0);
            for (int i = 0; !changed && i < count; i++)
                changed = boundRenderers[i] != Renderers[i] || boundMeshes[i] != (Renderers[i] ? Renderers[i].sharedMesh : null);
            if (!changed) return;
            // Clear the previous binding before a runtime renderer or mesh replacement.
            foreach (var binding in bindings)
                if (!FaceController && binding.Renderer && binding.Renderer.sharedMesh && binding.Index < binding.Renderer.sharedMesh.blendShapeCount &&
                    SemanticName(binding.Renderer.sharedMesh.GetBlendShapeName(binding.Index)) == binding.SemanticName)
                    binding.Renderer.SetBlendShapeWeight(binding.Index, 0);
            bindings.Clear(); boundRenderers = new SkinnedMeshRenderer[count]; boundMeshes = new Mesh[count];
            for (int r = 0; r < count; r++)
            {
                var renderer = Renderers[r]; boundRenderers[r] = renderer; boundMeshes[r] = renderer ? renderer.sharedMesh : null;
                if (!renderer || !renderer.sharedMesh) continue;
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    string name = SemanticName(renderer.sharedMesh.GetBlendShapeName(i));
                    if (name.StartsWith("speech_", StringComparison.Ordinal))
                        bindings.Add(new Binding { Renderer = renderer, Index = i, SemanticName = name });
                }
            }
        }
        static string SemanticName(string importedName)
        { int separator = importedName.LastIndexOf('.'); return separator < 0 ? importedName : importedName.Substring(separator + 1); }
        void ClearShapes()
        {
            if (FaceController) { FaceController.SetSpeechWeights(null); return; }
            EnsureBindings(); foreach (var binding in bindings) binding.Renderer.SetBlendShapeWeight(binding.Index, 0);
        }
        public void ResetSpeech() { stream?.Dispose(); stream = null; ClearShapes(); }
        void OnEnable() { boundRenderers = Array.Empty<SkinnedMeshRenderer>(); boundMeshes = Array.Empty<Mesh>(); EnsureBindings(); }
        void OnDisable() => ResetSpeech();
    }
}
