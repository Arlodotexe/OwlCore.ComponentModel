using System;
using System.IO;
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
    /// The original stream used to load data data into <see cref="BackingStream"/>.
    /// </summary>
    public Stream SourceStream { get; }

    /// <summary>
    /// The backing memory stream used for lazy seeking.
    /// </summary>
    public Stream BackingStream { get; }

    /// <summary>
    /// Creates a new instance of <see cref="LazySeekStream"/>.
    /// </summary>
    /// <param name="sourceStream"></param>
    public LazySeekStream(Stream sourceStream)
    {
        SourceStream = sourceStream;

        if (sourceStream.Length > int.MaxValue)
            throw new ArgumentException($"The provided source stream is too long to fit into a {nameof(MemoryStream)}. Please provide a writeable backing stream to store lazily loaded data in.");

        BackingStream = new MemoryStream
        {
            Capacity = (int)sourceStream.Length,
        };
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

    /// <inheritdoc />
    public override long Position
    {
        get => BackingStream.Position;
        set
        {
            if (value < 0)
                throw new IOException("An attempt was made to move the position before the beginning of the stream.");

            // Check if the requested position is beyond the current length of the backing stream
            if (value > BackingStream.Length)
            {
                BackingStream.Seek(0, SeekOrigin.End);
                long additionalBytesNeeded = value - BackingStream.Length;
                int bufferSize = 81920;
                var buffer = new byte[bufferSize];

                // Advance and forward buffer from source stream to backing stream
                // until all needed bytes are read.
                while (additionalBytesNeeded > 0)
                {
                    int bufferPos = 0;
                    while (bufferPos < buffer.Length)
                    {
                        // Read from source to buffer
                        var remainingBufferLength = buffer.Length - bufferPos;
                        var bytesRead = SourceStream.Read(buffer, offset: bufferPos, count: remainingBufferLength);

                        // Write the partial buffer to the backing stream
                        BackingStream.Write(buffer, bufferPos, bytesRead);

                        bufferPos += bytesRead;
                        additionalBytesNeeded -= bytesRead;

                        // Reset position if next starting position is out range for buffer.
                        if (bufferPos >= buffer.Length)
                            bufferPos = 0;

                        Guard.IsLessThanOrEqualTo(bytesRead, maximum: remainingBufferLength);
                        Guard.IsLessThanOrEqualTo(bufferPos, maximum: buffer.Length);

                        // The below needs the variable for further checks, the above doesn't.
                        // They're identical but the above form is used elsewhere. Kept as example.
                        var unfilledBufferLength = remainingBufferLength - bytesRead;
                        Guard.IsGreaterThanOrEqualTo(unfilledBufferLength, 0);

                        // End of stream reached
                        if (bytesRead == 0)
                            break;
                    }
                }
            }

            // Set the new position of the memory stream
            BackingStream.Position = value;
        }
    }

    /// <inheritdoc />
    public override void Flush() => BackingStream.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {   
        int backingBytesRead = 0;

        // If Position is within range of the backing stream, read from there.
        var distanceFromBackingStreamLength = BackingStream.Length - Position;
        if (distanceFromBackingStreamLength > 0)
        {
            // Either read all remaining backing bytes, or only the provided count.
            // Whichever is smaller.
            // If more bytes are requested than available in backing, backing will be read first and source will be read after.
            var totalBackingBytesToRead = Math.Min(distanceFromBackingStreamLength, count);
            var remainingBackingBytesToRead = totalBackingBytesToRead;
            var bufferSize = count;
            while (remainingBackingBytesToRead > 0)
            {
                // Read from backing to buffer
                var bytesRead = BackingStream.Read(buffer, offset + backingBytesRead, bufferSize);

                // Increment total, decrementing remaining.
                backingBytesRead += bytesRead;
                remainingBackingBytesToRead -= bytesRead;

                // Remaining backing bytes were read.
                if (backingBytesRead == totalBackingBytesToRead)
                    break;

                // End of stream reached, no backing bytes remain.
                if (bytesRead == 0)
                    break;
            }

            // Checks for min, max bounds
            // Should always read the number of bytes available.
            // Source can only advance where it left off.
            Guard.IsEqualTo(remainingBackingBytesToRead, 0);
            Guard.IsEqualTo(backingBytesRead, totalBackingBytesToRead);
        }

        // Remaining bytes not read by the backing stream should be read from source and copied to backing.
        var totalSourceBytesToRead = count - backingBytesRead;
        var remainingSourceBytesToRead = totalSourceBytesToRead;
        var sourceBytesRead = 0;
        while (remainingSourceBytesToRead > 0)
        {
            // Read from source to buffer.
            int bytesRead = SourceStream.Read(buffer, sourceBytesRead + offset, remainingSourceBytesToRead);

            // Write buffer to backing
            BackingStream.Write(buffer, sourceBytesRead + offset, bytesRead);

            // Increment total, decrementing remaining.
            sourceBytesRead += bytesRead;
            remainingSourceBytesToRead -= bytesRead;

            // Remaining source bytes were read.
            if (sourceBytesRead == totalSourceBytesToRead)
                break;

            // End of stream reached, no bytes remain.
            if (bytesRead == 0)
                break;
        }
        
        return backingBytesRead + sourceBytesRead;
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
    public override void SetLength(long value) => BackingStream.SetLength(value);

    /// <inheritdoc />
    public override void WriteByte(byte value) => BackingStream.WriteByte(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => BackingStream.Write(buffer, offset, count);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => BackingStream.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Only continue for managed resources.
        if (!disposing)
            return;
        
        BackingStream.Dispose();
        SourceStream.Dispose();
    }
}
