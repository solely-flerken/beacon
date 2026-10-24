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
        // Beacon / Enemy
        MaxHp,
        ArrivalRadius,
        Speed,
        ContactDamage,
        // Beam Weapon
        BeamRotationSpeed,
        BeamWidth
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
    public struct StatModifier : IBufferElementData
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
    }

    public struct GlobalStatModifier : IBufferElementData
    {
        public StatType Stat;
        public Classification Mask;
        public ModifierType Type;
        public float Value;
        public bool RunScoped;
    }

    public struct WaveStatModifier : IBufferElementData
    {
        public StatType Stat;
        public Classification Mask;
        public ModifierType Type;
        public float Value;
        public float ExpiryTime;
    }
    
    public struct Classification : IComponentData
    {
        public TargetMask Target;
        public FactionMask Faction;
        public RoleMask Role;
        public TierMask Tier;
    }

    [Flags]
    public enum TargetMask : byte
    {
        None = 0,
        Character = 1 << 0,
        Weapon = 1 << 1
    }

    [Flags]
    public enum FactionMask : byte
    {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1
    }
    
    [Flags]
    public enum RoleMask : byte
    {
        None = 0,
        Basic = 1 << 0,
        Fast = 1 << 1,
        Tank = 1 << 2,
        Range = 1 << 3
    }

    [Flags]
    public enum TierMask : byte
    {
        None = 0,
        Normal = 1 << 0,
        Elite = 1 << 1,
        Boss = 1 << 2
    }
    
    public struct GlobalModSingleton : IComponentData { }
    
    public static class TierMaskMap
    {
        public static TierMask Get(EnemyTier tier) => tier switch
        {
            EnemyTier.Normal => TierMask.Normal,
            EnemyTier.Elite => TierMask.Elite,
            EnemyTier.Boss => TierMask.Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
        };

        public static EnemyTier Get(TierMask mask)
        {
            if ((mask & TierMask.Elite) != 0) return EnemyTier.Elite;
            if ((mask & TierMask.Boss) != 0) return EnemyTier.Boss;
            if ((mask & TierMask.Normal) != 0) return EnemyTier.Normal;

            throw new ArgumentOutOfRangeException(nameof(mask), mask, null);
        }
    }
}