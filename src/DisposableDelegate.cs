using System;

namespace OwlCore.ComponentModel;

/// <summary>
/// An implementation of <see cref="IDisposable"/> that calls an <see cref="Action"/> when disposed.
/// </summary>
public sealed class DisposableDelegate : IDisposable, IDelegable<Action>
{
    /// <summary>
    /// The inner <see cref="Action"/> that is invoked when <see cref="IDisposable.Dispose"/> is called.
    /// </summary>
    public required Action Inner { get; init; }

    /// <inheritdoc />
    public void Dispose() => Inner();
}