using System.IO;

namespace OwlCore.ComponentModel;

/// <summary>
/// Wraps around a non-seekable stream to enable seeking functionality with lazy loading of the source.
/// </summary>
public class LazySeekStream : Stream
{
    private Stream _originalStream;
    private MemoryStream _memoryStream;

    /// <summary>
    /// Creates a new instance of <see cref="LazySeekStream"/>.
    /// </summary>
    /// <param name="stream"></param>
    public LazySeekStream(Stream stream)
    {
        _originalStream = stream;
        _memoryStream = new MemoryStream();
    }

    /// <inheritdoc />
    public override bool CanRead => _memoryStream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _memoryStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _memoryStream.CanWrite;

    /// <inheritdoc />
    public override long Length => _memoryStream.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _memoryStream.Position;
        set
        {
            if (value >= _memoryStream.Length)
            {
                // Load more data into memory stream, if needed.
                var buffer = new byte[value - _memoryStream.Length];
                var bytesRead = _originalStream.Read(buffer, 0, buffer.Length);

                _memoryStream.Seek(0, SeekOrigin.End);
                _memoryStream.Write(buffer, 0, bytesRead);
            }

            _memoryStream.Position = value;
        }
    }

    /// <inheritdoc />
    public override void Flush() => _memoryStream.Flush();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_memoryStream.Position < _memoryStream.Length)
            return _memoryStream.Read(buffer, offset, count);

        var bytesRead = _originalStream.Read(buffer, offset, count);

        _memoryStream.Seek(0, SeekOrigin.End);
        _memoryStream.Write(buffer, offset, bytesRead);

        return bytesRead;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _memoryStream.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _memoryStream.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _memoryStream.Write(buffer, offset, count);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _memoryStream.Dispose();
    }
}
