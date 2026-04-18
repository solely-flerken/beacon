using Unity.Entities;

namespace _Game.Components
{
    public enum StatType : byte
    {
        // Weapon
        Damage,
        FireRate,
        TargetRange,
        ProjectileSpeed,
        Pierce,
        // Beacon
        MaxHp,
        ArrivalRadius,
        // Enemy
        EnemySpeed,
        EnemyContactDamage,
        EnemyMaxHealth,
    }

    public enum ModifierType : byte
    {
        Additive,
        Multiplicative,
    }

    /// <summary>
    /// Modifier for the beacon, weapons or enemies.
    ///
    /// <param name="Value">Percentage as whole number (0-100)</param>
    /// </summary>
    public struct StatModifier : IBufferElementData
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
    }

    public struct GlobalStatModifier : IBufferElementData
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
    }
}