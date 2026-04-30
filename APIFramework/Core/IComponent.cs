namespace APIFramework.Core;

/// <summary>
/// Marker interface implemented by every simulation component type.
/// Analogous to Unity DOTS' <c>IComponentData</c> — components carry only data
/// (typically as <c>struct</c> values) and contain no behaviour; behaviour lives
/// in <see cref="ISystem"/> implementations.
/// </summary>
/// <remarks>
/// Components are stored on entities by <see cref="Type"/> (one component per type
/// per entity) and queried in bulk via <see cref="EntityManager.Query{T}"/>.
/// </remarks>
public interface IComponent { }