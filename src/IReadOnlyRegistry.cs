using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// Represents a read-only registry of items.
/// </summary>
/// <typeparam name="TReadOnly">The type of item that can be read from this registry.</typeparam>
public interface IReadOnlyRegistry<TReadOnly>
{
    /// <summary>
    /// Retrieves the items in the registry.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An async enumerable containing the items in the registry.</returns>
    public IAsyncEnumerable<TReadOnly> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves an item by its id.
    /// </summary>
    /// <param name="itemId">The ID of the item to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public Task<TReadOnly> GetAsync(string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// Raised when items in the registry are added.
    /// </summary>
    public event EventHandler<TReadOnly[]>? ItemsAdded;

    /// <summary>
    /// Raised when items in the registry are removed.
    /// </summary>
    public event EventHandler<TReadOnly[]>? ItemsRemoved;
}
