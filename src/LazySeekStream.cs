using System;
using System.IO;

namespace OwlCore.ComponentModel;

/// <summary>
/// Wraps around a non-seekable stream to enable seeking functionality with lazy loading of the source.
/// </summary>
public class LazySeekStream : Stream, IFlushable
{
    /// <summary>
    /// The original stream used to load data data into <see cref="MemoryStream"/>.
    /// </summary>
    protected Stream SourceStream { get; }

    /// <summary>
    /// The backing memory stream used for lazy seeking.
    /// </summary>
    protected MemoryStream MemoryStream { get; }

    /// <summary>
    /// Creates a new instance of <see cref="LazySeekStream"/>.
    /// </summary>
    /// <param name="stream"></param>
    public LazySeekStream(Stream stream)
    {
        SourceStream = stream;

        MemoryStream = new MemoryStream()
        {
            Capacity = (int)Length,
        };
    }

    /// <inheritdoc />
    public override bool CanRead => MemoryStream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => MemoryStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => SourceStream.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => MemoryStream.Position;
        set
        {
            if (value < 0)
                throw new IOException("An attempt was made to move the position before the beginning of the stream.");

            // Check if the requested position is beyond the current length of the memory stream
            if (value > MemoryStream.Length)
            {
                long additionalBytesNeeded = value - MemoryStream.Length;
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
                MemoryStream.Seek(0, SeekOrigin.End);
                MemoryStream.Write(buffer, 0, (int)totalBytesRead);
            }

            // Set the new position of the memory stream
            MemoryStream.Position = value;
        }
    }

    /// <inheritdoc />
    public override void Flush() => MemoryStream.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;

        // Read from memory stream first
        if (MemoryStream.Position < MemoryStream.Length)
        {
            totalBytesRead = MemoryStream.Read(buffer, offset, count);
            if (totalBytesRead == count)
            {
                return totalBytesRead; // Complete read from memory stream
            }

            // Prepare to read the remaining data from the original stream
            offset += totalBytesRead;
            count -= totalBytesRead;
        }

        // Read the remaining data directly into the provided buffer
        while (count > 0)
        {
            int bytesReadFromOriginalStream = SourceStream.Read(buffer, offset, count);
            if (bytesReadFromOriginalStream == 0)
            {
                break; // End of the original stream reached
            }

            // Write the new data from the original stream into the memory stream
            MemoryStream.Seek(0, SeekOrigin.End);
            MemoryStream.Write(buffer, offset, bytesReadFromOriginalStream);

            totalBytesRead += bytesReadFromOriginalStream;
            offset += bytesReadFromOriginalStream;
            count -= bytesReadFromOriginalStream;
        }

        return totalBytesRead;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;
                break;
            case SeekOrigin.Current:
                Position = MemoryStream.Position + offset;
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
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Length must be non-negative.");

        if (value < MemoryStream.Length)
        {
            // Truncate the memory stream
            MemoryStream.SetLength(value);
        }
        else if (value > MemoryStream.Length)
        {
            long additionalBytesNeeded = value - MemoryStream.Length;

            // Extend the memory stream with zeros or additional data from the original stream
            if (SourceStream.CanRead && additionalBytesNeeded > 0)
            {
                var buffer = new byte[additionalBytesNeeded];
                int bytesRead = SourceStream.Read(buffer, 0, buffer.Length);

                MemoryStream.Seek(0, SeekOrigin.End);
                MemoryStream.Write(buffer, 0, bytesRead);

                if (bytesRead < additionalBytesNeeded)
                {
                    // Fill the rest with zeros if the original stream didn't have enough data
                    var zeroFill = new byte[additionalBytesNeeded - bytesRead];
                    MemoryStream.Write(zeroFill, 0, zeroFill.Length);
                }
            }
            else
            {
                // Fill with zeros if the original stream can't be read or no additional bytes are needed
                MemoryStream.SetLength(value);
            }
        }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException($"Writing not supported by {nameof(LazySeekStream)}.");

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // See https://stackoverflow.com/a/1015790
        // If you are extending Stream, or MemoryStream etc. you will need to implement a call to Flush() when disposed/closed if it is necessary.
        Flush();

        // Dispose remaining resources 
        base.Dispose(disposing);
        MemoryStream.Dispose();
        SourceStream.Dispose();
    }
}
