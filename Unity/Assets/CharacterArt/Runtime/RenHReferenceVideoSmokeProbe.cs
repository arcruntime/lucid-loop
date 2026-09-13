using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Video;

namespace LucidLoop.CharacterArt
{
    /// <summary>Technical decoder smoke test only. No H model or placeholder character is instantiated.</summary>
    public sealed class RenHReferenceVideoSmokeProbe : MonoBehaviour
    {
        public VideoPlayer Video;
        public TextAsset Timeline;
        public string OutputDirectory, SourceSha256, TransportSha256, ControllerSha256;
        public bool QuitPlayer;
        RenHReferenceVideoTransport transport;
        RenHReferenceTimeline timeline;
        int step, stepFrame;
        double stepStarted, observedTime;
        long frozenFrame;
        readonly List<Record> records = new List<Record>();
        readonly List<string> rejectedBindings = new List<string>();
        bool complete;
        [Serializable] sealed class Record
        {
            public string name, png, sha256;
            public double time, videoTime;
            public long frame;
            public bool actualPlaying, desiredPlaying, seeking, audioMuted;
        }
        [Serializable] sealed class Evidence
        {
            public bool passed;
            public string error, unityVersion, sourceSha256, transportSha256, controllerSha256;
            public string scope = "Actual Unity VideoPlayer transport, source-video texture captures and binding-definition validation only. No character model, H expression or phone-performance proof.";
            public int frameReadyEvents, seekCompletedEvents, recoveries;
            public ulong importedFrameCount;
            public double importedFrameRate, duration, videoFrameSpan, lastVideoFrameTime;
            public Record[] records;
            public string[] rejectedInvalidBindings;
        }
        void Start()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(OutputDirectory);
            timeline = JsonUtility.FromJson<RenHReferenceTimeline>(Timeline.text);
            RenHReferenceAnimationController.ValidateTimeline(timeline);
            transport = new RenHReferenceVideoTransport(Video, timeline.duration, timeline.videoFrameSpan, timeline.lastVideoFrameTime);
            transport.FramePresented += (time, frame) => observedTime = time;
            Next(0);
        }
        void Update()
        {
            if (complete || transport == null) return;
            try
            {
                transport.Tick();
                if (transport.Error != null) throw new InvalidOperationException(transport.Error);
                if (Time.realtimeSinceStartupAsDouble - stepStarted > 12) throw new TimeoutException("Smoke step " + step + " exceeded 12 seconds.");
                switch (step)
                {
                    case 0:
                        if (!Settled()) break;
                        Check(transport.PresentedFrame == 0, "Initial decoded frame was not frame zero.");
                        Check(!Video.isPlaying, "Initial transport did not pause."); RecordFrame("initial", true);
                        Check(Video.audioTrackCount > 0, "Reference audio track was not imported.");
                        transport.SetAudio(true); Check(!Video.GetDirectAudioMute(0), "Audio enable did not unmute the decoder track."); RecordFrame("audio-enabled", false);
                        transport.SetAudio(false); Check(Video.GetDirectAudioMute(0), "Audio disable did not mute the decoder track.");
                        transport.Seek(3.2); Next(1); break;
                    case 1:
                        if (!At(3.2)) break;
                        Check(!Video.isPlaying, "Paused seek resumed playback."); RecordFrame("paused-seek-3.2", true);
                        frozenFrame = transport.PresentedFrame; Next(2); break;
                    case 2:
                        if (Time.frameCount - stepFrame < 8) break;
                        Check(transport.PresentedFrame == frozenFrame, "Paused source frame kept advancing.");
                        transport.Seek(6); Next(3); break;
                    case 3: transport.Seek(7.8); Next(4); break;
                    case 4: transport.Seek(2.4); Next(5); break;
                    case 5:
                        if (!At(2.4)) break;
                        Check(!Video.isPlaying, "Rapid paused seeks resumed playback."); RecordFrame("rapid-seek-last-wins-2.4", true);
                        transport.Replay(); Next(6); break;
                    case 6:
                        if (!transport.DesiredPlaying || transport.IsSeeking || transport.PresentedTime > .2) break;
                        RecordFrame("replay-start", true); Next(7); break;
                    case 7:
                        if (transport.PresentedTime < .4) break;
                        Check(Video.isPlaying, "Replay did not advance the actual decoder.");
                        transport.SetPlaying(false); RecordFrame("replay-advanced-pause", false);
                        transport.Seek(timeline.lastVideoFrameTime - .2); Next(8); break;
                    case 8:
                        if (!At(timeline.lastVideoFrameTime - .2)) break;
                        transport.SetPlaying(true); Next(9); break;
                    case 9:
                        if (!transport.Ended) break;
                        Check(transport.PresentedFrame == 451, "Decoder did not present the real final source frame451.");
                        Check(Math.Abs(transport.PresentedTime - timeline.lastVideoFrameTime) < .002, "Final decoded frame timestamp differs from the source PTS.");
                        Check(!Video.isPlaying && !transport.DesiredPlaying, "End state kept playing."); RecordFrame("end-last-real-frame", true);
                        transport.Seek(timeline.duration); Next(10); break;
                    case 10:
                        if (!At(timeline.lastVideoFrameTime)) break;
                        Check(transport.PresentedFrame == 451, "Tail seek fabricated a nonexistent frame."); RecordFrame("tail-seek-hold", false);
                        transport.Replay(); Next(11); break;
                    case 11:
                        if (!transport.DesiredPlaying || transport.IsSeeking || transport.PresentedTime > .2) break;
                        transport.SetPlaying(false); RecordFrame("replay-after-end", true);
                        ValidateFailures(); Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }
        bool Settled() => transport.HasPresentedFrame && !transport.IsSeeking && transport.RequestedTime < 0;
        bool At(double time) => Settled() && Math.Abs(transport.PresentedTime - time) < .038;
        void Next(int value) { step = value; stepStarted = Time.realtimeSinceStartupAsDouble; stepFrame = Time.frameCount; }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void RecordFrame(string name, bool capture)
        {
            Check(Math.Abs(observedTime - transport.PresentedTime) < .00001, "Video clock callback did not match the displayed decoder time.");
            var record = new Record { name = name, time = transport.PresentedTime, videoTime = Video.time, frame = transport.PresentedFrame,
                actualPlaying = Video.isPlaying, desiredPlaying = transport.DesiredPlaying, seeking = transport.IsSeeking,
                audioMuted = Video.audioTrackCount == 0 || Video.GetDirectAudioMute(0) };
            if (capture)
            {
                record.png = name + ".png"; var path = Path.Combine(OutputDirectory, record.png);
                CaptureTexture(Video.texture, path);
                using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                    record.sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
            records.Add(record);
        }
        void ValidateFailures()
        {
            var control = timeline.channels[0].name;
            var valid = new RenHReferenceMorphBinding { control = control, rendererPath = "Source/Head", shape = "test" };
            Reject("unknown morph control", () => RenHReferenceAnimationController.ValidateBindingDefinitions(timeline,
                new[] { new RenHReferenceMorphBinding { control = "notARealControl", rendererPath = "Source/Head", shape = "test" } }, Array.Empty<RenHReferenceRotationBinding>()));
            Reject("duplicate morph target", () => RenHReferenceAnimationController.ValidateBindingDefinitions(timeline, new[] { valid, valid }, Array.Empty<RenHReferenceRotationBinding>()));
            Reject("NaN morph multiplier", () => RenHReferenceAnimationController.ValidateBindingDefinitions(timeline,
                new[] { new RenHReferenceMorphBinding { control = control, rendererPath = "Source/Head", shape = "test", multiplier = float.NaN } }, Array.Empty<RenHReferenceRotationBinding>()));
            Reject("infinite morph offset", () => RenHReferenceAnimationController.ValidateBindingDefinitions(timeline,
                new[] { new RenHReferenceMorphBinding { control = control, rendererPath = "Source/Head", shape = "test", offset = float.PositiveInfinity } }, Array.Empty<RenHReferenceRotationBinding>()));
            foreach (var test in new[] { "unknown rotation", "NaN axis", "infinite degrees" })
            {
                var axis = new RenHReferenceRotationAxis { control = test == "unknown rotation" ? "notARealControl" : control,
                    localAxis = test == "NaN axis" ? new Vector3(float.NaN, 0, 0) : Vector3.up,
                    degreesPerUnit = test == "infinite degrees" ? float.PositiveInfinity : 20 };
                Reject(test, () => RenHReferenceAnimationController.ValidateBindingDefinitions(timeline, Array.Empty<RenHReferenceMorphBinding>(),
                    new[] { new RenHReferenceRotationBinding { transformPath = "", axes = new[] { axis } } }));
            }
        }
        void Reject(string label, Action action)
        {
            try { action(); }
            catch (InvalidOperationException error) { rejectedBindings.Add(label + ": " + error.Message); return; }
            throw new InvalidOperationException("Invalid binding was accepted: " + label);
        }
        void Finish(string error)
        {
            if (complete) return; complete = true; transport?.SetPlaying(false);
            var data = new Evidence { passed = error == null, error = error, unityVersion = Application.unityVersion,
                sourceSha256 = SourceSha256, transportSha256 = TransportSha256, controllerSha256 = ControllerSha256,
                frameReadyEvents = transport?.FrameReadyEvents ?? 0, seekCompletedEvents = transport?.SeekCompletedEvents ?? 0, recoveries = transport?.RecoveryCount ?? 0,
                importedFrameCount = Video.clip.frameCount, importedFrameRate = Video.clip.frameRate,
                duration = timeline.duration, videoFrameSpan = timeline.videoFrameSpan, lastVideoFrameTime = timeline.lastVideoFrameTime,
                records = records.ToArray(), rejectedInvalidBindings = rejectedBindings.ToArray() };
            File.WriteAllText(Path.Combine(OutputDirectory, "transport-smoke.json"), JsonUtility.ToJson(data, true));
            Debug.Log(error == null ? "REN_H_TRANSPORT_SMOKE_OK" : "REN_H_TRANSPORT_SMOKE_FAILED: " + error);
            if (QuitPlayer) Application.Quit(error == null ? 0 : 2);
        }
        static void CaptureTexture(Texture source, string path)
        {
            if (!source) throw new InvalidOperationException("Video did not supply a decoded texture.");
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active; var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false, false);
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(image); }
        }
        void OnDestroy() { transport?.Dispose(); }
    }
}
