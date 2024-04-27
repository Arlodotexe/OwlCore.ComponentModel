using System.Collections.Generic;

namespace OwlCore.ComponentModel;

/// <summary>
/// Indicates an object that has a list of sources that cannot be modified.
/// </summary>
/// <typeparam name="T">The inner collection type.</typeparam>
public interface IReadOnlySources<T>
{
    /// <summary>
    /// The sources for this event stream. Each contains timestamped event data from all participating nodes.
    /// </summary>
    IReadOnlyCollection<T> Sources { get; init; }
}
