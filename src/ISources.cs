using System.Collections.Generic;

namespace OwlCore.ComponentModel;

/// <summary>
/// Indicates an object that has a reference to a list of sources that can be changed.
/// </summary>
/// <typeparam name="T">The inner collection type.</typeparam>
public interface ISources<T>
{
    /// <summary>
    /// The sources for the object.
    /// </summary>
    ICollection<T> Sources { get; init; }
}
