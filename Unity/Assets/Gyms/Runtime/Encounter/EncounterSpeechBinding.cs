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
            if (!Adapter || !Coordinator) { Diagnostic = "speech_binding_missing"; return; }
            Adapter.ResetSpeech(); Adapter.Face = null;
            foreach (var actor in Coordinator.Characters)
                if (actor && actor.Id == Voice.CharacterId)
                { Adapter.Face = actor.GetComponentInChildren<CharacterFaceDriver>(true); break; }
            if (!Adapter.Face) { Diagnostic = "authored_speaker_face_missing"; return; }
            Adapter.BeginStream(generation, Language); Diagnostic = Adapter.Diagnostic;
        }
        void Push(byte[] pcm, int generation)
        {
            if (!Adapter || !Adapter.Face) return;
            Adapter.PushPcm16(pcm, generation); Diagnostic = Adapter.Diagnostic;
        }
        void Progress(int generation, long consumed, int rate, bool starved, bool ended)
        {
            if (!Adapter || !Adapter.Face) return;
            if (rate != 24000) { Adapter.ResetSpeech(); Diagnostic = "unsupported_playback_rate"; return; }
            Adapter.UpdatePlayback(consumed, generation, starved, ended); Diagnostic = Adapter.Diagnostic;
        }
        void Stop(int generation)
        { if (Adapter) Adapter.ResetSpeech(); }
        void Unbind()
        {
            if (subscribed)
            {
                subscribed.StreamStarted -= Begin; subscribed.StreamStopped -= Stop;
                subscribed.Pcm16Output -= Push; subscribed.PlaybackProgress -= Progress;
            }
            subscribed = null;
            if (Adapter) Adapter.ResetSpeech();
        }
        void OnDisable() => Unbind();
    }
}
