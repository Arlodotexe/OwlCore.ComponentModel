using System;
using System.Collections.Generic;

namespace OwlCore.ComponentModel;

/// <summary>
/// Aggregates many <see cref="IDisposable"/> into a single <see cref="IDisposable"/>, disposing all aggregates instances together.
/// </summary>
public class AggregateDisposable : IDisposable, IDelegable<IEnumerable<IDisposable>>
{
    /// <summary>
    /// Disposes all disposable instances in <see cref="Inner"/>.
    /// </summary>
    public void Dispose()
    {
        foreach(var disposable in Inner)
            disposable.Dispose();
    }

    /// <summary>
    /// The instances of <see cref="IDisposable"/> to dispose when <see cref="Dispose"/> is called.
    /// </summary>
    public required IEnumerable<IDisposable> Inner { get; init; }
}