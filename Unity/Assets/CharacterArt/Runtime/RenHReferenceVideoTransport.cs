using System;
using UnityEngine;
using UnityEngine.Video;

namespace LucidLoop.CharacterArt
{
    /// <summary>Shared decoder transport used by the H viewer and its model-free smoke probe.</summary>
    public sealed class RenHReferenceVideoTransport : IDisposable
    {
        public event Action<double, long> FramePresented;
        public readonly VideoPlayer Player;
        public readonly double Duration, VideoFrameSpan, LastVideoFrameTime;
        public bool Prepared { get; private set; }
        public bool HasPresentedFrame { get; private set; }
        public bool IsSeeking { get; private set; }
        public bool DesiredPlaying { get; private set; }
        public bool ReferenceAudio { get; private set; }
        public bool Ended { get; private set; }
        public string Error { get; private set; }
        public double PresentedTime { get; private set; }
        public long PresentedFrame { get; private set; } = -1;
        public double RequestedTime => pendingSeek;
        public int SeekCompletedEvents { get; private set; }
        public int FrameReadyEvents { get; private set; }
        public int RecoveryCount { get; private set; }
        public float PlaybackRate { get; set; } = 1;
        bool priming, disposed, seekRecovery;
        double pendingSeek = 0, activeSeek, operationStarted;
        double FrameTolerance => Player.frameRate > 0 ? 1.05 / Player.frameRate : .04;

        public RenHReferenceVideoTransport(VideoPlayer player, double duration, double videoFrameSpan, double lastVideoFrameTime)
        {
            if (!player || !Finite(duration) || !Finite(videoFrameSpan) || !Finite(lastVideoFrameTime) ||
                lastVideoFrameTime <= 0 || lastVideoFrameTime >= videoFrameSpan || videoFrameSpan > duration)
                throw new ArgumentException("A real decoder and finite, ordered video durations are required.");
            Player = player; Duration = duration; VideoFrameSpan = videoFrameSpan; LastVideoFrameTime = lastVideoFrameTime;
            Player.playOnAwake = false; Player.isLooping = false; Player.waitForFirstFrame = true;
            Player.skipOnDrop = false; Player.sendFrameReadyEvents = true;
            Player.prepareCompleted += OnPrepared; Player.seekCompleted += OnSeekCompleted;
            Player.frameReady += OnFrameReady; Player.loopPointReached += OnEnded; Player.errorReceived += OnError;
            operationStarted = Time.realtimeSinceStartupAsDouble;
            Player.Prepare();
        }
        void OnPrepared(VideoPlayer player)
        {
            Prepared = true; priming = true; operationStarted = Time.realtimeSinceStartupAsDouble;
            // Prepare need not present frame zero. Decode explicitly, then pause on its actual frameReady.
            UpdateAudio(); player.Play();
        }
        void OnFrameReady(VideoPlayer player, long frame)
        {
            FrameReadyEvents++; HasPresentedFrame = true;
            PresentedTime = Math.Max(0, Math.Min(player.time, LastVideoFrameTime)); PresentedFrame = frame;
            FramePresented?.Invoke(PresentedTime, frame);
            if (priming)
            {
                priming = false; player.Pause();
                if (Math.Abs(pendingSeek - PresentedTime) < FrameTolerance * .5) pendingSeek = -1;
                ResumeDesiredState();
            }
            if (IsSeeking && Math.Abs(PresentedTime - activeSeek) <= FrameTolerance) CompleteSeek();
        }
        void OnSeekCompleted(VideoPlayer player)
        {
            SeekCompletedEvents++;
            // Readiness is certified by frameReady, not by seekCompleted alone. Some decoders
            // need a brief silent decode step to present a frame while the requested state is paused.
            if (IsSeeking) { UpdateAudio(); player.Play(); }
        }
        void CompleteSeek()
        {
            IsSeeking = false; Player.Pause(); ResumeDesiredState();
        }
        void OnEnded(VideoPlayer player)
        {
            DesiredPlaying = false; Ended = true; IsSeeking = false; pendingSeek = -1;
            Player.Pause(); UpdateAudio();
        }
        void OnError(VideoPlayer player, string error) { Fail(error); }
        public void Tick()
        {
            if (disposed || Error != null) return;
            var elapsed = Time.realtimeSinceStartupAsDouble - operationStarted;
            if ((!Prepared || priming) && elapsed > 15) { Fail("Video did not prepare/present its initial frame within 15 seconds."); return; }
            if (IsSeeking && elapsed > 2 && !seekRecovery)
            {
                seekRecovery = true; RecoveryCount++; UpdateAudio(); Player.time = activeSeek; Player.Play();
            }
            if (IsSeeking && elapsed > 6) { Fail("Video seek did not present the requested frame within 6 seconds."); return; }
            if (!Prepared || priming || !HasPresentedFrame) return;
            if (pendingSeek >= 0 && !IsSeeking)
            {
                var target = pendingSeek; pendingSeek = -1;
                Player.Pause(); Ended = false;
                if (Math.Abs(PresentedTime - target) < FrameTolerance * .5)
                { ResumeDesiredState(); return; }
                activeSeek = target; IsSeeking = true; seekRecovery = false;
                operationStarted = Time.realtimeSinceStartupAsDouble; UpdateAudio(); Player.time = target;
            }
            if (Player.canSetPlaybackSpeed && Finite(PlaybackRate) && Math.Abs(Player.playbackSpeed - PlaybackRate) > .001f)
                Player.playbackSpeed = Mathf.Clamp(PlaybackRate, .1f, 4);
        }
        public void Seek(double time)
        {
            if (!Finite(time)) throw new ArgumentOutOfRangeException(nameof(time));
            pendingSeek = Math.Max(0, Math.Min(time, LastVideoFrameTime)); Ended = false;
        }
        public void SetPlaying(bool play)
        {
            DesiredPlaying = play;
            if (play && Ended) Seek(0);
            ResumeDesiredState();
        }
        public void Replay() { DesiredPlaying = true; Seek(0); }
        public void SetAudio(bool enabled) { ReferenceAudio = enabled; UpdateAudio(); }
        void ResumeDesiredState()
        {
            if (!Prepared || priming || IsSeeking) return;
            UpdateAudio();
            if (DesiredPlaying && pendingSeek < 0) Player.Play(); else Player.Pause();
        }
        void UpdateAudio()
        {
            if (!Prepared) return;
            for (ushort track = 0; track < Player.audioTrackCount; track++)
                Player.SetDirectAudioMute(track, !ReferenceAudio || priming || IsSeeking);
        }
        void Fail(string error)
        {
            Error = error; DesiredPlaying = false; priming = IsSeeking = false; pendingSeek = -1;
            Player.Pause(); Debug.LogError("REN_H_REFERENCE_VIDEO_ERROR: " + error);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (!Player) return;
            Player.prepareCompleted -= OnPrepared; Player.seekCompleted -= OnSeekCompleted;
            Player.frameReady -= OnFrameReady; Player.loopPointReached -= OnEnded; Player.errorReceived -= OnError;
            Player.Pause();
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
