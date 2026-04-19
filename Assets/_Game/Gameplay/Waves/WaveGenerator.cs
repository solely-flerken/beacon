using System.Collections.Generic;
using System.Linq;
using _Game.Components;
using UnityEngine;

namespace _Game.Gameplay.Waves
{
    public static class WaveGenerator
    {
        public static WaveDefinition Generate(int waveNumber, WaveGeneratorConfig config)
        {
            int budget = config.BudgetBase + waveNumber * config.BudgetPerWave;
            Dictionary<(EnemyType, EnemyTier), int> counts = FillBudget(budget, config, waveNumber);

            List<SpawnGroup> groups = new();
            foreach (((EnemyType type, EnemyTier tier), int count) in counts)
            {
                if (count <= 0) continue;

                groups.Add(new SpawnGroup
                {
                    Type = type, 
                    Tier = tier, 
                    Count = count
                });
            }

            return new WaveDefinition { WaveNumber = waveNumber, SpawnGroups = groups };
        }

        /// <summary>
        /// Spends the budget by repeatedly picking enemy types until nothing affordable remains.
        /// </summary>
        private static Dictionary<(EnemyType, EnemyTier), int> FillBudget(int budget, WaveGeneratorConfig config, int waveNumber)
        {
            Dictionary<(EnemyType, EnemyTier), int> counts = new();

            int remaining = budget;
            while (remaining > 0)
            {
                EnemyTypeEntry? picked = WeightedPick(config.EnemyTypes, remaining, waveNumber);
                if (picked == null)
                {
                    Debug.LogWarning($"[WaveGenerator] No affordable entry found with {remaining} budget remaining on wave {waveNumber}. Stopping early.");
                    break;
                }

                (EnemyType, EnemyTier) key = (picked.Value.Type, picked.Value.Tier);
                counts[key] = counts.TryGetValue(key, out int existing) ? existing + 1 : 1;
                remaining -= picked.Value.Cost;
            }

            return counts;
        }

        /// <summary>
        /// Picks one enemy type by weight, excluding types that cost more than the remaining budget.
        /// </summary>
        private static EnemyTypeEntry? WeightedPick(EnemyTypeEntry[] types, int remainingBudget, int wave)
        {
            float totalWeight = types.Where(entry => entry.Cost <= remainingBudget).Sum(entry => EffectiveWeight(entry, wave));

            if (totalWeight <= 0f) return null;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (EnemyTypeEntry entry in types)
            {
                if (entry.Cost > remainingBudget) continue;
                
                cumulative += EffectiveWeight(entry, wave);
                if (roll >= cumulative) continue;
                    
                return entry;
            }

            return null;
        }

        /// <summary>
        /// Returns the spawn weight for a (type, tier) entry at the current wave.
        /// Returns 0 before StartWave, full Weight once StartWave is reached.
        /// </summary>
        private static float EffectiveWeight(EnemyTypeEntry entry, int wave)
        {
            return wave >= entry.StartWave ? entry.Weight : 0f;
        }
    }
}
