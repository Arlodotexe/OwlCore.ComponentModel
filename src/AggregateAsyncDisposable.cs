using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// Aggregates many <see cref="IAsyncDisposable"/> into a single <see cref="IAsyncDisposable"/>, disposing all aggregates instances together.
/// </summary>
public class AggregateAsyncDisposable : IAsyncDisposable, IDelegable<IEnumerable<IAsyncDisposable>>
{
    /// <summary>
    /// Disposes all disposable instances in <see cref="Inner"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach(var disposable in Inner)
            await disposable.DisposeAsync();
    }

    /// <summary>
    /// The instances of <see cref="IAsyncDisposable"/> to dispose when <see cref="DisposeAsync"/> is called.
    /// </summary>
    public required IEnumerable<IAsyncDisposable> Inner { get; init; }
}