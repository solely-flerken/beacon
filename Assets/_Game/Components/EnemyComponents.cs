using System;
using Unity.Entities;

namespace _Game.Components
{
    public struct EnemyTag : IComponentData { }

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
    /// Dirty flag. Enable to force EnemyStatUpdateSystem to recalculate.
    /// </summary>
    public struct EnemyStatUpdateRequired : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Reward data.
    /// </summary>
    public struct EnemyReward : IComponentData
    {
        public float BaseExpValue;
        public float ExpValue;
    }

    public struct EnemySpeedVariation : IComponentData
    {
        public float Value;
    }

    public enum EnemyTier : byte { Normal, Elite, Boss }

    public struct TieredStatModifier
    {
        public EnemyTier Tier;
        public StatModifier Modifier;
    }

    public struct EnemyTypeBlob
    {
        public BlobArray<TieredStatModifier> ConfigModifiers;
    }

    public struct EnemyTypeBlobRef : IComponentData
    {
        public BlobAssetReference<EnemyTypeBlob> Value;
    }

    /// <summary>
    /// Marks the shared type entity that holds StatModifier buffers for an enemy type.
    /// </summary>
    public struct EnemyTypeTag : IComponentData { }

    /// <summary>
    /// Points each enemy instance to its shared type entity.
    /// </summary>
    public struct EnemyTypeRef : IComponentData
    {
        public Entity Value;
    }

    public struct EnemyIncomingDamage : IComponentData
    {
        public float Value;
    }

    public struct BasicEnemyTag : IComponentData { }
    public struct FastEnemyTag : IComponentData { }
    public struct TankEnemyTag : IComponentData { }
    public struct RangeEnemyTag : IComponentData { }
    
    /// <summary>
    /// Buffer on the catalog entity mapping type IDs to prefab and type entities.
    /// </summary>
    public struct EnemyTypeCatalogEntry : IBufferElementData
    {
        public RoleMask TypeId;
        public Entity Prefab;
        public Entity TypeEntity;
    }
    
    [InternalBufferCapacity(4)]
    public struct HitCooldown : IBufferElementData
    {
        public Entity Source;
        public float Expiry;
    }

    /// <summary>
    /// Maps EnemyType enum values to their corresponding per-type tag ComponentType.
    /// Use for archetype-level ECS filtering instead of per-entity EnemyTypeRef comparison.
    /// </summary>
    public static class EnemyTypeTagMap
    {
        public static ComponentType Get(RoleMask type) => type switch
        {
            RoleMask.Basic => ComponentType.ReadOnly<BasicEnemyTag>(),
            RoleMask.Fast => ComponentType.ReadOnly<FastEnemyTag>(),
            RoleMask.Tank => ComponentType.ReadOnly<TankEnemyTag>(),
            RoleMask.Range => ComponentType.ReadOnly<RangeEnemyTag>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}