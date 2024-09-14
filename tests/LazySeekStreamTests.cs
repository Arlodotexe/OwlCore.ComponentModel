using CommunityToolkit.Diagnostics;
using OwlCore.ComponentModel;
using OwlCore.Storage;

namespace OwlCore.Tests.ComponentModel;

[TestClass]
public sealed class LazySeekStreamTests
{
    [DataRow(100, 100, 10)]
    [DataRow(100, 100, 100)]
    [DataRow(10, 100, 10)]
    [DataRow(10, 10, 100)]
    [DataRow(100, 1000, 10)]
    [DataRow(100, 1000, 100)]
    [DataRow(int.MaxValue - 1024, (long)int.MaxValue + 1024, 81920)]
    [TestMethod]
    public void CanWriteToBackingBeyondSourceLength(int sourceCapacity, long destinationLengthToWrite, int bufferSize)
    {
        // Memory stream is limited to 2GB by default, but we can adjust it for test purposes
        using var sourceStream = new MemoryStream()
        {
            Capacity = sourceCapacity,
        };

        var tmpPath = Path.GetTempFileName();
        using var backingStream = new FileStream(tmpPath, FileMode.Create);

        var lazySeekStream = new LazySeekStream(sourceStream, backingStream);

        var buffer = new byte[bufferSize];
        var remainingBytes = destinationLengthToWrite;
        while (remainingBytes > 0)
        {
            lazySeekStream.Write(buffer);
            remainingBytes -= buffer.Length;
        }
    }

    [DataRow(1000, 100, 10)]
    [DataRow(100, 100, 100)]
    [DataRow(10, 100, 10)]
    [DataRow(10, 10, 100)]
    [DataRow(100, 1000, 10)]
    [DataRow(100, 1000, 100)]
    [DataRow(int.MaxValue - 1024, (long)int.MaxValue + 1024, 81920)]
    [TestMethod]
    [Timeout(5 * 60 * 1000)]
    public async Task CanReadSourceFromBacking(int sourceCapacity, long destinationLengthToWrite, int bufferSize)
    {
        // Memory stream is limited to 2GB by default, but we can adjust it for test purposes
        var sourceStream = new MemoryStream()
        {
            Capacity = sourceCapacity,
        };

        await WriteRandomBytesAsync(sourceStream, sourceCapacity, bufferSize, CancellationToken.None);
        sourceStream.Position = 0;

        // Setup lazy seek backed by system file
        var tmpPath = Path.GetTempFileName();
        using var backingStream = new FileStream(tmpPath, FileMode.Create);

        Guard.IsEqualTo(sourceStream.Position, 0);
        Guard.IsEqualTo(backingStream.Position, 0);
        var lazySeekStream = new LazySeekStream(sourceStream, backingStream);

        // Full read once via lazy seek to dump source into destination
        lazySeekStream.Seek(0, SeekOrigin.End);
        lazySeekStream.Seek(0, SeekOrigin.Begin);
        
        // Rewind for check 
        sourceStream.Seek(0, SeekOrigin.Begin);
        backingStream.Seek(0, SeekOrigin.Begin);

        // Verify contents of destination are identical
        await AssertStreamEqualAsync(sourceStream, backingStream, bufferSize, CancellationToken.None);
    }

    [DataRow(1000, 100, 10)]
    [DataRow(100, 100, 100)]
    [DataRow(10, 100, 10)]
    [DataRow(10, 10, 100)]
    [DataRow(100, 1000, 10)]
    [DataRow(100, 1000, 100)]
    [DataRow(int.MaxValue - 1024, (long)int.MaxValue + 1024, 81290)]
    [TestMethod]
    public void CanReadWriteBackingBeyondSourceLength(int sourceCapacity, long destinationLengthToWrite, int bufferSize)
    {
        // Memory stream is limited to 2GB by default, but we can adjust it for test purposes
        using var sourceStream = new MemoryStream()
        {
            Capacity = sourceCapacity,
        };

        var tmpPath = Path.GetTempFileName();
        using var backingStream = new FileStream(tmpPath, FileMode.Create);

        var lazySeekStream = new LazySeekStream(sourceStream, backingStream);

        // Write
        var buffer = new byte[bufferSize];
        var remainingBytes = destinationLengthToWrite;
        while (remainingBytes > 0)
        {
            if (remainingBytes < buffer.Length)
                buffer = new byte[remainingBytes];

            lazySeekStream.Write(buffer);
            remainingBytes -= buffer.Length;
        }

        Assert.AreEqual(0, remainingBytes);
        Assert.AreNotEqual(0, lazySeekStream.Length);
        Assert.AreEqual(destinationLengthToWrite, lazySeekStream.Position);

        // Rewind
        var newPos = 0;
        Assert.AreEqual(newPos, lazySeekStream.Seek(newPos, SeekOrigin.Begin));

        // Read back
        remainingBytes = lazySeekStream.Length;
        Assert.AreNotEqual(0, remainingBytes);
        Assert.AreEqual(remainingBytes, destinationLengthToWrite);

        while (remainingBytes > 0)
        {
            if (remainingBytes < buffer.Length)
                buffer = new byte[remainingBytes];

            remainingBytes -= lazySeekStream.Read(buffer);
        }

        Assert.AreEqual(0, remainingBytes);
        Assert.AreEqual(lazySeekStream.Length, lazySeekStream.Position);
    }

    private static async Task<int> WriteRandomBytesAsync(Stream stream, long numberOfBytes, int bufferSize, CancellationToken cancellationToken)
    {
        var rnd = new Random();
        var bytes = new byte[bufferSize];
        var bytesWritten = 0L;
        while (bytesWritten < numberOfBytes)
        {
            var remaining = numberOfBytes - bytesWritten;

            // Always runs if there are bytes left, even if there's fewer bytes left than the buffer.
            // Truncate the buffer size to remaining length if smaller than buffer.
            if (bufferSize > remaining)
                bufferSize = (int)remaining;

            if (bytes.Length != bufferSize)
                bytes = new byte[bufferSize];

            rnd.NextBytes(bytes);

            await stream.WriteAsync(bytes, cancellationToken);
            bytesWritten += bufferSize;
        }

        return bufferSize;
    }


    private static async Task AssertStreamEqualAsync(Stream srcStream, Stream destStream, int bufferSize, CancellationToken cancellationToken)
    {
        Assert.AreEqual(srcStream.Length, destStream.Length);

        var totalBytes = srcStream.Length;
        var bytesChecked = 0L;

        var srcBuffer = new byte[bufferSize];
        var destBuffer = new byte[bufferSize];

        // Fill each buffer until bufferSize is reached.
        // Each stream must fill the buffer until it is full,
        // except if no bytes are left.
        while (bytesChecked < totalBytes)
        {
            var srcBytesRead = 0;
            while (srcBytesRead < srcBuffer.Length)
            {
                var remainingBufferToFill = srcBuffer.Length - srcBytesRead;
                if (remainingBufferToFill == 0)
                    break;
                
                var srcBytesReadInternal = await srcStream.ReadAsync(srcBuffer, offset: srcBytesRead, count: srcBuffer.Length - srcBytesRead, cancellationToken);
                if (srcBytesReadInternal == 0)
                    break;

                srcBytesRead += srcBytesReadInternal;
            }

            var destBytesRead = 0;
            while (destBytesRead < destBuffer.Length)
            {
                var remainingBufferToFill = destBuffer.Length - destBytesRead;
                if (remainingBufferToFill == 0)
                    break;
                
                var destBytesReadInternal = await destStream.ReadAsync(destBuffer, offset: destBytesRead, count: remainingBufferToFill, cancellationToken);
                if (destBytesReadInternal == 0)
                    break;

                destBytesRead += destBytesReadInternal;
            }

            if (srcBytesRead != destBytesRead)
            {
                throw new InvalidOperationException($"Mismatch in bytes read between source and destination streams: src {srcBytesRead}, dest {destBytesRead}.");
            }

            // When buffers are full, compare and continue.
            CollectionAssert.AreEqual(destBuffer, srcBuffer);
            bytesChecked += srcBytesRead;
        }

        Assert.AreEqual(srcStream.Length, destStream.Length);
    }
}
