using System;
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

    public enum EnemyTier : byte { Normal, Elite, Boss }
    
    public struct EnemyTierComponent : IComponentData
    {
        public EnemyTier Value;
    }
    
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
    
    public enum EnemyType : byte
    {
        Basic,
        Fast,
        Tank,
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

    public struct BasicEnemyTag : IComponentData { }
    public struct FastEnemyTag : IComponentData { }
    public struct TankEnemyTag : IComponentData { }
    
    /// <summary>
    /// Buffer on the catalog entity mapping type IDs to prefab and type entities.
    /// </summary>
    public struct EnemyTypeCatalogEntry : IBufferElementData
    {
        public EnemyType TypeId;
        public Entity Prefab;
        public Entity TypeEntity;
    }

    /// <summary>
    /// Maps EnemyType enum values to their corresponding per-type tag ComponentType.
    /// Use for archetype-level ECS filtering instead of per-entity EnemyTypeRef comparison.
    /// </summary>
    public static class EnemyTypeTagMap
    {
        public static ComponentType Get(EnemyType type) => type switch
        {
            EnemyType.Basic => ComponentType.ReadOnly<BasicEnemyTag>(),
            EnemyType.Fast  => ComponentType.ReadOnly<FastEnemyTag>(),
            EnemyType.Tank  => ComponentType.ReadOnly<TankEnemyTag>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}