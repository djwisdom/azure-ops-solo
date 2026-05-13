using System;
using System.Threading;

namespace MyCrownJewelApp.Pfpad;

#if PROFILING

/// <summary>
/// Lock-free ring buffer for batching performance samples.
/// Uses volatile reads/writes and careful ordering to ensure thread safety.
/// </summary>
public sealed class RingBuffer<T> where T : struct
{
    private readonly T[] _buffer;
    private readonly int _capacity;
    private volatile int _writePos;
    private volatile int _readPos;
    private volatile int _count;

    public RingBuffer(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if ((capacity & (capacity - 1)) != 0) throw new ArgumentException("Capacity must be a power of 2", nameof(capacity));

        _capacity = capacity;
        _buffer = new T[capacity];
        _writePos = 0;
        _readPos = 0;
        _count = 0;
    }

    public int Capacity => _capacity;
    public int Count => _count;

    /// <summary>
    /// Try to add an item to the ring buffer. Returns false if buffer is full.
    /// </summary>
    public bool TryEnqueue(T item)
    {
        int count = _count;
        if (count >= _capacity) return false;

        int writePos = _writePos;
        _buffer[writePos] = item;

        // Memory barrier to ensure item is written before updating positions
        Thread.MemoryBarrier();

        _writePos = (writePos + 1) & (_capacity - 1);
        Interlocked.Increment(ref _count);
        return true;
    }

    /// <summary>
    /// Try to remove an item from the ring buffer. Returns false if buffer is empty.
    /// </summary>
    public bool TryDequeue(out T item)
    {
        item = default;
        int count = _count;
        if (count == 0) return false;

        int readPos = _readPos;
        item = _buffer[readPos];

        _readPos = (readPos + 1) & (_capacity - 1);
        Interlocked.Decrement(ref _count);
        return true;
    }

    /// <summary>
    /// Try to peek at the next item without removing it.
    /// </summary>
    public bool TryPeek(out T item)
    {
        item = default;
        if (_count == 0) return false;

        item = _buffer[_readPos];
        return true;
    }

    /// <summary>
    /// Batch dequeue multiple items at once for efficiency.
    /// </summary>
    public int DequeueBatch(Span<T> destination)
    {
        int dequeued = 0;
        while (dequeued < destination.Length && TryDequeue(out T item))
        {
            destination[dequeued++] = item;
        }
        return dequeued;
    }

    /// <summary>
    /// Clear all items from the buffer.
    /// </summary>
    public void Clear()
    {
        _writePos = 0;
        _readPos = 0;
        _count = 0;
        Array.Clear(_buffer, 0, _capacity);
    }
}

#endif