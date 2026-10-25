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
            List<(RoleMask, EnemyTier)> picks = FillBudget(budget, config, waveNumber);
            Shuffle(picks);

            float duration = config.SpawnDurationBase + (waveNumber - 1) * config.SpawnDurationPerWave;
            List<SpawnPulse> pulses = BuildPulses(picks, duration, config);

            return new WaveDefinition
            {
                WaveNumber = waveNumber,
                SpawnDuration = duration,
                TotalEnemies = picks.Count,
                Pulses = pulses,
            };
        }

        /// <summary>
        /// Spends the budget by repeatedly picking enemy types until nothing affordable remains.
        /// </summary>
        private static List<(RoleMask, EnemyTier)> FillBudget(int budget, WaveGeneratorConfig config, int waveNumber)
        {
            List<(RoleMask, EnemyTier)> picks = new();

            int remaining = budget;
            while (remaining > 0)
            {
                EnemyTypeEntry? picked = WeightedPick(config.EnemyTypes, remaining, waveNumber);
                if (picked == null)
                {
                    Debug.LogWarning($"[WaveGenerator] No affordable entry found with {remaining} budget remaining on wave {waveNumber}. Stopping early.");
                    break;
                }

                picks.Add((picked.Value.Type, picked.Value.Tier));
                remaining -= picked.Value.Cost;
            }

            return picks;
        }

        /// <summary>
        /// Splits the picked enemies into pulses spread over the spawn duration.
        /// Pulses grow linearly so the last is IntensityRamp times the size of the first.
        /// </summary>//
        private static List<SpawnPulse> BuildPulses(List<(RoleMask, EnemyTier)> picks, float duration, WaveGeneratorConfig config)
        {
            int pulseCount = Mathf.Max(1, Mathf.FloorToInt(duration / config.PulseInterval));
            int[] sizes = DistributeByRamp(picks.Count, pulseCount, config.IntensityRamp);

            List<SpawnPulse> pulses = new(pulseCount);
            int next = 0;
            for (int i = 0; i < pulseCount; i++)
            {
                if (sizes[i] == 0) continue;

                Dictionary<(RoleMask, EnemyTier), int> counts = new();
                for (int j = 0; j < sizes[i]; j++, next++)
                {
                    counts[picks[next]] = counts.TryGetValue(picks[next], out int existing) ? existing + 1 : 1;
                }

                pulses.Add(new SpawnPulse
                {
                    Time = i * config.PulseInterval,
                    Angle = Random.Range(0f, Mathf.PI * 2f),
                    Groups = counts.Select(kv => new SpawnGroup { Type = kv.Key.Item1, Tier = kv.Key.Item2, Count = kv.Value }).ToList(),
                });
            }

            return pulses;
        }

        /// <summary>
        /// Distributes total over count slots, weighted by a linear ramp.
        /// </summary>
        private static int[] DistributeByRamp(int total, int countSlots, float ramp)
        {
            float[] weights = new float[countSlots];
            for (int i = 0; i < countSlots; i++)
            {
                float t = countSlots > 1 ? (float)i / (countSlots - 1) : 0f;
                weights[i] = Mathf.Lerp(1f, ramp, t);
            }

            float weightSum = weights.Sum();
            int[] sizes = new int[countSlots];
            float[] remainders = new float[countSlots];
            int assigned = 0;

            for (int i = 0; i < countSlots; i++)
            {
                float exact = total * weights[i] / weightSum;
                sizes[i] = Mathf.FloorToInt(exact);
                remainders[i] = exact - sizes[i];
                assigned += sizes[i];
            }

            foreach (int i in Enumerable.Range(0, countSlots).OrderByDescending(i => remainders[i]).Take(total - assigned))
            {
                sizes[i]++;
            }

            return sizes;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
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
