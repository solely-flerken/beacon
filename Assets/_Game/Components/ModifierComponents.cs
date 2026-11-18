using System;
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
        // Bonus damage vs. target tier (percent)
        BonusDamageElite,
        BonusDamageBoss,
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

    public interface IFilteredModifier
    {
        TagFilter TagFilter { get; }
    }

    /// <summary>
    /// Modifier for every entity whose tags match <see cref="Filter"/>.
    /// </summary>
    public struct GlobalStatModifier : IBufferElementData, IFilteredModifier, IEquatable<GlobalStatModifier>
    {
        public StatType Stat;
        public TagFilter Filter;
        public ModifierType Type;
        public float Value;
        public bool RunScoped;

        public readonly TagFilter TagFilter => Filter;

        public readonly bool Equals(GlobalStatModifier other)
        {
            return Stat == other.Stat
                   && Filter.Equals(other.Filter)
                   && Type == other.Type
                   && Value.Equals(other.Value)
                   && RunScoped == other.RunScoped;
        }
    }

    /// <summary>
    /// Like <see cref="GlobalStatModifier"/>, but removed once the wave time passes <see cref="ExpiryTime"/>.
    /// </summary>
    public struct WaveStatModifier : IBufferElementData, IFilteredModifier, IEquatable<WaveStatModifier>
    {
        public StatType Stat;
        public TagFilter Filter;
        public ModifierType Type;
        public float Value;
        public float ExpiryTime;

        public readonly TagFilter TagFilter => Filter;

        public readonly bool Equals(WaveStatModifier other)
        {
            return Stat == other.Stat
                   && Filter.Equals(other.Filter)
                   && Type == other.Type
                   && Value.Equals(other.Value)
                   && ExpiryTime.Equals(other.ExpiryTime);
        }
    }

    public struct GlobalModSingleton : IComponentData { }
}
