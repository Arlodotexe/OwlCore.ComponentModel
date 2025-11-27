using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;

namespace OwlCore.ComponentModel;

/// <summary>
/// Wraps around a non-seekable stream to enable seeking functionality with lazy loading of the source.
/// </summary>
public class LazySeekStream : Stream, IFlushable
{
    /// <summary>
    /// Represents a range of values with a start and end.
    /// </summary>
    private record Range
    {
        /// <summary>
        /// The inclusive start of the range.
        /// </summary>
        public long Start { get; }

        /// <summary>
        /// The exclusive end of the range.
        /// </summary>
        public long End { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Range"/> struct with a specified start and end.
        /// </summary>
        /// <param name="start">The inclusive start of the range.</param>
        /// <param name="end">The exclusive end of the range. Must be greater than or equal to <paramref name="start"/>.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="end"/> is less than <paramref name="start"/>.</exception>
        public Range(long start, long end)
        {
            if (end < start)
                throw new ArgumentException("End must be greater than or equal to Start.");

            Start = start;
            End = end;
        }

        /// <summary>
        /// Determines whether a specified position is contained within the range.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns><c>true</c> if the position is within the range; otherwise, <c>false</c>.</returns>
        public bool Contains(long position)
        {
            return position >= Start && position < End;
        }

        /// <summary>
        /// Determines whether this range overlaps with another range.
        /// </summary>
        /// <param name="other">The other range to check for overlap.</param>
        /// <returns>
        /// <c>true</c> if the ranges overlap; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Two ranges are considered to overlap if any part of one range intersects with any part of the other.
        /// 
        /// ### Overlap Scenarios:
        /// - **Complete overlap**: If one range is entirely within the other (e.g., `[2, 8)` and `[3, 7)`), they overlap.
        /// - **Partial overlap**: If ranges partially overlap (e.g., `[2, 8)` and `[5, 10)`), they overlap.
        /// - **Touching but non-overlapping**: If one range ends exactly where another starts (e.g., `[2, 5)` and `[5, 8)`), they do not overlap.
        /// - **Non-overlapping**: If the ranges are disjoint (e.g., `[2, 4)` and `[5, 8)`), they do not overlap.
        /// - **Identical ranges**: If the two ranges are exactly the same (e.g., `[2, 5)` and `[2, 5)`), they overlap.
        /// 
        /// The method checks for overlap by ensuring the start of each range is less than the end of the other range.
        /// </remarks>
        public bool Overlaps(Range other)
        {
            return Start < other.End && other.Start < End;
        }
    }

    private readonly List<Range> _writtenRanges = new List<Range>();
    private long _sourcePosition;

    /// <summary>
    /// The original stream used to load data data into <see cref="BackingStream"/>.
    /// </summary>
    public Stream SourceStream { get; }

    /// <summary>
    /// The backing memory stream used for lazy seeking.
    /// </summary>
    public Stream BackingStream { get; }

    /// <summary>
    /// Creates a new instance of <see cref="LazySeekStream"/> with a <see cref="MemoryStream"/> to back it (limited to 2.1GB).
    /// </summary>
    /// <param name="sourceStream">The underlying stream to lazy seek.</param>
    public LazySeekStream(Stream sourceStream)
    {
        SourceStream = sourceStream;
        BackingStream = new MemoryStream();
    }

    /// <summary>
    /// Creates a new instance of <see cref="LazySeekStream"/>.
    /// </summary>
    /// <param name="sourceStream">The non-seekable source stream to read from.</param>
    /// <param name="backingStream">The seekable backing stream where read <paramref name="sourceStream"/> data is persisted, and where writes are delegated to.</param>
    public LazySeekStream(Stream sourceStream, Stream backingStream)
    {
        Guard.IsTrue(backingStream.CanSeek);
        SourceStream = sourceStream;
        BackingStream = backingStream;
    }

    /// <inheritdoc />
    public override bool CanRead => SourceStream.CanRead;

    /// <inheritdoc />
    public override bool CanWrite => BackingStream.CanWrite;

    /// <summary>
    /// Gets the length of this stream in bytes from either <see cref="SourceStream"/> or <see cref="BackingStream"/>, whichever is larger.
    /// </summary>
    /// <remarks>
    /// The backing stream may be larger than the source stream when source starts empty and the stream is written to.
    /// <para/>
    /// The source stream may be larger than the backing stream when backing starts empty and source is read from.
    /// </remarks>
    public override long Length => Math.Max(SourceStream.Length, BackingStream.Length);

    /// <inheritdoc />
    public override bool CanSeek => BackingStream.CanSeek;

    /// <summary>
    /// The current position within the stream.
    /// </summary>
    /// <remarks>
    /// This is backed by <see cref="BackingStream"/>, allowing it to seek backwards.
    /// <para/>
    /// When seeking forward, <see cref="SourceStream"/> will be used unless data has been written to.
    /// </remarks>
    public override long Position
    {
        get => BackingStream.Position;
        set
        {
            if (value < 0)
                throw new IOException("An attempt was made to move the position before the beginning of the stream.");

            long additionalBytesNeeded = value - Position;
            var remainingBytesNeeded = additionalBytesNeeded;
            var totalBytesRead = 0;
            int bufferSize = 81920;
            var buffer = new byte[bufferSize];

            var pos = 0;
            while (totalBytesRead < additionalBytesNeeded)
            {
                // Reset buffer pos if too large
                // allows buffer reuse
                if (pos >= buffer.Length)
                    pos = 0;

                var bytesRead = Read(buffer, pos, (int)Math.Min(remainingBytesNeeded, buffer.Length) - pos);

                // Break early, allows setting position past source length.
                if (bytesRead == 0)
                    break;

                pos += bytesRead;
                totalBytesRead += bytesRead;
                remainingBytesNeeded -= bytesRead;
            }

            BackingStream.Position = value;
        }
    }

    /// <summary>
    /// The current position of the source stream.
    /// </summary>
    /// <remarks>
    /// The <see cref="SourceStream"/> is not seekable, so this value can only be increased.
    /// </remarks>
    public long SourcePosition
    {
        get => _sourcePosition;
        set
        {
            if (value < _sourcePosition)
                throw new ArgumentException($"Source position can only be increased, cannot seek to {value} from {_sourcePosition}.");

            // Updating Position should read from Source to Backing, advancing the source position.
            if (value > _sourcePosition)
                Position = value;
        }
    }

    /// <inheritdoc />
    public override void Flush() => BackingStream.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => BackingStream.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferParams(buffer, offset, count);

        long totalBytesToRead = count;
        var remainingBytesToRead = totalBytesToRead;
        var bytesRead = 0;

        // The backing stream should only be used to read after the stream is rewound or written to.
        // The written/rewound ranges are marked.
        // - Read backing for written ranges, advance source + discard.
        // - Read source for unwritten ranges, write to backing.
        while (remainingBytesToRead > 0)
        {
            var streamToRead = SourceStream;
            var remainingBytesToReadInternal = remainingBytesToRead;

            var rangeToRead = new Range(Position, Position + totalBytesToRead);
            var overlappingWrittenByteRanges = _writtenRanges.Where(x => x.Overlaps(rangeToRead)).OrderBy(x => x.Start);

            // If bytes following Position were written to
            var currentOverlappingWrittenRange = overlappingWrittenByteRanges.FirstOrDefault(x => x.Contains(Position));
            if (currentOverlappingWrittenRange is not null)
            {
                // Find how many.
                var backingBytesAvailable = currentOverlappingWrittenRange.End - Position;
                if (backingBytesAvailable > 0)
                {
                    // Set to read from backing.
                    streamToRead = BackingStream;

                    // Limit count to backing bytes available.
                    // Anything after this may need to be read from source instead, handled next iteration.
                    remainingBytesToReadInternal = backingBytesAvailable;
                }
            }

            // Read from source/backing to buffer.            
            // bytesRead + offset used here to fill the same buffer using multiple read calls
            var bytesReadOffset = bytesRead + offset;
            var bufferSize = (int)Math.Min(remainingBytesToReadInternal, count);
            int lastBytesReadCount = streamToRead.Read(buffer, bytesReadOffset, bufferSize);

            // Reads from source should be written to backing.
            if (streamToRead == SourceStream)
            {
                _sourcePosition += lastBytesReadCount;

                var writeStart = BackingStream.Position;
                BackingStream.Write(buffer, bytesReadOffset, lastBytesReadCount);

                var writeEnd = BackingStream.Position;
                AddWrittenRange(new(writeStart, writeEnd));
            }

            // If able, reads from backing stream should also advance the source stream.
            // Ensures that the source next reads from the expected position after backing bytes are read,
            // but skips over bytes that have been written through this consumer.
            // Discards bytes read from source, since they were read from backing already.
            if (streamToRead == BackingStream && SourcePosition < Position)
            {
                var sourceBytesToReadAndDiscard = Position - SourcePosition;
                var remainingSourceBytesToReadAndDiscard = sourceBytesToReadAndDiscard;
                var discardBuffer = new byte[count];

                while (remainingSourceBytesToReadAndDiscard > 0)
                {
                    var sourceBytesRead = SourceStream.Read(discardBuffer, 0, discardBuffer.Length);
                    if (sourceBytesRead == 0)
                        break;

                    _sourcePosition += sourceBytesRead;
                    remainingSourceBytesToReadAndDiscard -= sourceBytesRead;
                        
                    discardBuffer = new byte[Math.Min(remainingSourceBytesToReadAndDiscard, count)];
                }

                // Throws here suggest source returned zero bytes earlier than expected.
                // This is allowed behavior.
                // Guard.IsEqualTo(remainingSourceBytesToReadAndDiscard, 0);
                
                // Source position may not be in line with Backing position if data was written beyond the length of the source stream.
                // This is allowed behavior.
                // Guard.IsEqualTo(SourcePosition, Position); 
            }

            // Increment total, decrementing remaining.
            bytesRead += lastBytesReadCount;
            remainingBytesToRead -= lastBytesReadCount;

            // All bytes were read.
            if (bytesRead == totalBytesToRead)
                break;

            // End of stream reached, no bytes remain.
            if (bytesRead == 0)
                break;
        }

        return bytesRead;
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferParams(buffer, offset, count);

        long totalBytesToRead = count;
        var remainingBytesToRead = totalBytesToRead;
        var bytesRead = 0;

        // The backing stream should only be used to read after the stream is rewound or written to.
        // The written/rewound ranges are marked.
        while (remainingBytesToRead > 0)
        {
            var streamToRead = SourceStream;
            var remainingBytesToReadInternal = remainingBytesToRead;

            var rangeToRead = new Range(Position, Position + totalBytesToRead);
            var overlappingWrittenByteRanges = _writtenRanges.Where(x => x.Overlaps(rangeToRead)).OrderBy(x => x.Start);

            // If bytes following Position were written to
            var currentOverlappingWrittenRange = overlappingWrittenByteRanges.FirstOrDefault(x => x.Contains(Position));
            if (currentOverlappingWrittenRange is not null)
            {
                // Find how many.
                var backingBytesAvailable = currentOverlappingWrittenRange.End - Position;
                if (backingBytesAvailable > 0)
                {
                    // Set to read from backing.
                    streamToRead = BackingStream;

                    // Limit count to backing bytes available.
                    // Anything after this may need to be read from source instead, handled next iteration.
                    remainingBytesToReadInternal = backingBytesAvailable;
                }
            }

            // Read from source/backing to buffer.
            // bytesRead + offset used here to fill the same buffer using multiple read calls
            var bytesReadOffset = bytesRead + offset;
            var bufferSize = (int)Math.Min(remainingBytesToReadInternal, count);
            int lastBytesReadCount = await streamToRead.ReadAsync(buffer, bytesReadOffset, bufferSize, cancellationToken);

            // Reads from source should be written to backing.
            if (streamToRead == SourceStream)
            {
                _sourcePosition += lastBytesReadCount;

                var writeStart = BackingStream.Position;
                await BackingStream.WriteAsync(buffer, bytesReadOffset, lastBytesReadCount, cancellationToken);

                var writeEnd = BackingStream.Position;
                AddWrittenRange(new(writeStart, writeEnd));
            }

            // If able, reads from backing stream should also advance the source stream.
            // Ensures that the source next reads from the expected position after backing bytes are read.
            // Discards bytes read from source, since they were read from backing already.
            if (streamToRead == BackingStream && SourcePosition < Position)
            {
                var sourceBytesToReadAndDiscard = Position - SourcePosition;
                var remainingSourceBytesToReadAndDiscard = sourceBytesToReadAndDiscard;
                var discardBuffer = new byte[count];

                while (remainingSourceBytesToReadAndDiscard > 0)
                {
                    var sourceBytesRead = await SourceStream.ReadAsync(discardBuffer, 0, discardBuffer.Length, cancellationToken);
                    if (sourceBytesRead == 0)
                        break;
                        
                    discardBuffer = new byte[Math.Min(remainingSourceBytesToReadAndDiscard, count)];

                    _sourcePosition += sourceBytesRead;
                    remainingSourceBytesToReadAndDiscard -= sourceBytesRead;
                }

                // Throws here suggest source returned zero bytes earlier than expected.
                // This is allowed behavior.
                // Guard.IsEqualTo(remainingSourceBytesToReadAndDiscard, 0);
                
                // Source position may not be in line with Backing position if data was written beyond the length of the source stream.
                // This is allowed behavior.
                // Guard.IsEqualTo(SourcePosition, Position); 
            }

            // Increment total, decrementing remaining.
            bytesRead += lastBytesReadCount;
            remainingBytesToRead -= lastBytesReadCount;

            // All bytes were read.
            if (bytesRead == totalBytesToRead)
                break;

            // End of stream reached, no bytes remain.
            if (bytesRead == 0)
                break;
        }

        return bytesRead;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        Guard.IsTrue(BackingStream.CanSeek);
        
        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;
                break;
            case SeekOrigin.Current:
                Position += offset;
                break;
            case SeekOrigin.End:
                Position = Length + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin.");
        }

        return Position;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Length cannot be negative.");

        BackingStream.SetLength(value);

        // Remove any written ranges that are now beyond the new length
        _writtenRanges.RemoveAll(r => r.Start >= value);

        // Adjust any ranges that partially extend beyond the new length
        for (int i = 0; i < _writtenRanges.Count; i++)
        {
            var range = _writtenRanges[i];
            if (range.End > value)
            {
                _writtenRanges[i] = new Range(range.Start, value);
            }
        }
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        var start = BackingStream.Position;
        BackingStream.WriteByte(value);

        var end = BackingStream.Position;
        AddWrittenRange(new Range(start, end));
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferParams(buffer, offset, count);

        var start = BackingStream.Position;
        BackingStream.Write(buffer, offset, count);

        var end = BackingStream.Position;
        AddWrittenRange(new Range(start, end));
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferParams(buffer, offset, count);

        var start = BackingStream.Position;
        await BackingStream.WriteAsync(buffer, offset, count, cancellationToken);

        var end = BackingStream.Position;
        AddWrittenRange(new Range(start, end));
    }

    private void AddWrittenRange(Range newRange)
    {
        // Insert first range
        if (_writtenRanges.Count == 0)
        {
            _writtenRanges.Add(newRange);
            return;
        }

        // Find existing position to insert the range
        int index = _writtenRanges.BinarySearch(newRange, new RangeComparer());
        if (index < 0)
            index = ~index;

        // Merge overlapping ranges
        long mergedStart = newRange.Start;
        long mergedEnd = newRange.End;

        // Merge with previous ranges if overlapping or adjacent
        if (index > 0 && _writtenRanges[index - 1].End >= newRange.Start - 1)
        {
            mergedStart = _writtenRanges[index - 1].Start;
            mergedEnd = Math.Max(_writtenRanges[index - 1].End, newRange.End);
            _writtenRanges.RemoveAt(index - 1);
            index--;
        }

        // Merge with subsequent ranges if overlapping or adjacent
        while (index < _writtenRanges.Count && _writtenRanges[index].Start <= mergedEnd + 1)
        {
            mergedEnd = Math.Max(_writtenRanges[index].End, mergedEnd);
            _writtenRanges.RemoveAt(index);
        }

        // Insert the merged range
        _writtenRanges.Insert(index, new Range(mergedStart, mergedEnd));
    }

    private class RangeComparer : IComparer<Range>
    {
        public int Compare(Range? x, Range? y)
        {
            Guard.IsNotNull(x);
            Guard.IsNotNull(y);

            return x.Start.CompareTo(y.Start);
        }
    }

    /// <summary>
    /// Validates buffer parameters for read/write operations.
    /// </summary>
    private static void ValidateBufferParams(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        if (count < 0 || (offset + count) > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BackingStream.Dispose();
            SourceStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
