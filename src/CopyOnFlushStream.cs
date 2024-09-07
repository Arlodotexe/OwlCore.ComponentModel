using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// Wraps around a backing stream for read and writes, and copies to a destination stream on flush.
/// </summary>
public class CopyOnFlushStream : Stream
{
    /// <summary>
    /// Creates a new instance of <see cref="CopyOnFlushStream"/>.
    /// </summary>
    /// <param name="backingStream">The stream to use for read and writes.</param>
    /// <param name="destinationStream">The stream to copy the <paramref name="backingStream"/> to on flush.</param>
    public CopyOnFlushStream(Stream backingStream, Stream destinationStream)
    {
        BackingStream = backingStream;
        DestinationStream = destinationStream;
    }
    
    /// <summary>
    /// The backing stream to use for read and writes.
    /// </summary>
    public Stream BackingStream { get; }
    
    /// <summary>
    /// The stream to copy the <see cref="BackingStream"/> to on flush.
    /// </summary>
    public Stream DestinationStream { get; }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => BackingStream.Read(buffer, offset, count);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => BackingStream.ReadAsync(buffer, offset, count, cancellationToken);

#if NET5_0_OR_GREATER
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => BackingStream.ReadAsync(buffer, cancellationToken);
#endif

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => BackingStream.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => BackingStream.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => BackingStream.Write(buffer, offset, count);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => BackingStream.WriteAsync(buffer, offset, count, cancellationToken);

#if NET5_0_OR_GREATER
    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => BackingStream.WriteAsync(buffer, cancellationToken);
#endif

    /// <inheritdoc />
    public override bool CanRead => BackingStream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => BackingStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => BackingStream.CanWrite;
    
    /// <inheritdoc />
    public override long Length => BackingStream.Length;
    
    /// <inheritdoc />
    public override long Position
    {
        get => BackingStream.Position;
        set => BackingStream.Position = value;
    }

    /// <inheritdoc />
    public override void Flush()
    {
        if (DestinationStream.Position != 0)
            DestinationStream.Seek(0, SeekOrigin.Begin);

        // Seek to end to ensure full backing stream is loaded
        if (Position != Length)
            Seek(0, SeekOrigin.End);

        if (DestinationStream.Position != 0)
            DestinationStream.Position = 0;

        if (BackingStream.Position != 0)
            BackingStream.Position = 0;

        // Copy backing stream to destination.
        // Will include any writes done below.
        BackingStream.CopyTo(DestinationStream);

        BackingStream.Flush();
        DestinationStream.Flush();
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (DestinationStream.Position != 0)
            DestinationStream.Seek(0, SeekOrigin.Begin);

        // Seek to end to ensure full backing stream is loaded
        if (Position != Length)
            Seek(0, SeekOrigin.End);

        if (DestinationStream.Position != 0)
            DestinationStream.Position = 0;

        if (BackingStream.Position != 0)
            BackingStream.Position = 0;

        // Copy backing stream to destination.
        // Will include any writes done below.
        #if NET5_0_OR_GREATER
        await BackingStream.CopyToAsync(DestinationStream, cancellationToken);
#else
        await BackingStream.CopyToAsync(DestinationStream);
#endif
        
        await BackingStream.FlushAsync(cancellationToken);
        await DestinationStream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BackingStream.Dispose();
            DestinationStream.Dispose();
        }
        
        base.Dispose(disposing);
    }
}