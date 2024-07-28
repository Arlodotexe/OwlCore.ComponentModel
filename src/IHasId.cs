namespace OwlCore.ComponentModel;

/// <summary>
/// Represents an object with a unique instance Id. This Id should be identical across runs and environments.
/// </summary>
public interface IHasId
{
    /// <summary>
    /// An Id corresponding to this object instance. This Id should be unique for the object, but identical across runs and environments.
    /// </summary>
    string Id { get; }
}
