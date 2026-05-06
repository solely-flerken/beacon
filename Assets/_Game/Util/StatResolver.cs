using System;
using _Game.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Util
{
    public static class StatResolver
    {
        /// <summary>
        /// Resolves a stat for a generic entity by accumulating global mods (filtered by classification),
        /// up to two local mod buffers, then applying the final formula.
        /// </summary>
        public static float Resolve(
            StatType statType,
            float baseValue,
            Classification mask,
            DynamicBuffer<GlobalStatModifier> globalMods,
            DynamicBuffer<StatModifier> localMods1,
            DynamicBuffer<StatModifier> localMods2 = default)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;

            if (globalMods.IsCreated)
                Accumulate(statType, mask, globalMods, ref additive, ref multiplicativeBonus);

            if (localMods1.IsCreated)
                Accumulate(statType, localMods1, ref additive, ref multiplicativeBonus);

            if (localMods2.IsCreated)
                Accumulate(statType, localMods2, ref additive, ref multiplicativeBonus);

            return Apply(baseValue, additive, multiplicativeBonus);
        }

        /// <summary>
        /// Resolves a weapon stat. Like Resolve but also applies blob-asset tier mods
        /// from the owning enemy's type blob. Pass a default BlobAssetReference when
        /// there is no owning enemy type (e.g. beacon-owned weapons).
        /// </summary>
        public static float ResolveWeapon(
            StatType statType,
            float baseValue,
            Classification mask,
            DynamicBuffer<GlobalStatModifier> globalMods,
            DynamicBuffer<StatModifier> localMods1,
            DynamicBuffer<StatModifier> localMods2,
            BlobAssetReference<EnemyTypeBlob> blobRef)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;

            if (globalMods.IsCreated)
                Accumulate(statType, mask, globalMods, ref additive, ref multiplicativeBonus);

            if (localMods1.IsCreated)
                Accumulate(statType, localMods1, ref additive, ref multiplicativeBonus);

            if (localMods2.IsCreated)
                Accumulate(statType, localMods2, ref additive, ref multiplicativeBonus);

            if (blobRef.IsCreated)
            {
                EnemyTier tier = mask.Tier != TierMask.None ? TierMaskMap.Get(mask.Tier) : EnemyTier.Normal;
                ref BlobArray<TieredStatModifier> blobMods = ref blobRef.Value.ConfigModifiers;
                if (blobMods.Length != 0)
                {
                    Accumulate(statType, tier, ref blobMods, ref additive, ref multiplicativeBonus);
                }
            }

            return Apply(baseValue, additive, multiplicativeBonus);
        }

        /// <summary>
        /// Resolves a stat for an enemy entity. Applies global mods (classification-filtered),
        /// local instance mods, and blob-asset tier mods
        /// (Normal tier entries always apply; tier-specific entries only apply when the enemy's tier matches).
        /// </summary>
        public static float ResolveEnemy(
            EnemyTier tier,
            StatType statType,
            float baseValue,
            Classification mask,
            DynamicBuffer<GlobalStatModifier> globalMods,
            DynamicBuffer<StatModifier> localMods,
            ref BlobArray<TieredStatModifier> blobMods)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;

            if (globalMods.IsCreated)
                Accumulate(statType, mask, globalMods, ref additive, ref multiplicativeBonus);

            if (localMods.IsCreated)
                Accumulate(statType, localMods, ref additive, ref multiplicativeBonus);

            if (blobMods.Length != 0)
                Accumulate(statType, tier, ref blobMods, ref additive, ref multiplicativeBonus);

            return Apply(baseValue, additive, multiplicativeBonus);
        }

        /// <summary>
        /// Accumulates global mods into additive/multiplicative accumulators,
        /// skipping any mod whose classification mask does not match the entity.
        /// </summary>
        private static void Accumulate(
            StatType statType,
            Classification entityMask,
            DynamicBuffer<GlobalStatModifier> mods,
            ref float additive,
            ref float multiplicative)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                GlobalStatModifier mod = mods[i];
                
                if (mod.Stat != statType) continue;
                if (!IsMatch(entityMask, mod.Mask)) continue;

                AddMod(mod.Type, mod.Value, ref additive, ref multiplicative);
            }
        }

        /// <summary>
        /// Accumulates blob-asset tier mods. Normal-tier entries apply to all enemies;
        /// non-Normal entries only apply when the enemy's current tier matches exactly.
        /// </summary>
        private static void Accumulate(
            StatType statType,
            EnemyTier currentTier,
            ref BlobArray<TieredStatModifier> mods,
            ref float additive,
            ref float multiplicativeBonus)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                TieredStatModifier mod = mods[i];
                
                if (mod.Modifier.Stat != statType) continue;
                if (mod.Tier != currentTier && mod.Tier != EnemyTier.Normal) continue;

                AddMod(mod.Modifier.Type, mod.Modifier.Value, ref additive, ref multiplicativeBonus);
            }
        }

        /// <summary>
        /// Accumulates local (per-entity) stat mods. Multiplicative values are stored
        /// as percentages and divided by 100 here.
        /// </summary>
        private static void Accumulate(
            StatType statType, 
            DynamicBuffer<StatModifier> mods, 
            ref float additive, 
            ref float multiplicativeBonus)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                StatModifier mod = mods[i];
                
                if (mod.Stat != statType) continue;
                
                AddMod(mod.Type, mod.Value, ref additive, ref multiplicativeBonus);
            }
        }

        /// <summary>
        /// Adds a single modifier value into the correct accumulator.
        /// Multiplicative values are authored as percentages and converted to decimal here.
        /// </summary>
        private static void AddMod(ModifierType type, float value, ref float additive, ref float multiplicative)
        {
            switch (type)
            {
                case ModifierType.Additive:
                    additive += value;
                    break;
                case ModifierType.Multiplicative:
                    multiplicative += value / 100f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Final math: (Base + Flat) * (1 + %Bonus)
        /// </summary>
        private static float Apply(float baseValue, float additive, float multiplicativeBonus)
        {
            return math.max(0f, (baseValue + additive) * (1f + multiplicativeBonus));
        }

        /// <summary>
        /// Returns true if the entity's classification satisfies all non-zero criteria fields.
        /// Each field is a bitmask: the entity must share at least one bit with the criteria.
        /// A zero criteria field is treated as "any" (no restriction).
        /// </summary>
        private static bool IsMatch(Classification entity, Classification criteria)
        {
            // 1. If criteria requires a specific Target (e.g. Weapon),
            // the entity must have that bit set.
            if (criteria.Target != 0 && (entity.Target & criteria.Target) == 0) return false;

            // 2. If criteria requires a specific Faction (e.g. Player),
            // the entity must have that bit set.
            if (criteria.Faction != 0 && (entity.Faction & criteria.Faction) == 0) return false;

            // 3. If criteria requires a Role (e.g. Fast OR Tank),
            // the entity must match at least one of those roles.
            if (criteria.Role != 0 && (entity.Role & criteria.Role) == 0) return false;

            // 4. If criteria requires a Tier (e.g. Elite),
            // the entity must match.
            if (criteria.Tier != 0 && (entity.Tier & criteria.Tier) == 0) return false;

            return true;
        }
    }
}
