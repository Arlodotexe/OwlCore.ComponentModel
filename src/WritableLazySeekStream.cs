using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// Wraps around a non-seekable stream to enable seeking functionality with lazy loading of the source.
/// Flushes the backing memory stream (and any writes) to the provided destination stream on Flush/Dispose.
/// </summary>
public class WritableLazySeekStream : LazySeekStream
{
    /// <summary>
    /// Creates a new instance of <see cref="WritableLazySeekStream"/>.
    /// </summary>
    /// <param name="sourceStream">The original source stream used to lazy load data.</param>
    /// <param name="destinationStream">A seekable, writable destination stream to flush data to when <see cref="FlushAsync"/> is called.</param>
    public WritableLazySeekStream(Stream sourceStream, Stream destinationStream)
        : base(sourceStream)
    {
        DestinationStream = destinationStream;
    }

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <summary>
    /// A seekable, writable destination stream to flush data to when <see cref="FlushAsync"/> is called.
    /// </summary>
    public Stream DestinationStream { get; }

    /// <inheritdoc />
    public override void Flush()
    {
        if (DestinationStream.Position != 0)
            DestinationStream.Seek(0, SeekOrigin.Begin);

        // Seek to end to ensure full memory stream is loaded
        if (Position != Length)
            Seek(0, SeekOrigin.End);

        // Copy memory stream to destination.
        // Will include any writes done below.
        MemoryStream.CopyTo(DestinationStream);

        base.Flush();
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (DestinationStream.Position != 0)
            DestinationStream.Seek(0, SeekOrigin.Begin);

        // Seek to end to ensure full memory stream is loaded
        if (Position != Length)
            Seek(0, SeekOrigin.End);

        // Copy memory stream to destination.
        // Will include any writes done below.
        await MemoryStream.CopyToAsync(DestinationStream);

        await base.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void WriteByte(byte value) => MemoryStream.WriteByte(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => MemoryStream.Write(buffer, offset, count);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => MemoryStream.WriteAsync(buffer, offset, count, cancellationToken);
}