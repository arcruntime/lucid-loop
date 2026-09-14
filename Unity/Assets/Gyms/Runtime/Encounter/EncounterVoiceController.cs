using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // Presentation-independent voice for a single foreground NPC. All Unity API calls
    // and public events run on the main thread; EncounterDspOutput consumes audio on the DSP thread.
    public sealed class EncounterVoiceController : MonoBehaviour
    {
        const int Rate = 24000, Packet = 480, PlaybackCapacity = Rate * 3;
        public EncounterCoordinator Coordinator;
        public AudioSource Output;
        public bool IsReady { get; private set; }
        public bool IsConnecting { get; private set; }
        public bool IsClosing { get; private set; }
        public bool MicrophoneEnabled => nativeOwned ? nativeCaptureEnabled : microphone != null;
        public string CharacterId { get; private set; }
        public string Status { get; private set; } = "disconnected";
        public int StreamGeneration => generation;
        public bool FinalUsageConfirmed { get; private set; }
        public event Action Ready;
        public event Action<string> StatusChanged;
        public event Action<JObject> TranscriptFragment;
        public event Action<float[]> PcmOutput;
        public event Action<byte[], int> Pcm16Output;
        public event Action<int> StreamStarted;
        public event Action<int> StreamStopped;
        // Samples dequeued by the active renderer, not a guarantee of sound heard at the device.
        public event Action<int, long, int, bool, bool> PlaybackProgress;

        sealed class Retired
        { public LiveConnection Connection; public float Deadline; public bool Final; }
        readonly List<Retired> retired = new List<Retired>();
        LiveConnection connection;
        EncounterCoordinator subscribed;
        AudioClip microphone, outputClip;
        string device;
        int micCursor, generation, micAttempt, requestSequence, pendingCommands;
        float deadline, audioDebt;
        Task audioUpload;
        DspPcmPlayback playback;
        EncounterDspOutput dspOutput;
        AudioSource configuredOutput, dedicatedOutput;
        bool streamOpen;
        int audioDeviceChanged;
        string closeNotice;
        static EncounterVoiceController nativeOwner;
        bool nativeOwned, nativeCaptureEnabled;

        void OnEnable() { BindCoordinator(); AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged; }
        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // Queue only external device changes. Internal setup/reset notifications
            // can accompany microphone initialization and must not cancel that setup.
            if (deviceWasChanged) Interlocked.Exchange(ref audioDeviceChanged, 1);
        }
        void BindCoordinator()
        {
            if (subscribed == Coordinator) return;
            if (subscribed)
            { subscribed.ConversationRequested -= Begin; subscribed.ConversationInvalidated -= Leave; }
            subscribed = Coordinator;
            if (subscribed)
            { subscribed.ConversationRequested += Begin; subscribed.ConversationInvalidated += Leave; }
        }

        public void Begin(EncounterConversationRequest request)
        {
            if (request == null || !isActiveAndEnabled) return;
            RetireCurrent();
            closeNotice = null;
            Interlocked.Exchange(ref audioDeviceChanged, 0);
            generation++;
            CharacterId = request.CharacterId;
            FinalUsageConfirmed = false;
            if (!EnsureDedicatedOutput()) { SetStatus("audio_output_missing"); return; }
            int outputRate = AudioSettings.outputSampleRate;
            if (outputRate < Rate || outputRate > 192000) { SetStatus("audio_output_format_unavailable"); return; }
            var state = new DspPcmPlayback(PlaybackCapacity, outputRate);
            playback = state;
            Output.Stop();
            dspOutput = Output.GetComponent<EncounterDspOutput>();
            if (!dspOutput) dspOutput = Output.gameObject.AddComponent<EncounterDspOutput>();
            dspOutput.enabled = true;
            dspOutput.Bind(state);
            // A non-streaming silent carrier keeps the DSP filter active. Its clip
            // read/prefetch position never advances the live speech clock.
            outputClip = AudioClip.Create("Encounter DSP carrier", outputRate, 1, outputRate, false);
            Output.clip = outputClip; Output.loop = true; Output.Play();
            streamOpen = true; StreamStarted?.Invoke(generation);
            connection = new LiveConnection();
            IsConnecting = true; IsReady = IsClosing = false;
            deadline = Time.unscaledTime + 25f;
            audioDebt = 0; audioUpload = null;
            SetStatus("connecting");
            _ = connection.Connect(request.Address, request.Startup);
        }

        // OnAudioFilterRead must have an unambiguous AudioSource host. The scene's
        // coordinator can also host nightclub music sources or an AudioListener.
        bool EnsureDedicatedOutput()
        {
            if (dedicatedOutput && Output == dedicatedOutput) return true;
            ReleaseDedicatedOutput();
            if (!Output) return false;
            configuredOutput = Output;
            var host = new GameObject("Encounter voice DSP output");
            host.transform.SetParent(configuredOutput.transform, false);
            dedicatedOutput = host.AddComponent<AudioSource>();
            dedicatedOutput.playOnAwake = false;
            dedicatedOutput.outputAudioMixerGroup = configuredOutput.outputAudioMixerGroup;
            dedicatedOutput.volume = configuredOutput.volume;
            dedicatedOutput.mute = configuredOutput.mute;
            dedicatedOutput.pitch = configuredOutput.pitch;
            dedicatedOutput.priority = configuredOutput.priority;
            dedicatedOutput.panStereo = configuredOutput.panStereo;
            dedicatedOutput.spatialBlend = configuredOutput.spatialBlend;
            dedicatedOutput.spatialize = configuredOutput.spatialize;
            dedicatedOutput.spatializePostEffects = configuredOutput.spatializePostEffects;
            dedicatedOutput.dopplerLevel = configuredOutput.dopplerLevel;
            dedicatedOutput.spread = configuredOutput.spread;
            dedicatedOutput.minDistance = configuredOutput.minDistance;
            dedicatedOutput.maxDistance = configuredOutput.maxDistance;
            dedicatedOutput.velocityUpdateMode = configuredOutput.velocityUpdateMode;
            dedicatedOutput.reverbZoneMix = configuredOutput.reverbZoneMix;
            dedicatedOutput.bypassEffects = configuredOutput.bypassEffects;
            dedicatedOutput.bypassListenerEffects = configuredOutput.bypassListenerEffects;
            dedicatedOutput.bypassReverbZones = configuredOutput.bypassReverbZones;
            dedicatedOutput.ignoreListenerPause = configuredOutput.ignoreListenerPause;
            dedicatedOutput.ignoreListenerVolume = configuredOutput.ignoreListenerVolume;
            foreach (AudioSourceCurveType type in new[] { AudioSourceCurveType.CustomRolloff,
                AudioSourceCurveType.SpatialBlend, AudioSourceCurveType.Spread, AudioSourceCurveType.ReverbZoneMix })
            {
                var curve = configuredOutput.GetCustomCurve(type);
                if (curve != null) dedicatedOutput.SetCustomCurve(type, curve);
            }
            dedicatedOutput.rolloffMode = configuredOutput.rolloffMode;
            Output = dedicatedOutput;
            return true;
        }

        void ReleaseDedicatedOutput()
        {
            if (dedicatedOutput)
            {
                dedicatedOutput.Stop();
                dedicatedOutput.gameObject.SetActive(false);
                if (Output == dedicatedOutput) Output = configuredOutput;
                Destroy(dedicatedOutput.gameObject);
            }
            dedicatedOutput = null;
            configuredOutput = null;
            dspOutput = null;
        }

        public bool SendText(string text)
        {
            if (!IsReady || IsClosing || connection == null || pendingCommands >= 4 || string.IsNullOrWhiteSpace(text) || text.Length > 4000) return false;
            var active = connection;
            pendingCommands++;
            _ = SendCommand(active, new JObject { ["type"] = "game.text",
                ["requestId"] = $"unity:{generation}:{++requestSequence}", ["text"] = text });
            return true;
        }

        async Task SendCommand(LiveConnection active, JObject message)
        {
            try { await active.Send(message); }
            catch (Exception) { if (connection == active) Fail("command_transport_failed"); }
            finally { pendingCommands--; }
        }

        public void EnableMicrophone(bool enabled)
        {
            micAttempt++;
            if (!enabled) { StopMic(); if (IsReady) SetStatus("ready_typed"); return; }
            if ((!IsReady && !IsConnecting) || IsClosing || MicrophoneEnabled) return;
            StartCoroutine(StartMic(generation, micAttempt));
        }

        IEnumerator StartMic(int attemptGeneration, int attemptMic)
        {
            SetStatus("microphone_permission");
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (attemptGeneration != generation || attemptMic != micAttempt || IsClosing || (!IsReady && !IsConnecting)) yield break;
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            { SetStatus("microphone_permission_denied"); yield break; }
#if UNITY_IOS && !UNITY_EDITOR
            // Do not accumulate native capture while the relay is still starting.
            while (IsConnecting && !IsClosing && attemptGeneration == generation && attemptMic == micAttempt)
                yield return null;
            if (attemptGeneration != generation || attemptMic != micAttempt || !IsReady || IsClosing) yield break;
            if (!nativeOwned && !StartNativeAudio()) yield break;
            var captureStatus = IosNativeVoiceAudio.SetCaptureEnabled(true);
            if (captureStatus != IosNativeVoiceStatus.Running)
            { CloseNativeFailure(captureStatus); yield break; }
            nativeCaptureEnabled = true;
            SetStatus("ready_voice");
            yield break;
#else
            if (Microphone.devices.Length == 0) { SetStatus("microphone_unavailable"); yield break; }
            device = Microphone.devices[0];
            try { microphone = Microphone.Start(device, true, 1, Rate); }
            catch (Exception) { StopMic(); SetStatus("microphone_unavailable"); yield break; }
            if (!microphone || microphone.frequency != Rate || microphone.channels < 1)
            { StopMic(); SetStatus("microphone_format_unavailable"); yield break; }
            micCursor = Math.Max(0, Microphone.GetPosition(device));
            SetStatus(IsReady ? "ready_voice" : "connecting");
#endif
        }

        bool StartNativeAudio()
        {
            var switchingConnection = connection;
            int switchingGeneration = generation;
            if (nativeOwner != null && nativeOwner != this)
            { closeNotice = "Audio is in use by another conversation. Tap Talk to try again."; Leave(); SetStatus(closeNotice); return false; }
            // Claim ownership even on failed Start so cleanup only stops our backend.
            nativeOwner = this; nativeOwned = true;
            var status = IosNativeVoiceAudio.Start();
            if (status != IosNativeVoiceStatus.Running) { CloseNativeFailure(status); return false; }
            nativeCaptureEnabled = false;
            // Switch once per session. Explicitly discard the old renderer's queued
            // speech and reset the analyzer's clock before accepting native output.
            var oldPlayback = playback;
            if (dspOutput) dspOutput.Unbind(oldPlayback);
            if (Output) { Output.Stop(); if (Output.clip == outputClip) Output.clip = null; }
            if (outputClip) Destroy(outputClip); outputClip = null;
            if (streamOpen)
            {
                PlaybackProgress?.Invoke(generation, oldPlayback?.Snapshot().ConsumedSamples ?? 0, Rate, true, true);
                StreamStopped?.Invoke(generation);
            }
            if (!nativeOwned || nativeOwner != this || !streamOpen || !IsReady || IsClosing ||
                connection != switchingConnection || generation != switchingGeneration) return false;
            playback = null;
            generation++;
            StreamStarted?.Invoke(generation);
            return nativeOwned && nativeOwner == this && streamOpen && IsReady && !IsClosing &&
                connection == switchingConnection && generation == switchingGeneration + 1;
        }

        void CloseNativeFailure(IosNativeVoiceStatus status)
        {
            closeNotice = DescribeAudioFailure(status);
            Leave();
            SetStatus(closeNotice);
        }

        static string DescribeAudioFailure(IosNativeVoiceStatus status) => status switch
        {
            IosNativeVoiceStatus.PermissionRequired => "Microphone access is unavailable. Check microphone permission, then tap Talk to try again.",
            IosNativeVoiceStatus.RouteChanged or IosNativeVoiceStatus.ConfigurationChanged => "Audio device changed. Tap Talk to start again.",
            IosNativeVoiceStatus.Interrupted => "Audio was interrupted. Tap Talk to start again when you're ready.",
            IosNativeVoiceStatus.UnsupportedOS or IosNativeVoiceStatus.UnsupportedPlatform => "Voice audio is unavailable on this device. Tap Talk to use typed replies.",
            _ => "Voice audio stopped. Check your audio device, then tap Talk to try again."
        };

        void Update()
        {
            BindCoordinator();
            // Native route/configuration notifications are authoritative after the
            // switch; Unity may also report its own setup changes during that switch.
            if (Interlocked.Exchange(ref audioDeviceChanged, 0) != 0 && !nativeOwned && streamOpen && !IsClosing)
            {
                closeNotice = "Audio device changed. Tap Talk to start again.";
                Leave();
                SetStatus(IsClosing ? "Audio device changed. Closing conversation..." : closeNotice);
            }
            if (nativeOwned && !IsClosing)
            {
                var status = IosNativeVoiceAudio.Status;
                if (status != IosNativeVoiceStatus.Running) CloseNativeFailure(status);
            }
            DrainRetired();
            var active = connection;
            int budget = 80;
            while (active != null && connection == active && budget-- > 0 && active.TryRead(out var message)) Handle(message);
            if ((IsConnecting || IsClosing) && Time.unscaledTime > deadline)
            {
                if (IsClosing && FinalUsageConfirmed) CompleteClose();
                else Fail(IsClosing ? "closed_final_usage_unconfirmed" : "startup_timeout");
            }
            if (IsReady && !IsClosing) PumpInput();
            if (streamOpen && nativeOwned)
                PlaybackProgress?.Invoke(generation, (long)IosNativeVoiceAudio.ConsumedOutputSamples, Rate, IosNativeVoiceAudio.IsStarved, false);
            else if (streamOpen && playback != null)
            {
                var rendered = playback.Snapshot();
                PlaybackProgress?.Invoke(generation, rendered.ConsumedSamples, Rate, rendered.Starved, false);
            }
        }

        void PumpInput()
        {
            // Keep Live frame progress moving even without microphone access, so typed
            // context/results can be injected. Disabling the mic replaces capture with silence.
            audioDebt += Time.unscaledDeltaTime;
            if (audioDebt > .5f) { Fail("audio_input_stalled"); return; }
            if (audioUpload != null && !audioUpload.IsCompleted) return;
            if (audioUpload != null && (audioUpload.IsFaulted || audioUpload.IsCanceled))
            { _ = audioUpload.Exception; Fail("audio_upload_failed"); return; }
            int packets = Math.Min(4, (int)(audioDebt / .02f));
            if (packets < 1) return;
            int frames = packets * Packet;
            var mono = new float[frames];
            if (nativeOwned && nativeCaptureEnabled)
            {
                int captured = IosNativeVoiceAudio.ReadCapture(mono, frames);
                if (captured < 0) { CloseNativeFailure((IosNativeVoiceStatus)captured); return; }
                if (captured > frames) { Fail("native_capture_invalid_count"); return; }
                // Any unread tail remains zero; mic OFF takes the all-silence path.
            }
            else if (microphone)
            {
                int position = Microphone.GetPosition(device);
                if (position < 0 || !Microphone.IsRecording(device))
                { StopMic(); SetStatus("microphone_disconnected"); }
                else
                {
                    int available = (position - micCursor + microphone.samples) % microphone.samples;
                    if (available > Rate / 2) { Fail("microphone_capture_overrun"); return; }
                    int take = Math.Min(frames, available);
                    if (take > 0)
                    {
                        int channels = microphone.channels;
                        var captured = new float[take * channels];
                        if (!microphone.GetData(captured, micCursor)) { Fail("microphone_read_failed"); return; }
                        for (int i = 0; i < take; i++)
                        { float sum = 0; for (int c = 0; c < channels; c++) sum += captured[i * channels + c]; mono[i] = sum / channels; }
                        micCursor = (micCursor + take) % microphone.samples;
                    }
                }
            }
            audioDebt -= packets * .02f;
            // One bounded send at a time; do not use the gym helper that silently drops queued audio.
            audioUpload = connection.Send(new JObject { ["type"] = "session.input_audio.append",
                ["audio"] = Convert.ToBase64String(AudioRingBuffer.Encode(mono, mono.Length)) });
        }

        void Handle(JObject message)
        {
            string type = (string)message["type"];
            if (type == "session.started") { if (IsConnecting) SetStatus("session_started"); }
            else if (type == "session.output_audio.delta")
            {
                if (!streamOpen || IsClosing) return;
                try
                {
                    var bytes = Convert.FromBase64String((string)message["delta"] ?? "");
                    int queued = nativeOwned ? IosNativeVoiceAudio.QueuedOutputSamples : playback == null ? PlaybackCapacity : playback.Snapshot().QueuedSamples;
                    if (bytes.Length == 0 || bytes.Length % 2 != 0 || queued < 0 ||
                        bytes.Length / 2 > PlaybackCapacity - queued)
                    { Fail("audio_output_overflow_or_invalid"); return; }
                    float[] samples = null;
                    if (nativeOwned || PcmOutput != null)
                    {
                        samples = new float[bytes.Length / 2];
                        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
                    }
                    if (nativeOwned)
                    {
                        int written = IosNativeVoiceAudio.WriteOutput(samples, samples.Length);
                        if (written < 0) { CloseNativeFailure((IosNativeVoiceStatus)written); return; }
                        // Never analyze or silently discard an unqueued suffix.
                        if (written != samples.Length) { Fail("native_audio_output_partial_write"); return; }
                    }
                    else if (!playback.TryWritePcm16(bytes)) { Fail("audio_output_overflow_or_invalid"); return; }
                    int packetGeneration = generation;
                    Pcm16Output?.Invoke(bytes, packetGeneration);
                    if (!streamOpen || generation != packetGeneration) return;
                    if (PcmOutput != null)
                    {
                        if (samples == null)
                        {
                            samples = new float[bytes.Length / 2];
                            for (int i = 0; i < samples.Length; i++) samples[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
                        }
                        PcmOutput.Invoke(samples);
                    }
                }
                catch (Exception) { Fail("invalid_output_audio"); }
            }
            else if (type == "session.input_transcript.delta" || type == "session.output_transcript.delta")
            { if (!IsClosing) TranscriptFragment?.Invoke((JObject)message.DeepClone()); }
            else if (type == "session.closed")
            {
                FinalUsageConfirmed = true; IsReady = IsConnecting = false; IsClosing = true;
                StopStream(); deadline = Time.unscaledTime + 2f; SetStatus("finalizing");
            }
            else if (type == "gym.transport.closed")
            { if (FinalUsageConfirmed) CompleteClose(); else Fail("transport_closed_final_usage_unconfirmed"); }
            else if (type == "gym.status")
            {
                string state = (string)message["status"];
                if (state == "ready" && IsConnecting)
                {
                    IsConnecting = false; IsReady = true; audioDebt = 0;
                    if (microphone) micCursor = Math.Max(0, Microphone.GetPosition(device));
                    SetStatus(MicrophoneEnabled ? "ready_voice" : "ready_typed"); Ready?.Invoke();
                }
                else if (state == "closed" && FinalUsageConfirmed) CompleteClose();
                else if (state == "error")
                {
                    string code = (string)message["code"];
                    if (IsReady && (code == "unsupported_event" || code == "invalid_message" || code == "not_ready")) SetStatus("command_rejected");
                    else if (code == "game_paused") Fail("Resume the night before starting a conversation.");
                    else if ((code == "out_of_range" || code == "character_out_of_range")) Fail("They moved out of range. Leave, then tap Talk to approach again.");
                    else Fail(IsConnecting ? "startup_rejected" : "relay_error");
                }
            }
            else if (type == "game.intent_result") { if (!IsClosing) SetStatus(EncounterIntentFeedback.Describe(message)); }
            else if (type == "game.error") { SetStatus("game_request_rejected"); }
            else if (type == "error")
            { if (IsConnecting) Fail("provider_startup_rejected"); else SetStatus("provider_command_rejected"); }
        }

        public void Leave()
        {
            if (IsClosing) return;
            StopStream();
            if (connection == null) { SetStatus("disconnected"); return; }
            if (!IsReady) { DisposeCurrent(); SetStatus("disconnected"); return; }
            IsReady = IsConnecting = false; IsClosing = true;
            deadline = Time.unscaledTime + 16f; SetStatus("closing");
            _ = connection.Command("session.close");
        }

        void RetireCurrent()
        {
            Leave();
            if (connection == null) return;
            retired.Add(new Retired { Connection = connection, Deadline = deadline, Final = FinalUsageConfirmed });
            connection = null; IsReady = IsConnecting = IsClosing = false;
            while (retired.Count > 4) { retired[0].Connection.Dispose(); retired.RemoveAt(0); }
        }

        void DrainRetired()
        {
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                var old = retired[i]; bool closed = false; int budget = 80;
                while (budget-- > 0 && old.Connection.TryRead(out var message))
                {
                    string type = (string)message["type"];
                    if (type == "session.closed") { old.Final = true; old.Deadline = Time.unscaledTime + 2f; }
                    if (type == "gym.transport.closed" || (type == "gym.status" && (string)message["status"] == "closed")) closed = true;
                    // No retired transcripts or audio are delivered to the current NPC.
                }
                if (closed || Time.unscaledTime > old.Deadline)
                { old.Connection.Dispose(); retired.RemoveAt(i); }
            }
        }

        void StopMic()
        {
            micAttempt++;
            if (nativeOwned && nativeOwner == this)
            {
                nativeCaptureEnabled = false;
                var status = IosNativeVoiceAudio.SetCaptureEnabled(false);
                if (status != IosNativeVoiceStatus.Running && streamOpen && !IsClosing)
                {
                    // Leave/StopStream also uses StopMic; defer invalidation handling
                    // to Update to avoid recursive teardown here.
                    if (closeNotice == null) closeNotice = DescribeAudioFailure(status);
                }
            }
            if (device != null) Microphone.End(device);
            if (microphone) Destroy(microphone);
            microphone = null; device = null; micCursor = 0;
        }
        void StopStream()
        {
            long consumed = nativeOwned && nativeOwner == this ? (long)IosNativeVoiceAudio.ConsumedOutputSamples :
                playback?.Snapshot().ConsumedSamples ?? 0;
            StopMic();
            if (nativeOwned && nativeOwner == this) { IosNativeVoiceAudio.Stop(); nativeOwner = null; }
            nativeOwned = nativeCaptureEnabled = false;
            if (dspOutput) dspOutput.Unbind(playback);
            else playback?.Deactivate();
            if (Output) { Output.Stop(); if (Output.clip == outputClip) Output.clip = null; }
            if (outputClip) Destroy(outputClip); outputClip = null;
            if (streamOpen)
            {
                PlaybackProgress?.Invoke(generation, consumed, Rate, true, true);
                StreamStopped?.Invoke(generation);
            }
            streamOpen = false; playback = null;
        }
        void DisposeCurrent()
        {
            StopStream(); connection?.Dispose(); connection = null;
            IsReady = IsConnecting = IsClosing = false;
            audioUpload = null; audioDebt = 0;
        }
        void CompleteClose() { DisposeCurrent(); SetStatus(closeNotice ?? "closed"); }
        void Fail(string code) { DisposeCurrent(); SetStatus(code); }
        void SetStatus(string value) { Status = value; StatusChanged?.Invoke(value); }
        void OnApplicationPause(bool paused) { if (paused) Leave(); }
        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            Interlocked.Exchange(ref audioDeviceChanged, 0);
            if (subscribed) { subscribed.ConversationRequested -= Begin; subscribed.ConversationInvalidated -= Leave; }
            subscribed = null; DisposeCurrent();
            ReleaseDedicatedOutput();
            foreach (var old in retired) old.Connection.Dispose(); retired.Clear();
        }
    }
}
