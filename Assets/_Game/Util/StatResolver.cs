using System;
using _Game.Components;
using Unity.Entities;

namespace _Game.Util
{
    public static class StatResolver
    {
        // TODO: Check multiplicative calculation
        public static float Resolve(float baseValue, DynamicBuffer<StatModifier> localMods, StatType statType)
        {
            float additive = 0f;
            float multiplicative = 1f;

            if (localMods.IsCreated)
            {
                for (int i = 0; i < localMods.Length; i++)
                {
                    if (localMods[i].Stat != statType) continue;

                    switch (localMods[i].Type)
                    {
                        case ModifierType.Additive:
                            additive += localMods[i].Value;
                            break;
                        case ModifierType.Multiplicative:
                            multiplicative *= localMods[i].Value;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return (baseValue + additive) * multiplicative;
        }

        public static float Resolve(float baseValue, DynamicBuffer<StatModifier> localMods, DynamicBuffer<GlobalStatModifier> globalMods, StatType statType)
        {
            float additive = 0f;
            float multiplicative = 1f;

            // Apply Local Modifiers
            if (localMods.IsCreated)
            {
                for (int i = 0; i < localMods.Length; i++)
                {
                    if (localMods[i].Stat != statType) continue;

                    switch (localMods[i].Type)
                    {
                        case ModifierType.Additive:
                            additive += localMods[i].Value;
                            break;
                        case ModifierType.Multiplicative:
                            multiplicative *= localMods[i].Value;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            // Apply Global Modifiers
            if (globalMods.IsCreated)
            {
                for (int i = 0; i < globalMods.Length; i++)
                {
                    if (globalMods[i].Stat != statType) continue;

                    switch (globalMods[i].Type)
                    {
                        case ModifierType.Additive:
                            additive += globalMods[i].Value;
                            break;
                        case ModifierType.Multiplicative:
                            multiplicative *= globalMods[i].Value;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return (baseValue + additive) * multiplicative;
        }
    }
}