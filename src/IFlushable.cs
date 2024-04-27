using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// Represents a resource that can be flushed from memory to an underlying source.
/// </summary>
public interface IFlushable
{
    /// <summary>
    /// Flushes the resource to the underlying device.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    Task FlushAsync(CancellationToken cancellationToken);
}