using System.Collections.Generic;

namespace OwlCore.ComponentModel;

/// <summary>
/// Indicates an object that has a list of sources that cannot be modified.
/// </summary>
/// <typeparam name="T">The inner collection type.</typeparam>
public interface IReadOnlySources<T>
{
    /// <summary>
    /// The sources for the object.
    /// </summary>
    IReadOnlyCollection<T> Sources { get; init; }
}
