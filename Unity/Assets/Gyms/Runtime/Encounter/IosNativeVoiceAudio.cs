using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace LucidLoop.Gyms
{
    // Values -1 through -15 mirror Plugins/iOS/LLVoice.h. UnsupportedPlatform is
    // managed-only: the Editor and other players never substitute unprocessed audio.
    public enum IosNativeVoiceStatus
    {
        Stopped = 0, Running = 1,
        PermissionRequired = -1, SessionFailed = -2, ProcessingFailed = -3,
        FormatUnsupported = -4, EngineFailed = -5, RouteChanged = -6,
        Interrupted = -7, MediaReset = -8, ConfigurationChanged = -9,
        WrongThread = -10, InvalidSamples = -11, CaptureOverflow = -12,
        ConversionFailed = -13, MuteUnavailable = -14, UnsupportedOS = -15,
        UnsupportedPlatform = -100
    }

    // The native backend is process-global; this static bridge intentionally exposes
    // a single stream. The controller owns start/stop and must not also play the same
    // NPC samples through AudioSource. Every native call is Unity-main-thread-only.
    public static class IosNativeVoiceAudio
    {
        public const int SampleRate = 24000;
        public const int Channels = 1;
#if UNITY_IOS && !UNITY_EDITOR
        public const bool IsSupported = true;
#else
        public const bool IsSupported = false;
#endif
        static int mainThreadId;
        static bool captureEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitializeMainThread()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            captureEnabled = false;
        }

        static void RequireMainThread()
        {
            if (mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId != mainThreadId)
                throw new InvalidOperationException("iOS voice audio requires the initialized Unity main thread.");
        }

        // No permission prompt: the caller must obtain microphone permission first.
        // A successful Start enables duplex voice processing but leaves capture OFF.
        public static IosNativeVoiceStatus Start()
        {
            RequireMainThread();
            captureEnabled = false;
#if UNITY_IOS && !UNITY_EDITOR
            return (IosNativeVoiceStatus)Native.LLVoice_Start();
#else
            return IosNativeVoiceStatus.UnsupportedPlatform;
#endif
        }

        public static void Stop()
        {
            RequireMainThread();
            captureEnabled = false;
#if UNITY_IOS && !UNITY_EDITOR
            Native.LLVoice_Stop();
#endif
        }

        public static IosNativeVoiceStatus Status
        {
            get
            {
                RequireMainThread();
#if UNITY_IOS && !UNITY_EDITOR
                var status = (IosNativeVoiceStatus)Native.LLVoice_GetStatus();
#else
                var status = IosNativeVoiceStatus.UnsupportedPlatform;
#endif
                if (status != IosNativeVoiceStatus.Running) captureEnabled = false;
                return status;
            }
        }

        public static bool IsRunning => Status == IosNativeVoiceStatus.Running;
        public static bool CaptureEnabled => IsRunning && captureEnabled;

        // Negative status means stop on the main thread; restarting is explicit.
        public static IosNativeVoiceStatus SetCaptureEnabled(bool enabled)
        {
            RequireMainThread();
#if UNITY_IOS && !UNITY_EDITOR
            var status = (IosNativeVoiceStatus)Native.LLVoice_SetCaptureEnabled(enabled ? 1 : 0);
#else
            var status = IosNativeVoiceStatus.UnsupportedPlatform;
#endif
            captureEnabled = status == IosNativeVoiceStatus.Running && enabled;
            return status;
        }

        // Returns a sample count, or a negative LLVoiceStatus. Only the returned
        // prefix contains new mono float32 PCM; never transmit the unused tail.
        public static int ReadCapture(float[] destination, int capacity)
        {
            RequireMainThread();
            CheckArray(destination, capacity, nameof(destination));
#if UNITY_IOS && !UNITY_EDITOR
            return Native.LLVoice_ReadCapture(destination, capacity);
#else
            return (int)IosNativeVoiceStatus.UnsupportedPlatform;
#endif
        }

        // May accept a prefix. Caller must retain the remainder or fail the stream;
        // silently discarding it would desynchronize playback and speech animation.
        public static int WriteOutput(float[] samples, int count)
        {
            RequireMainThread();
            CheckArray(samples, count, nameof(samples));
#if UNITY_IOS && !UNITY_EDITOR
            return Native.LLVoice_WriteOutput(samples, count);
#else
            return (int)IosNativeVoiceStatus.UnsupportedPlatform;
#endif
        }

        // Stream-local real samples dequeued by the source node; excludes underrun
        // zeros. This is not a hardware/DAC presentation timestamp.
        public static ulong ConsumedOutputSamples
        {
            get
            {
                RequireMainThread();
#if UNITY_IOS && !UNITY_EDITOR
                return Native.LLVoice_GetConsumedOutput();
#else
                return 0;
#endif
            }
        }

        public static int QueuedOutputSamples
        {
            get
            {
                RequireMainThread();
#if UNITY_IOS && !UNITY_EDITOR
                return Native.LLVoice_GetQueuedOutput();
#else
                return 0;
#endif
            }
        }

        public static bool IsStarved
        {
            get
            {
                RequireMainThread();
#if UNITY_IOS && !UNITY_EDITOR
                return Native.LLVoice_GetStarved() != 0;
#else
                return false;
#endif
            }
        }

        static void CheckArray(float[] samples, int count, string name)
        {
            if (samples == null) throw new ArgumentNullException(name);
            if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
        }

#if UNITY_IOS && !UNITY_EDITOR
        static class Native
        {
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_Start();
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void LLVoice_Stop();
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_SetCaptureEnabled(int enabled);
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_ReadCapture([Out] float[] destination, int capacity);
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_WriteOutput([In] float[] samples, int count);
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern ulong LLVoice_GetConsumedOutput();
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_GetQueuedOutput();
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_GetStarved();
            [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int LLVoice_GetStatus();
        }
#endif
    }
}
