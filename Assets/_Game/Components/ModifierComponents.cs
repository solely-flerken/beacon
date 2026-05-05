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
        // Beacon / Enemy
        MaxHp,
        ArrivalRadius,
        Speed,
        ContactDamage,
    }

    public enum ModifierType : byte
    {
        Additive,
        Multiplicative,
    }

    /// <summary>
    /// Modifier for the beacon, weapons or enemies.
    ///
    /// <value name="Value">Percentage as whole number (0-100)</value>
    /// </summary>
    public struct StatModifier : IBufferElementData
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
    }
}