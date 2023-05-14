using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace OwlCore.ComponentModel;

/// <summary>
/// Provides data for the <see cref="NotifyCollectionChangedEventHandler"/> event delegate.
/// </summary>
/// <typeparam name="T"></typeparam>
public class NotifyCollectionChangedEventArgs<T> : NotifyCollectionChangedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a <see cref="NotifyCollectionChangedAction.Reset"/> change.
    /// </summary>
    /// <param name="action">The action that caused the event. This must be set to <see cref="NotifyCollectionChangedAction.Reset"/>.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action)
        : base(action)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a multi-item change.
    /// </summary>
    /// <param name="action">The action that caused the event.</param>
    /// <param name="changedItems">The items that are affected by the change.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, IList<T> changedItems)
        : base(action, (IList)changedItems)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a one-item change.
    /// </summary>
    /// <param name="action">The action that caused the event.</param>
    /// <param name="changedItem">The item that is affected by the change.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, T changedItem)
        : base(action, changedItem)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a multi-item <see cref="NotifyCollectionChangedAction.Replace"/> change.
    /// </summary>
    /// <param name="action">The action that caused the event. This can only be set to <see cref="NotifyCollectionChangedAction.Replace"/>.</param>
    /// <param name="newItems">The new items that are replacing the original items.</param>
    /// <param name="oldItems">The original items that are replaced.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, IList<T> newItems, IList<T> oldItems)
        : base(action, (IList)newItems, (IList)oldItems)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a multi-item change or a <see cref="NotifyCollectionChangedAction.Reset"/> change.
    /// </summary>
    /// <param name="action">The action that caused the event.</param>
    /// <param name="changedItems">The items affected by the change.</param>
    /// <param name="startingIndex">The index where the change occurred.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, IList<T> changedItems, int startingIndex)
        : base(action, (IList)changedItems, startingIndex)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a one-item change.
    /// </summary>
    /// <param name="action">The action that caused the event.</param>
    /// <param name="changedItem">The item that is affected by the change.</param>
    /// <param name="index">The index where the change occurred.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, T changedItem, int index)
        : base(action, changedItem, index)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a one-item <see cref="NotifyCollectionChangedAction.Replace"/> change.
    /// </summary>
    /// <param name="action">The action that caused the event. This can only be set to <see cref="NotifyCollectionChangedAction.Replace"/>.</param>
    /// <param name="newItem">The new item that is replacing the original item.</param>
    /// <param name="oldItem">The original item that is replaced.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, T newItem, T oldItem)
        : base(action, newItem, oldItem)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyCollectionChangedEventArgs{T}"/> class that describes a one-item <see cref="NotifyCollectionChangedAction.Replace"/> change.
    /// </summary>
    /// <param name="action">The action that caused the event. This can only be set to <see cref="NotifyCollectionChangedAction.Replace"/>.</param>
    /// <param name="newItem">The new item that is replacing the original item.</param>
    /// <param name="oldItem">The original item that is replaced.</param>
    /// <param name="index">The index at which the change occurred.</param>
    public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, T newItem, T oldItem, int index)
        : base(action, newItem, oldItem, index)
    {
    }

    /// <summary>
    /// Gets the list of new items involved in the change.
    /// </summary>
    public new IList<T>? NewItems => base.NewItems as IList<T>;

    /// <summary>
    /// Gets the list of items affected by a System.Collections.Specialized.NotifyCollectionChangedAction.Replace, Remove, or Move action.
    /// </summary>
    public new IList<T>? OldItems => base.OldItems as IList<T>;
}