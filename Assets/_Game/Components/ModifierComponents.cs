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
        ArmorPenetration,
        CritChance,
        CritDamage,
        // Beacon / Enemy
        MaxHp,
        Armor,
        ArrivalRadius,
        Speed,
        ContactDamage,
        // Beam Weapon
        BeamRotationSpeed,
        BeamWidth,
        // Enemy reward
        Exp,
        // Beacon exp
        ExpGain,
        // Projectile ricochet
        BounceCount,
        BounceRange,
        BounceFalloff,
        BounceDamageFloor,
        // Chain lightning
        ChainCount,
        ChainRange,
        ChainFalloff,
        ChainDamageFloor,
    }

    public enum ModifierType : byte
    {
        Additive,
        Multiplicative,
        Overwrite,
    }

    /// <summary>
    /// Modifier for the beacon, weapons or enemies.
    ///
    /// <value name="Value">Percentage as whole number (0-100)</value>
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct StatModifier : IBufferElementData
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
    }

    /// <summary>
    /// Modifier for every entity whose tags match <see cref="Filter"/>.
    /// </summary>
    public struct GlobalStatModifier : IBufferElementData
    {
        public StatType Stat;
        public TagFilter Filter;
        public ModifierType Type;
        public float Value;
        public bool RunScoped;
    }

    /// <summary>
    /// Like <see cref="GlobalStatModifier"/>, but removed once the wave time passes <see cref="ExpiryTime"/>.
    /// </summary>
    public struct WaveStatModifier : IBufferElementData
    {
        public StatType Stat;
        public TagFilter Filter;
        public ModifierType Type;
        public float Value;
        public float ExpiryTime;
    }

    public struct GlobalModSingleton : IComponentData { }
}
