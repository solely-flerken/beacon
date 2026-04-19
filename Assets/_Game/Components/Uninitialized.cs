using Unity.Entities;

namespace _Game.Components
{
    /// <summary>
    /// Plain tag added at bake time. Should be removed after first stat resolve.
    /// Prevents UpdateStatsJob from running before spawn initialization completes.
    /// </summary>
    public struct Uninitialized : IComponentData { }
}