using Unity.Entities;

namespace _Game.Components
{
    public struct EnemyTag : IComponentData { }

    /// <summary>
    /// Movement speed in world units per second.
    /// </summary>
    public struct EnemySpeed : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Damage dealt to the beacon on contact.
    /// Add to any entity that should deal damage when reaching the beacon - enemies, bosses, projectiles, etc.
    /// </summary>
    public struct ContactDamage : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Enemy health.
    /// </summary>
    public struct EnemyHealth : IComponentData
    {
        public float Current;
        public float Max;
    }

    /// <summary>
    /// Reward data.
    /// </summary>
    public struct EnemyReward : IComponentData
    {
        public float ExpValue;
    }

    public struct EnemySpeedVariation : IComponentData
    {
        public float Value;
    }
}