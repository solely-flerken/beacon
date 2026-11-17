using System;
using Unity.Entities;

namespace _Game.Components
{
    public struct EnemyTag : IComponentData { }

    /// <summary>
    /// Added (disabled) at bake time. Enabled means the entity is removed by DespawnSystem at the end of the frame:
    /// disabled = alive, enabled + Killed = died (EnemyDeathSystem sends rewards), enabled + !Killed = despawned without rewards.
    /// </summary>
    public struct Despawn : IComponentData, IEnableableComponent
    {
        public bool Killed;
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
        public float BaseExpValue;
        public float ExpValue;
    }

    /// <summary>
    /// Random per-enemy speed variation as a fraction: base speed * (1 ± Value), rolled once at spawn.
    /// </summary>
    public struct EnemySpeedVariation : IComponentData
    {
        public float Value;
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

    /// <summary>
    /// Buffer on the catalog entity mapping type IDs to prefab and type entities.
    /// </summary>
    public struct EnemyTypeCatalogEntry : IBufferElementData
    {
        public RoleMask TypeId;
        public Entity Prefab;
        public Entity TypeEntity;
    }
    
    /// <summary>
    /// Queued enemy group on the spawn queue singleton, consumed by EnemySpawnSystem.
    /// Enemies appear on an arc of the spawn ring around the beacon's position at spawn time.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnemySpawnRequest : IBufferElementData
    {
        public RoleMask Type;
        public EnemyTier Tier;
        public int Count;
        public float ArcCenter; // Radians
        public float ArcWidth; // Radians
    }

    [InternalBufferCapacity(4)]
    public struct HitCooldown : IBufferElementData
    {
        public Entity Source;
        public float Expiry;
    }
}
