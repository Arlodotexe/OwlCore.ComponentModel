using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// A <see cref="Stream"/> wrapper that, on dispose, disposes both the stream and an additional <see cref="IAsyncDisposable"/>.
/// </summary>
public class AsyncDelegatedDisposalStream : Stream, IDelegable<IAsyncDisposable>, IAsyncDisposable
{
    private readonly Stream _sourceStream;

    /// <summary>
    /// Creates a new instance of <see cref="AsyncDelegatedDisposalStream"/>.
    /// </summary>
    /// <param name="sourceStream">The stream to wrap. Must implement <see cref="IAsyncDisposable"/>.</param>
    public AsyncDelegatedDisposalStream(Stream sourceStream)
    {
        if (sourceStream is not IAsyncDisposable)
            throw new ArgumentException($"The provided {nameof(sourceStream)} does not implement {nameof(IAsyncDisposable)} and cannot be used with {nameof(AsyncDelegatedDisposalStream)}.");

        _sourceStream = sourceStream;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Dispose the source stream synchronously
            _sourceStream.Dispose();

            if (Inner is IDisposable disposable)
                disposable.Dispose();
            else
                Inner.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
#if NET8_0_OR_GREATER
    public async override ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        // Perform async cleanup.
        await Inner.DisposeAsync();

        // Dispose the source stream asynchronously if it supports it
        if (_sourceStream is IAsyncDisposable asyncDisposableStream)
            await asyncDisposableStream.DisposeAsync();

        // Dispose of unmanaged resources.
        Dispose(false);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public override void Flush() => _sourceStream.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _sourceStream.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => _sourceStream.Read(buffer, offset, count);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _sourceStream.ReadAsync(buffer, offset, count, cancellationToken);

#if NET5_0_OR_GREATER
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _sourceStream.ReadAsync(buffer, cancellationToken);
#endif

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _sourceStream.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _sourceStream.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _sourceStream.Write(buffer, offset, count);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _sourceStream.WriteAsync(buffer, offset, count, cancellationToken);

#if NET5_0_OR_GREATER
    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _sourceStream.WriteAsync(buffer, cancellationToken);
#endif

    /// <inheritdoc />
    public override bool CanRead => _sourceStream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _sourceStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _sourceStream.CanWrite;

    /// <inheritdoc />
    public override long Length => _sourceStream.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _sourceStream.Position;
        set => _sourceStream.Position = value;
    }

    /// <summary>
    /// The additional <see cref="IAsyncDisposable"/> instance that will be disposed along with the stream.
    /// </summary>
    public required IAsyncDisposable Inner { get; init; }
}