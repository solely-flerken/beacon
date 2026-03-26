using Unity.Entities;

namespace _Game.Components
{
    /// <summary>
    /// Seconds until this projectile is automatically destroyed.
    /// </summary>
    public struct Lifetime : IComponentData
    {
        public float Remaining;
    }
}