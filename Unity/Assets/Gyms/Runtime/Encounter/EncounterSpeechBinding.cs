using LucidLoop.CharacterArt;
using LucidLoop.LiveSpeech;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // Playback owns time; the artist's driver remains the only facial compositor.
    public sealed class EncounterSpeechBinding : MonoBehaviour
    {
        public EncounterVoiceController Voice;
        public EncounterCoordinator Coordinator;
        public LiveSpeechFaceAdapter Adapter;
        public SpeechLanguage Language = SpeechLanguage.English;
        public string Diagnostic { get; private set; } = "speaker_not_bound";
        EncounterVoiceController subscribed;
        RenLiveSpeechFaceAdapter renAdapter;
        int activeGeneration;
        bool streamOpen;

        void OnEnable() => Bind();
        void Start() => Bind();
        void Bind()
        {
            if (subscribed == Voice) return;
            Unbind(); subscribed = Voice;
            if (!subscribed) return;
            subscribed.StreamStarted += Begin;
            subscribed.StreamStopped += Stop;
            subscribed.Pcm16Output += Push;
            subscribed.PlaybackProgress += Progress;
        }
        void Begin(int generation)
        {
            ResetAdapters(); activeGeneration = generation; streamOpen = true;
            if (!Coordinator || !Voice) { Diagnostic = "speech_binding_missing"; return; }
            if (Adapter) Adapter.Face = null;
            foreach (var actor in Coordinator.Characters)
            {
                if (!actor || actor.Id != Voice.CharacterId) continue;
                // Resolve only the speaking actor's active presentation, never an old hidden model.
                renAdapter = actor.GetComponentInChildren<RenLiveSpeechFaceAdapter>();
                if (Adapter) Adapter.Face = actor.GetComponentInChildren<CharacterFaceDriver>();
                break;
            }
            if (renAdapter)
            {
                if (Language != SpeechLanguage.English)
                { Diagnostic = "speech_language_unsupported"; return; }
                renAdapter.BeginStream(generation); Diagnostic = renAdapter.Diagnostic; return;
            }
            if (!Adapter || !Adapter.Face) { Diagnostic = "authored_speaker_face_missing"; return; }
            Adapter.BeginStream(generation, Language); Diagnostic = Adapter.Diagnostic;
        }
        void Push(byte[] pcm, int generation)
        {
            if (!streamOpen || generation != activeGeneration) return;
            if (renAdapter)
            { renAdapter.PushPcm16(pcm, generation); Diagnostic = renAdapter.Diagnostic; return; }
            if (!Adapter || !Adapter.Face) return;
            Adapter.PushPcm16(pcm, generation); Diagnostic = Adapter.Diagnostic;
        }
        void Progress(int generation, long consumed, int rate, bool starved, bool ended)
        {
            if (!streamOpen || generation != activeGeneration) return;
            if (rate != 24000)
            { ResetAdapters(); streamOpen = false; Diagnostic = "unsupported_playback_rate"; return; }
            if (ended) { Stop(generation); return; }
            if (renAdapter)
            { renAdapter.UpdatePlayback(consumed, generation, starved); Diagnostic = renAdapter.Diagnostic; return; }
            if (!Adapter || !Adapter.Face) return;
            Adapter.UpdatePlayback(consumed, generation, starved, false); Diagnostic = Adapter.Diagnostic;
        }
        void Stop(int generation)
        {
            if (!streamOpen || generation != activeGeneration) return;
            ResetAdapters(); streamOpen = false;
        }
        void ResetAdapters()
        {
            if (renAdapter) renAdapter.ResetSpeech();
            renAdapter = null;
            if (Adapter) Adapter.ResetSpeech();
        }
        void Unbind()
        {
            if (subscribed)
            {
                subscribed.StreamStarted -= Begin; subscribed.StreamStopped -= Stop;
                subscribed.Pcm16Output -= Push; subscribed.PlaybackProgress -= Progress;
            }
            subscribed = null;
            ResetAdapters(); streamOpen = false;
        }
        void OnDisable() => Unbind();
    }
}
