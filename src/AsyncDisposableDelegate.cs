using System;
using System.Threading.Tasks;

namespace OwlCore.ComponentModel;

/// <summary>
/// An implementation of <see cref="IAsyncDisposable"/> that calls an <see cref="Func{Task}"/> when disposed.
/// </summary>
public sealed class AsyncDisposableDelegate : IAsyncDisposable, IDelegable<Func<Task>>
{
    /// <summary>
    /// The inner <see cref="Func{Task}"/> that is invoked when <see cref="IAsyncDisposable.DisposeAsync"/> is called.
    /// </summary>
    public required Func<Task> Inner { get; init; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new ValueTask(Inner());
}