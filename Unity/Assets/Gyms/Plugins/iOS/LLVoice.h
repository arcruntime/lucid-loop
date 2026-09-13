#pragma once
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
// Every ABI call must originate on the same Unity main thread. No permission UI here.
// Mono float32, 24000 Hz. Start requires already-granted recording permission.
// Start and SetCaptureEnabled return 1 on success, a negative LLVoiceStatus on failure.
// Read/Write return nonnegative sample counts or negative LLVoiceStatus errors.
// Discard destination contents on a negative Read result. Write may accept a prefix.
enum LLVoiceStatus {
    LLVoiceStopped = 0, LLVoiceRunning = 1,
    LLVoicePermissionRequired = -1, LLVoiceSessionFailed = -2,
    LLVoiceProcessingFailed = -3, LLVoiceFormatUnsupported = -4,
    LLVoiceEngineFailed = -5, LLVoiceRouteChanged = -6,
    LLVoiceInterrupted = -7, LLVoiceMediaReset = -8,
    LLVoiceConfigurationChanged = -9, LLVoiceWrongThread = -10,
    LLVoiceInvalidSamples = -11, LLVoiceCaptureOverflow = -12,
    LLVoiceConversionFailed = -13, LLVoiceMuteUnavailable = -14,
    LLVoiceUnsupportedOS = -15
};
int LLVoice_Start(void);
void LLVoice_Stop(void);
int LLVoice_SetCaptureEnabled(int enabled); // Default OFF on every Start.
int LLVoice_ReadCapture(float* destination, int capacity);
int LLVoice_WriteOutput(const float* samples, int count);
uint64_t LLVoice_GetConsumedOutput(void); // Real samples dequeued by source node, not DAC time.
int LLVoice_GetQueuedOutput(void);
int LLVoice_GetStarved(void); // Most recent render needed more samples than queued.
int LLVoice_GetStatus(void); // Negative => caller stops on main thread; explicit restart only.
#ifdef __cplusplus
}
#endif
