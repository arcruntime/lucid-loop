// Ported from HeadAudio modules/ringbuffer.mjs (MIT License, Copyright (c) 2025 Mika Suominen).
// Pinned commit: d3af5f9ff86ab6b2b1913d411a4e1922ec101953. See src/THIRD-PARTY-NOTICES.md.

using System;

namespace SplatterfaceGames.LipSync
{
    /// <summary>
    /// Fixed-size ring buffer. Semantics are identical to the upstream JS implementation,
    /// including the unusual getLatest() head/count bookkeeping the processor relies on.
    /// </summary>
    internal sealed class RingBuffer<T>
    {
        internal readonly T[] Buf;
        private readonly int _capacity;
        private int _head; // Index of oldest element
        private int _tail; // Index of next write
        private int _count; // Number of valid elements

        public RingBuffer(int capacity, Func<T>? factory = null, bool full = false)
        {
            Buf = new T[capacity];
            if (factory != null)
                for (int i = 0; i < capacity; i++) Buf[i] = factory();
            _capacity = capacity;
            _head = 0;
            _tail = 0;
            _count = full ? capacity : 0;
        }

        public int Capacity => _capacity;
        public int Count => _count;

        /// <summary>Add item.</summary>
        public void Add(T item)
        {
            int c = _capacity;
            Buf[_tail] = item;
            _tail = (_tail + 1) % c;
            if (_count < c) _count++;
            else _head = (_head + 1) % c;
        }

        /// <summary>Allocate the next item and advance; returns a reference to the slot.</summary>
        public T Allocate()
        {
            int c = _capacity;
            T item = Buf[_tail];
            _tail = (_tail + 1) % c;
            if (_count < c) _count++;
            else _head = (_head + 1) % c;
            return item;
        }

        public bool IsAvailable(int n) => _count >= n;

        public bool IsFull() => _count == _capacity;

        /// <summary>Get item at position relative to the head (oldest element).</summary>
        public T GetHead(int pos = 0)
        {
            int c = _capacity;
            int index = ((_head + (pos % c)) + c) % c;
            return Buf[index];
        }

        /// <summary>Get item at position relative to the tail (pos 0 = last added).</summary>
        public T GetTail(int pos = 0)
        {
            int c = _capacity;
            int index = ((_tail + ((pos - 1) % c)) + c) % c;
            return Buf[index];
        }

        /// <summary>
        /// Copy the latest items into <paramref name="outBuf"/>. After reading, the count
        /// of valid items becomes (out.Length - hop), matching upstream exactly.
        /// </summary>
        public T[]? GetLatest(T[] outBuf, int? hop = null)
        {
            int n = outBuf.Length;
            if (n > _count) return null;
            int c = _capacity;
            _head = (_head + (_count - n)) % c;
            for (int i = 0; i < n; i++)
                outBuf[i] = Buf[(_head + i) % c];
            if (hop.HasValue && hop.Value >= 0 && hop.Value <= n)
            {
                _head = (_head + hop.Value) % c;
                _count = n - hop.Value;
            }
            return outBuf;
        }

        /// <summary>Clear the buffer, optionally leaving <paramref name="cnt"/> items.</summary>
        public void Clear(int cnt = 0)
        {
            _count = cnt;
            _tail = (_head + cnt) % _capacity;
        }

        /// <summary>
        /// Restore the constructed "full" state: every slot set to
        /// <paramref name="value"/>, head/tail rewound, count = capacity.
        /// Used by VisemeAnalyzer.Reset for buffers upstream creates with full=true.
        /// </summary>
        public void ResetFilled(T value)
        {
            for (int i = 0; i < _capacity; i++) Buf[i] = value;
            _head = 0;
            _tail = 0;
            _count = _capacity;
        }
    }
}
