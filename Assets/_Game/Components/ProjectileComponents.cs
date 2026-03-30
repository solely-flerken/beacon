using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Components
{
    /// <summary>
    /// Marks an entity as a projectile.
    /// </summary>
    public struct ProjectileTag : IComponentData { }

    /// <summary>
    /// Runtime velocity — direction x speed.
    /// Zero at bake time.
    /// </summary>
    public struct ProjectileVelocity : IComponentData
    {
        public float2 Value;
    }

    /// <summary>
    /// Damage dealt on hit.
    /// </summary>
    public struct ProjectileDamage : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// How many enemies this projectile can pass through before being destroyed.
    /// </summary>
    public struct ProjectilePierce : IComponentData
    {
        public int Remaining;
    }
}