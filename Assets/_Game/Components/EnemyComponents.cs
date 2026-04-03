using Unity.Entities;

namespace _Game.Components
{
    public struct EnemyTag : IComponentData { }

    /// <summary>
    /// Movement speed in world units per second.
    /// </summary>
    public struct EnemySpeed : IComponentData
    {
        public float BaseValue;
        public float Value;
    }

    /// <summary>
    /// Damage dealt to the beacon on contact.
    /// Add to any entity that should deal damage when reaching the beacon - enemies, bosses, projectiles, etc.
    /// </summary>
    public struct ContactDamage : IComponentData
    {
        public float BaseValue;
        public float Value;
    }

    /// <summary>
    /// Enemy health.
    /// </summary>
    public struct EnemyHealth : IComponentData
    {
        public float BaseMax;
        public float Max;
        public float Current;
    }

    /// <summary>
    /// Dirty flag. Enable to force EnemyStatUpdateSystem to recalculate.
    /// </summary>
    public struct EnemyStatUpdateRequired : IComponentData, IEnableableComponent { }

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