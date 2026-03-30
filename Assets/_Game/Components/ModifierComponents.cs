using Unity.Entities;

namespace _Game.Components
{
    public enum StatType : byte
    {
        Damage,
        FireRate,
        TargetRange,
        ProjectileSpeed,
        Pierce,
    }

    public enum ModifierType : byte
    {
        Additive,
        Multiplicative,
    }
    
    /// <summary>
    /// Dirty flag.
    /// Enable this to force WeaponStatUpdateSystem to recalculate.
    /// </summary>
    public struct StatUpdateRequired : IComponentData, IEnableableComponent { }
    
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