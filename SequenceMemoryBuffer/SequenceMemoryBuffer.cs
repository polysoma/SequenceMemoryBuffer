using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace SequenceMemoryBuffer;

public class SequenceMemoryBuffer<T>
{
    const int MinimumCapacity = 256;
    private const int MaximumBlockSize = 24;

    private class MemoryBufferSequenceSegment : ReadOnlySequenceSegment<T>
    {
        public MemoryBufferSequenceSegment(in T[] buffer, in long offset)
        {
            Memory = new Memory<T>(buffer, 0, buffer.Length);
            RunningIndex = offset;
        }

        public void SetNextSegment(MemoryBufferSequenceSegment next)
        {
            Next = next;
        }
    }

    private int _blockOffset;
    private int _blockIndex;
    private int _aryArrived;
    private int _aryPosition;

    [InlineArray(MaximumBlockSize)]
    private struct BufferBlock
    {
        private T[] _array;
    }

    private BufferBlock _blocks;

    // -------------------------------------------

    public SequenceMemoryBuffer() : this(MinimumCapacity)
    {
    }

    public SequenceMemoryBuffer(int capacity)
    {
        if (capacity < 0) {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (capacity < MinimumCapacity)
        {
            capacity = MinimumCapacity;
        }

        _blocks = default;
        _blockOffset = 0;
        _blockIndex = -1;
        InitNextBlock(capacity);
    }

    // -------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitNextBlock(int capacity)
    {
        ref var buff = ref _blocks[++_blockIndex];
        buff = new T[capacity];
        _aryPosition = 0;
        _aryArrived = buff.Length;
    }

    private int AllocNextBuffer(int hint)
    {
        var totalSize = _blockOffset + _aryPosition;
        if (totalSize == Array.MaxLength || _blockIndex >= MaximumBlockSize - 1)
        {
            // 上限越えた
            throw new IOException("Buffer Too Long");
        }

        var aryLength = _blocks[_blockIndex].Length;
        hint = hint > aryLength ? hint : aryLength;

        var nextSize = unchecked(hint * 2);
        var maxLength = unchecked(totalSize + nextSize);
        if (nextSize < 0 || maxLength < 0 || maxLength > Array.MaxLength)  // 桁あふれ
        {
            nextSize = Array.MaxLength - totalSize;
        }

        _blockOffset = totalSize;
        InitNextBlock(nextSize);
        return nextSize;
    }

    // -------------------------------------------

    /// <summary>
    /// メモリバッファに追加する
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public void Write(in T[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Need Non Negative Number");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Need Non Negative Number");
        if (buffer.Length - offset < count)
            throw new ArgumentException("Invalid Offset Length");

        ref T[] ary = ref _blocks[_blockIndex];
        if (count <= _aryArrived)
        {
            System.Buffer.BlockCopy(buffer, offset, ary, _aryPosition, count);
            _aryPosition += count;
            _aryArrived -= count;
            return;
        }
        while (count > 0)
        {
            if (_aryArrived < 1)
            {
                // 新しいバッファを追加する
                AllocNextBuffer(count);
                ary = ref _blocks[_blockIndex];
            }

            var size = (_aryArrived <= count) ? _aryArrived : count;
            System.Buffer.BlockCopy(buffer, offset, ary, _aryPosition, size);

            count -= size;
            offset += size;
            _aryPosition += size;
            _aryArrived -= size;
        }
    }

    /// <summary>
    /// メモリバッファに追加する
    /// </summary>
    /// <param name="span"></param>
    public void Write(in ReadOnlySpan<T> span)
    {
        var count = span.Length;
        var offset = 0;

        ref T[] ary = ref _blocks[_blockIndex];
        if (count <= _aryArrived)
        {
            var target = new Span<T>(ary, _aryPosition, _aryArrived);
            span.CopyTo(target);
            _aryPosition += count;
            _aryArrived -= count;
            return;
        }
        while (count > 0)
        {
            if (_aryArrived < 1)
            {
                // 新しいバッファを追加する
                AllocNextBuffer(count);
                ary = ref _blocks[_blockIndex];
            }

            var size = (_aryArrived <= count) ? _aryArrived : count;
            var target = new Span<T>(ary, _aryPosition, _aryArrived);
            span.Slice(offset, size).CopyTo(target);

            count -= size;
            offset += size;
            _aryArrived -= size;
            _aryPosition += size;
        }
    }

    /// <summary>
    /// メモリバッファに追加する
    /// </summary>
    /// <param name="buffer"></param>
    public void Write(in ReadOnlySequence<T> buffer)
    {
        foreach (var memory in buffer)
        {
            Write(memory.Span);
        }
    }

    // -------------------------------------------

    /// <summary>
    /// Array配列の取得
    /// </summary>
    /// <returns></returns>
    public T[] ToArray()
    {
        var lastIndex = _blockIndex;
        var lastUsed = _aryPosition;
        var targetArray = new T[_blockOffset + _aryPosition];

        if (lastIndex < 1)
        {
            ref readonly var block = ref _blocks[0];
            System.Buffer.BlockCopy(block, 0, targetArray, 0, lastUsed);
            return targetArray;
        }

        var offset = 0;
        var idx = 0;
        for (; idx < lastIndex; ++idx)
        {
            ref readonly var block = ref _blocks[idx];
#pragma warning disable CA2018
            System.Buffer.BlockCopy(block, 0, targetArray, offset, block.Length);
#pragma warning restore CA2018
            offset += block.Length;
        }

        {
            ref readonly var block = ref _blocks[idx];
            System.Buffer.BlockCopy(block, 0, targetArray, offset, lastUsed);
        }

        return targetArray;
    }

    /// <summary>
    /// ReadOnlySegmentの生成
    /// </summary>
    public ReadOnlySequence<T> Buffer
    {
        get
        {
            if (_blockIndex == 0 && _aryPosition < 1)
                return ReadOnlySequence<T>.Empty;

            var lastIndex = _blockIndex;
            var lastUsed = _aryPosition;

            var startSegment = new MemoryBufferSequenceSegment(_blocks[0], 0);
            if (lastIndex < 1)
            {
                return new ReadOnlySequence<T>(startSegment, 0, startSegment, lastUsed);
            }

            var prevSegment = startSegment;
            var offset = 0L;
            for (var idx = 1; idx <= lastIndex; ++idx)
            {
                ref readonly var buff = ref _blocks[idx];
                var newSegment = new MemoryBufferSequenceSegment(buff, offset);
                prevSegment.SetNextSegment(newSegment);
                offset += buff.Length;
                prevSegment = newSegment;
            }
            return new ReadOnlySequence<T>(startSegment, 0, prevSegment, lastUsed);
        }
    }
}
