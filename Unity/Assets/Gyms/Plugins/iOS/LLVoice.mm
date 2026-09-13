#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>
#include "LLVoice.h"
#include "LLVoiceRing.h"
#include <memory>
#include <cmath>
#include <cstring>

#if !__has_feature(objc_arc)
#error LLVoice.mm requires -fobjc-arc
#endif

namespace {
using llvoice::CaptureSample;
static_assert(ATOMIC_INT_LOCK_FREE == 2, "render status atomics must be lock-free");
struct AudioState {
    // Internal starting state never returned by a completed Start.
    std::atomic<int> status{2};
    std::atomic<uint64_t> captureControl{2}; // epoch << 1 | enabled, one coherent snapshot
    std::atomic<uint32_t> starved{0};
    std::atomic<uint64_t> consumed{0};
    llvoice::SpscRing<CaptureSample,262144> capture;
    llvoice::SpscRing<float,131072> output;
    void invalidate(int code) {
        int expected = status.load(std::memory_order_acquire);
        while ((expected == LLVoiceRunning || expected == 2) &&
               !status.compare_exchange_weak(expected,code,std::memory_order_acq_rel)) {}
    }
};
// Callback blocks retain only AudioState, never Backend/engine: no ownership cycle.
struct Backend {
    std::shared_ptr<AudioState> state = std::make_shared<AudioState>();
    __strong AVAudioEngine* engine = nil;
    __strong AVAudioSourceNode* source = nil;
    __strong AVAudioConverter* converter = nil;
    __strong AVAudioPCMBuffer* inputBuffer = nil;
    __strong AVAudioPCMBuffer* outputBuffer = nil;
    __strong NSMutableArray* observers = nil;
    __strong NSString* previousCategory = nil;
    __strong NSString* previousMode = nil;
    AVAudioSessionCategoryOptions previousOptions = 0;
    bool sessionChanged = false, tapInstalled = false;
    uint32_t outputOffset = 0;
};
std::unique_ptr<Backend> backend;
int lastStatus = LLVoiceStopped;

bool MainThread() { return [NSThread isMainThread]; }
void Silence(AudioBufferList* buffers) {
    if (!buffers) return;
    for (UInt32 i=0; i<buffers->mNumberBuffers; ++i)
        if (buffers->mBuffers[i].mData) std::memset(buffers->mBuffers[i].mData,0,buffers->mBuffers[i].mDataByteSize);
}
NSString* RouteIdentity(AVAudioSession* session) {
    NSMutableString* identity = [NSMutableString string];
    AVAudioSessionRouteDescription* route = session.currentRoute;
    for (AVAudioSessionPortDescription* port in route.inputs) [identity appendFormat:@"I:%@:%@;",port.UID,port.portType];
    for (AVAudioSessionPortDescription* port in route.outputs) [identity appendFormat:@"O:%@:%@;",port.UID,port.portType];
    return identity;
}
void Observe(Backend& b, NSNotificationName name, id object, int code) {
    auto state = b.state;
    id observer = [[NSNotificationCenter defaultCenter] addObserverForName:name object:object queue:nil usingBlock:^(NSNotification*) {
        // Notification may run on an engine queue. Atomic invalidation ONLY.
        state->invalidate(code);
    }];
    [b.observers addObject:observer];
}
void ObserveRoute(Backend& b, AVAudioSession* session, NSString* configuredRoute,
                  NSString* configuredCategory, NSString* configuredMode, AVAudioSessionCategoryOptions configuredOptions) {
    auto state = b.state;
    // Capture immutable setup values and state, never Backend or engine.
    NSString* route = [configuredRoute copy];
    NSString* category = [configuredCategory copy];
    NSString* mode = [configuredMode copy];
    id observer = [[NSNotificationCenter defaultCenter] addObserverForName:AVAudioSessionRouteChangeNotification object:session queue:nil usingBlock:^(NSNotification* notification) {
        NSNumber* reason = notification.userInfo[AVAudioSessionRouteChangeReasonKey];
        // setCategory/setActive may deliver their own category-change notice late.
        // Ignore only that reason with the SAME complete configured session/route.
        if ([reason isKindOfClass:[NSNumber class]] && reason.unsignedIntegerValue == AVAudioSessionRouteChangeReasonCategoryChange &&
            [session.category isEqualToString:category] && [session.mode isEqualToString:mode] &&
            session.categoryOptions == configuredOptions && [route isEqualToString:RouteIdentity(session)]) return;
        state->invalidate(LLVoiceRouteChanged);
    }];
    [b.observers addObject:observer];
}
void TearDown() {
    if (!backend) return;
    auto& b = *backend;
    b.state->captureControl.fetch_and(~uint64_t(1),std::memory_order_acq_rel);
    b.state->status.store(LLVoiceStopped,std::memory_order_release);
    for (id observer in b.observers) [[NSNotificationCenter defaultCenter] removeObserver:observer];
    [b.observers removeAllObjects];
    @try {
        [b.engine stop];
        if (b.tapInstalled) { [b.engine.inputNode removeTapOnBus:0]; b.tapInstalled = false; }
        if (b.source) [b.engine detachNode:b.source];
    } @catch (NSException*) { /* Teardown must still release retained callback state. */ }
    // Shared AVAudioSession also serves Unity music: restore settings, do not deactivate it.
    if (b.sessionChanged) {
        NSError* ignored = nil;
        [[AVAudioSession sharedInstance] setCategory:b.previousCategory mode:b.previousMode options:b.previousOptions error:&ignored];
    }
    backend.reset(); // Any in-flight retained block still owns its AudioState safely.
}
int StartFailed(int code) { TearDown(); lastStatus = code; return code; }
}

extern "C" int LLVoice_Start(void) {
    if (!MainThread()) return LLVoiceWrongThread;
    TearDown();
    if (@available(iOS 17.0, *)) {
        if (AVAudioApplication.sharedInstance.recordPermission != AVAudioApplicationRecordPermissionGranted)
            return lastStatus = LLVoicePermissionRequired;
    } else return lastStatus = LLVoiceUnsupportedOS;
    @try {
        backend.reset(new Backend());
        auto& b = *backend;
        auto state = b.state;
        AVAudioSession* session = AVAudioSession.sharedInstance;
        b.previousCategory = session.category; b.previousMode = session.mode; b.previousOptions = session.categoryOptions;
        NSError* error = nil;
        // allowBluetooth is the iOS 17-compatible HFP flag. Do not force speaker or prefer built-in input.
        if (![session setCategory:AVAudioSessionCategoryPlayAndRecord mode:AVAudioSessionModeVoiceChat
                         options:AVAudioSessionCategoryOptionDefaultToSpeaker | AVAudioSessionCategoryOptionAllowBluetooth error:&error])
            return StartFailed(LLVoiceSessionFailed);
        b.sessionChanged = true;
        if (![session setActive:YES error:&error]) return StartFailed(LLVoiceSessionFailed);
        b.engine = [[AVAudioEngine alloc] init];
        AVAudioInputNode* input = b.engine.inputNode;
        // Engine is STOPPED. voiceChat mode alone does not enable the engine's voice processing.
        if (![input setVoiceProcessingEnabled:YES error:&error] || !input.isVoiceProcessingEnabled)
            return StartFailed(LLVoiceProcessingFailed);
        if (![input respondsToSelector:@selector(setVoiceProcessingInputMuted:)]) return StartFailed(LLVoiceMuteUnavailable);
        input.voiceProcessingInputMuted = YES;
        input.voiceProcessingBypassed = NO;
        AVAudioFormat* active = [input outputFormatForBus:0];
        NSString* configuredRoute = RouteIdentity(session);
        NSString* configuredCategory = [session.category copy];
        NSString* configuredMode = [session.mode copy];
        const AVAudioSessionCategoryOptions configuredOptions = session.categoryOptions;
        if (![configuredCategory isEqualToString:AVAudioSessionCategoryPlayAndRecord] ||
            ![configuredMode isEqualToString:AVAudioSessionModeVoiceChat]) return StartFailed(LLVoiceSessionFailed);
        const double hardwareRate = active.sampleRate;
        const UInt32 channels = active.channelCount;
        const bool interleaved = active.interleaved;
        if (hardwareRate < 8000 || hardwareRate > 192000 || channels == 0 || channels > 8 || active.commonFormat != AVAudioPCMFormatFloat32)
            return StartFailed(LLVoiceFormatUnsupported);
        AVAudioFormat* monoHardware = [[AVAudioFormat alloc] initStandardFormatWithSampleRate:hardwareRate channels:1];
        AVAudioFormat* mono24 = [[AVAudioFormat alloc] initStandardFormatWithSampleRate:24000 channels:1];
        b.converter = [[AVAudioConverter alloc] initFromFormat:monoHardware toFormat:mono24];
        b.inputBuffer = [[AVAudioPCMBuffer alloc] initWithPCMFormat:monoHardware frameCapacity:4096];
        b.outputBuffer = [[AVAudioPCMBuffer alloc] initWithPCMFormat:mono24 frameCapacity:4096];
        if (!b.converter || !b.inputBuffer || !b.outputBuffer) return StartFailed(LLVoiceConversionFailed);
        [input installTapOnBus:0 bufferSize:1024 format:active block:^(AVAudioPCMBuffer* buffer, AVAudioTime*) {
            const uint64_t control = state->captureControl.load(std::memory_order_acquire);
            if (state->status.load(std::memory_order_acquire) != LLVoiceRunning || !(control & 1)) return;
            const uint32_t epoch = static_cast<uint32_t>(control >> 1);
            AVAudioFormat* delivered = buffer.format;
            if (delivered.channelCount != channels || delivered.sampleRate != hardwareRate || delivered.commonFormat != AVAudioPCMFormatFloat32 || delivered.interleaved != interleaved) {
                state->invalidate(LLVoiceFormatUnsupported); return;
            }
            const UInt32 frames = buffer.frameLength;
            float* const* data = buffer.floatChannelData;
            if (!data) { state->invalidate(LLVoiceFormatUnsupported); return; }
            CaptureSample chunk[256];
            for (UInt32 offset=0; offset<frames; offset+=256) {
                const UInt32 n = std::min<UInt32>(256,frames-offset);
                for (UInt32 i=0; i<n; ++i) {
                    float value = 0;
                    for (UInt32 c=0; c<channels; ++c) value += interleaved ? data[0][(offset+i)*channels+c] : data[c][offset+i];
                    value /= channels;
                    chunk[i] = {std::isfinite(value) ? value : 0,epoch};
                }
                if (state->capture.write(chunk,n) != n) { state->invalidate(LLVoiceCaptureOverflow); return; }
            }
        }];
        b.tapInstalled = true;
        b.source = [[AVAudioSourceNode alloc] initWithFormat:mono24 renderBlock:^OSStatus(BOOL* silence, const AudioTimeStamp*, AVAudioFrameCount frames, AudioBufferList* buffers) {
            Silence(buffers);
            *silence = YES;
            if (state->status.load(std::memory_order_acquire) != LLVoiceRunning) return noErr;
            // Source format is explicitly non-interleaved mono float32 at 24k.
            if (!buffers || buffers->mNumberBuffers != 1 || !buffers->mBuffers[0].mData ||
                buffers->mBuffers[0].mNumberChannels != 1 || buffers->mBuffers[0].mDataByteSize / sizeof(float) < frames) {
                state->invalidate(LLVoiceFormatUnsupported); return noErr;
            }
            uint32_t read = state->output.pop(static_cast<float*>(buffers->mBuffers[0].mData),frames);
            state->consumed.fetch_add(read,std::memory_order_relaxed);
            state->starved.store(read < frames,std::memory_order_release);
            *silence = read == 0;
            return noErr;
        }];
        [b.engine attachNode:b.source];
        // Engine mixer performs 24k -> actual route conversion inside the same VPIO engine.
        [b.engine connect:b.source to:b.engine.mainMixerNode format:mono24];
        [b.engine prepare];
        // Observe after our graph setup, before start. A startup notification fails closed;
        // start must not overwrite it with Running. No callback performs teardown.
        b.observers = [[NSMutableArray alloc] init];
        ObserveRoute(b,session,configuredRoute,configuredCategory,configuredMode,configuredOptions);
        Observe(b,AVAudioSessionInterruptionNotification,session,LLVoiceInterrupted);
        Observe(b,AVAudioSessionMediaServicesWereResetNotification,session,LLVoiceMediaReset);
        Observe(b,AVAudioSessionMediaServicesWereLostNotification,session,LLVoiceMediaReset);
        Observe(b,AVAudioEngineConfigurationChangeNotification,b.engine,LLVoiceConfigurationChanged);
        // Catch format/route changes in the unobserved graph-construction window.
        AVAudioFormat* beforeStart = [input outputFormatForBus:0];
        if (beforeStart.sampleRate != hardwareRate || beforeStart.channelCount != channels ||
            beforeStart.commonFormat != AVAudioPCMFormatFloat32 || beforeStart.interleaved != interleaved ||
            ![configuredRoute isEqualToString:RouteIdentity(session)] ||
            ![configuredCategory isEqualToString:session.category] || ![configuredMode isEqualToString:session.mode] ||
            configuredOptions != session.categoryOptions) return StartFailed(LLVoiceRouteChanged);
        if (![b.engine startAndReturnError:&error]) return StartFailed(LLVoiceEngineFailed);
        int starting = 2;
        if (!state->status.compare_exchange_strong(starting,LLVoiceRunning,std::memory_order_acq_rel))
            return StartFailed(starting);
        return lastStatus = LLVoiceRunning;
    } @catch (NSException*) { return StartFailed(LLVoiceEngineFailed); }
}

extern "C" void LLVoice_Stop(void) {
    if (!MainThread()) return;
    TearDown(); lastStatus = LLVoiceStopped;
}
extern "C" int LLVoice_SetCaptureEnabled(int enabled) {
    if (!MainThread()) return LLVoiceWrongThread;
    if (!backend || backend->state->status.load(std::memory_order_acquire) != LLVoiceRunning) return LLVoice_GetStatus();
    auto& b = *backend;
    const uint64_t next = ((b.state->captureControl.load(std::memory_order_acquire) >> 1) + 1) << 1;
    b.state->captureControl.store(next,std::memory_order_release);
    b.state->capture.discardFromConsumer(); b.outputOffset = 0; b.outputBuffer.frameLength = 0;
    [b.converter reset];
    @try {
        b.engine.inputNode.voiceProcessingInputMuted = enabled == 0;
        b.state->captureControl.store(next | (enabled != 0 ? 1 : 0),std::memory_order_release);
        return LLVoiceRunning;
    } @catch (NSException*) { b.state->invalidate(LLVoiceMuteUnavailable); return LLVoiceMuteUnavailable; }
}
extern "C" int LLVoice_ReadCapture(float* destination, int capacity) {
    if (!MainThread()) return LLVoiceWrongThread;
    if (!backend) return lastStatus < 0 ? lastStatus : 0;
    auto& b = *backend;
    const int initialStatus = b.state->status.load(std::memory_order_acquire);
    if (initialStatus != LLVoiceRunning) return initialStatus;
    if (!destination || capacity <= 0) return LLVoiceInvalidSamples;
    const uint64_t control = b.state->captureControl.load(std::memory_order_acquire);
    if (!(control & 1)) return 0;
    capacity = std::min(capacity,24000); // Bound main-thread conversion work per call.
    int copied = 0;
    const uint32_t epoch = static_cast<uint32_t>(control >> 1);
    while (copied < capacity) {
        uint32_t remaining = b.outputBuffer.frameLength - b.outputOffset;
        if (remaining) {
            uint32_t n = std::min<uint32_t>(remaining,capacity-copied);
            std::memcpy(destination+copied,b.outputBuffer.floatChannelData[0]+b.outputOffset,n*sizeof(float));
            b.outputOffset += n; copied += n; continue;
        }
        b.outputOffset = 0; b.outputBuffer.frameLength = 0;
        NSError* error = nil;
        Backend* current = &b; // Synchronous conversion callback on this main-thread ABI call.
        AVAudioConverterOutputStatus result = [b.converter convertToBuffer:b.outputBuffer error:&error withInputFromBlock:^AVAudioBuffer*(AVAudioPacketCount requested, AVAudioConverterInputStatus* status) {
            auto& local = *current;
            UInt32 count = 0, wanted = std::min<UInt32>(requested,local.inputBuffer.frameCapacity);
            CaptureSample sample;
            while (count < wanted && local.state->capture.pop(&sample,1))
                if (sample.epoch == epoch) local.inputBuffer.floatChannelData[0][count++] = sample.value;
            local.inputBuffer.frameLength = count;
            *status = count ? AVAudioConverterInputStatus_HaveData : AVAudioConverterInputStatus_NoDataNow;
            return count ? local.inputBuffer : nil;
        }];
        if (result == AVAudioConverterOutputStatus_Error) { b.state->invalidate(LLVoiceConversionFailed); break; }
        if (!b.outputBuffer.frameLength) break;
    }
    const int finalStatus = b.state->status.load(std::memory_order_acquire);
    return finalStatus == LLVoiceRunning ? copied : finalStatus;
}
extern "C" int LLVoice_WriteOutput(const float* samples, int count) {
    if (!MainThread()) return LLVoiceWrongThread;
    if (!backend) return lastStatus < 0 ? lastStatus : 0;
    int status = backend->state->status.load(std::memory_order_acquire);
    if (status != LLVoiceRunning) return status;
    if (!samples || count <= 0) return LLVoiceInvalidSamples;
    count = std::min(count,131072);
    for (int i=0; i<count; ++i) if (!std::isfinite(samples[i]) || samples[i] < -1.f || samples[i] > 1.f) {
        backend->state->invalidate(LLVoiceInvalidSamples); return LLVoiceInvalidSamples;
    }
    return static_cast<int>(backend->state->output.write(samples,count));
}
extern "C" uint64_t LLVoice_GetConsumedOutput(void) { return MainThread() && backend ? backend->state->consumed.load(std::memory_order_acquire) : 0; }
extern "C" int LLVoice_GetQueuedOutput(void) { return MainThread() && backend ? static_cast<int>(backend->state->output.available()) : 0; }
extern "C" int LLVoice_GetStarved(void) { return MainThread() && backend ? static_cast<int>(backend->state->starved.load(std::memory_order_acquire)) : 0; }
extern "C" int LLVoice_GetStatus(void) { return !MainThread() ? LLVoiceWrongThread : backend ? backend->state->status.load(std::memory_order_acquire) : lastStatus; }
