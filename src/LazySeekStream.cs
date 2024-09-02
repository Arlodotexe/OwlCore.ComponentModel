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

    /// <inheritdoc />
    public override long Length => SourceStream.Length;

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

            // Check if the requested position is beyond the current length of the memory stream
            if (value > BackingStream.Length)
            {
                long additionalBytesNeeded = value - BackingStream.Length;
                var buffer = new byte[additionalBytesNeeded];
                long totalBytesRead = 0;

                while (totalBytesRead < additionalBytesNeeded)
                {
                    int bytesRead = SourceStream.Read(buffer, (int)totalBytesRead, (int)(additionalBytesNeeded - totalBytesRead));
                    if (bytesRead == 0)
                        break; // End of the original stream reached

                    totalBytesRead += bytesRead;
                }

                // Write the newly read bytes to the end of the memory stream
                BackingStream.Seek(0, SeekOrigin.End);
                BackingStream.Write(buffer, 0, (int)totalBytesRead);
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
        int totalBytesRead = 0;

        // Read from backing stream first, if loaded far enough.
        Guard.IsTrue(BackingStream.CanSeek);
        if (BackingStream.Position < BackingStream.Length)
        {
            totalBytesRead = BackingStream.Read(buffer, offset, count);
            if (totalBytesRead == count)
            {
                return totalBytesRead; // Complete read from backing stream
            }

            // Prepare to read the remaining data from the original stream
            offset += totalBytesRead;
            count -= totalBytesRead;
        }

        // Read the remaining data from source into backing stream.
        while (count > 0)
        {
            int bytesReadFromOriginalStream = SourceStream.Read(buffer, offset, count);
            if (bytesReadFromOriginalStream == 0)
            {
                break; // End of the original stream reached
            }

            // Write the new data from the original stream into the backing stream
            BackingStream.Seek(0, SeekOrigin.End);
            BackingStream.Write(buffer, offset, bytesReadFromOriginalStream);

            totalBytesRead += bytesReadFromOriginalStream;
            offset += bytesReadFromOriginalStream;
            count -= bytesReadFromOriginalStream;
        }

        return totalBytesRead;
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
                Position = BackingStream.Position + offset;
                break;
            case SeekOrigin.End:
                Position = SourceStream.Length + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin.");
        }

        return Position;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        Guard.IsTrue(BackingStream.CanSeek);
        
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Length must be non-negative.");

        if (value < BackingStream.Length)
        {
            // Truncate the backing stream
            BackingStream.SetLength(value);
        }
        else if (value > BackingStream.Length)
        {
            long additionalBytesNeeded = value - BackingStream.Length;

            // Extend the memory stream with zeros or additional data from the original stream
            if (SourceStream.CanRead && additionalBytesNeeded > 0)
            {
                var buffer = new byte[additionalBytesNeeded];
                int bytesRead = SourceStream.Read(buffer, 0, buffer.Length);

                BackingStream.Seek(0, SeekOrigin.End);
                BackingStream.Write(buffer, 0, bytesRead);

                if (bytesRead < additionalBytesNeeded)
                {
                    // Fill the rest with zeros if the original stream didn't have enough data
                    var zeroFill = new byte[additionalBytesNeeded - bytesRead];
                    BackingStream.Write(zeroFill, 0, zeroFill.Length);
                }
            }
            else
            {
                // Fill with zeros if the original stream can't be read or no additional bytes are needed
                BackingStream.SetLength(value);
            }
        }
    }

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
