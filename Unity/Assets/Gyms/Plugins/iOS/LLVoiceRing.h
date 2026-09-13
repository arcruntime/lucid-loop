#pragma once
#include <atomic>
#include <cstdint>
#include <cstddef>
#include <algorithm>
#include <type_traits>

namespace llvoice {
// Exactly one producer and one consumer. Never reset the producer counter while live.
// Consumer may discard queued values. Captured samples carry an epoch across mute.
template<class T, uint32_t Capacity> class SpscRing {
    static_assert(Capacity && !(Capacity & (Capacity - 1)), "capacity must be power of two");
    static_assert(std::is_trivially_copyable<T>::value, "audio ring values must be trivial");
    static_assert(ATOMIC_LLONG_LOCK_FREE == 2, "64-bit atomics must be lock-free on target");
    alignas(64) std::atomic<uint64_t> written{0};
    alignas(64) std::atomic<uint64_t> read{0};
    T values[Capacity]{};
public:
    uint32_t available() const {
        auto r = read.load(std::memory_order_acquire);
        auto w = written.load(std::memory_order_acquire);
        return static_cast<uint32_t>(std::min<uint64_t>(w-r, Capacity));
    }
    uint32_t write(const T* source, uint32_t count) {
        auto w = written.load(std::memory_order_relaxed);
        auto r = read.load(std::memory_order_acquire);
        auto n = std::min(count, Capacity - static_cast<uint32_t>(w-r));
        for (uint32_t i=0; i<n; ++i) values[(w+i)&(Capacity-1)] = source[i];
        written.store(w+n,std::memory_order_release); return n;
    }
    uint32_t pop(T* destination, uint32_t count) {
        auto r = read.load(std::memory_order_relaxed);
        auto w = written.load(std::memory_order_acquire);
        auto n = std::min(count,static_cast<uint32_t>(w-r));
        for (uint32_t i=0; i<n; ++i) destination[i] = values[(r+i)&(Capacity-1)];
        read.store(r+n,std::memory_order_release); return n;
    }
    void discardFromConsumer() { read.store(written.load(std::memory_order_acquire),std::memory_order_release); }
};
struct CaptureSample { float value; uint32_t epoch; };
}
