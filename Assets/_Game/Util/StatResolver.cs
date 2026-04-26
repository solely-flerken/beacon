using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Util
{
    public static class StatResolver
    {
        /// <summary>
        /// Resolves a stat across all three modifier layers for a single enemy.
        /// Enemy Type Tier Mods, Enemy Type Mods, Individual Enemy Mods
        /// </summary>
        public static float ResolveEnemy(float baseValue, ref BlobArray<TieredStatModifier> blobMods, DynamicBuffer<StatModifier> typeMods, DynamicBuffer<StatModifier> entityMods, StatType statType, EnemyTier tier)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;
            Accumulate(ref blobMods, statType, tier, ref additive, ref multiplicativeBonus);
            Accumulate(typeMods, statType, ref additive, ref multiplicativeBonus);
            Accumulate(entityMods, statType, ref additive, ref multiplicativeBonus);
            return Apply(baseValue, additive, multiplicativeBonus);
        }

        /// <summary>
        /// Resolves a stat from local and global modifier buffers combined.
        /// </summary>
        public static float Resolve(float baseValue, DynamicBuffer<StatModifier> localMods, DynamicBuffer<GlobalStatModifier> globalMods, StatType statType)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;
            Accumulate(localMods, statType, ref additive, ref multiplicativeBonus);
            Accumulate(globalMods, statType, ref additive, ref multiplicativeBonus);
            return Apply(baseValue, additive, multiplicativeBonus);
        }
        
        /// <summary>Accumulates tier-filtered mods from the blob into running totals.</summary>
        /// <remarks>Normal-tier entries apply to all tiers; Elite/Boss entries apply only to their tier.</remarks>
        private static void Accumulate(
            ref BlobArray<TieredStatModifier> mods,
            StatType statType,
            EnemyTier currentTier,
            ref float additive,
            ref float multiplicativeBonus)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                if (mods[i].Modifier.Stat != statType) continue;
                
                // Modifier to EnemyTier.Normal applies to all tiers
                if (mods[i].Tier != currentTier && mods[i].Tier != EnemyTier.Normal) continue;

                if (mods[i].Modifier.Type == ModifierType.Additive)
                {
                    additive += mods[i].Modifier.Value;
                }
                else
                {
                    multiplicativeBonus += mods[i].Modifier.Value / 100f;
                }
            }
        }

        /// <summary>
        /// Accumulates mods from a buffer into running totals.
        /// </summary>
        private static void Accumulate(DynamicBuffer<StatModifier> mods, StatType statType, ref float additive, ref float multiplicativeBonus)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                if (mods[i].Stat != statType) continue;

                if (mods[i].Type == ModifierType.Additive)
                {
                    additive += mods[i].Value;
                }
                else
                {
                    multiplicativeBonus += mods[i].Value / 100f;
                }
            }
        }

        /// <summary>
        /// Accumulates mods from a global modifier buffer into running totals.
        /// </summary>
        private static void Accumulate(DynamicBuffer<GlobalStatModifier> mods, StatType statType, ref float additive, ref float multiplicativeBonus)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                if (mods[i].Stat != statType) continue;

                if (mods[i].Type == ModifierType.Additive)
                {
                    additive += mods[i].Value;
                }
                else
                {
                    multiplicativeBonus += mods[i].Value / 100f;
                }
            }
        }

        /// <summary>
        /// Applies accumulated totals: (base + additive) * (1 + multiplicativeBonus).
        /// </summary>
        private static float Apply(float baseValue, float additive, float multiplicativeBonus)
        {
            return Mathf.Max(0f, (baseValue + additive) * (1f + multiplicativeBonus));
        }
    }
}
