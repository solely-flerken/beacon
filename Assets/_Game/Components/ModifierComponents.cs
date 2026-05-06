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

    public struct GlobalStatModifier : IBufferElementData
    {
        public StatType Stat;
        public Classification Mask;
        public ModifierType Type;
        public float Value;
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
}