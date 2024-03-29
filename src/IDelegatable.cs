﻿namespace OwlCore.ComponentModel
{
    /// <summary>
    /// Indicates that the class is holding a reference to an implementation of <typeparamref name="T"/>, to which public properties, events or methods may be delegated to.
    /// </summary>
    /// <typeparam name="T">The type that is delegated to.</typeparam>
    [System.Obsolete("This interface will be removed in favor of IDelegable<T> in a future release. This interface now inherits IDelegable<T>, allowing for a gradual migration.")]
    public interface IDelegatable<T> : IDelegable<T>
        where T : class
    {
    }

}
