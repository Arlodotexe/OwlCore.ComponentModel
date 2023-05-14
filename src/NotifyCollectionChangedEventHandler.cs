using System.Collections.Specialized;

namespace OwlCore.ComponentModel;

/// <summary>
/// A strongly typed <see cref="NotifyCollectionChangedEventHandler"/>.
/// </summary>
/// <remarks>
/// Not for use with UI binding. See <see cref="INotifyCollectionChanged"/> instead.
/// </remarks>
/// <param name="sender">The object that raised the event.</param>
/// <param name="e">Information about the event.</param>
public delegate void NotifyCollectionChangedEventHandler<T>(object sender, NotifyCollectionChangedEventArgs<T> e);
