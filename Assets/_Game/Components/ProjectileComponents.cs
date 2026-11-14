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

    /// <summary>
    /// Ricochet: once pierce is used up, the projectile redirects to the nearest enemy it hasn't hit yet.
    /// </summary>
    public struct ProjectileBounce : IComponentData
    {
        public int Remaining;
        public float Range;

        /// <summary>
        /// Damage multiplier applied on each bounce.
        /// </summary>
        public float Falloff;

        /// <summary>
        /// Lowest damage a bounce can reduce to. % relative of the shot damage.
        /// </summary>
        public float MinDamage;
    }

    /// <summary>
    /// Links a projectile back to the weapon entity that fired it, and the weapon owner.
    /// </summary>
    public struct ProjectileSource : IComponentData
    {
        /// <summary>
        /// Weapon that fired it.
        /// </summary>
        public Entity Weapon;

        /// <summary>
        /// Unit that owned the weapon.
        /// </summary>
        public Entity Owner;
    }

    /// <summary>
    /// Tracks the intended target of a projectile for overkill prevention.
    /// </summary>
    public struct ProjectileTarget : IComponentData
    {
        public Entity Value;
        public double ReservationExpiry;
    }
}
