using System;
using _Game.Components;
using Unity.Entities;

namespace _Game.Util
{
    public static class StatResolver
    {
        public static float Resolve(float baseValue, DynamicBuffer<StatModifier> localMods, StatType statType)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;

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
                            multiplicativeBonus += localMods[i].Value / 100f;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return (baseValue + additive) * (1f + multiplicativeBonus);
        }

        public static float Resolve(float baseValue, DynamicBuffer<StatModifier> localMods, DynamicBuffer<GlobalStatModifier> globalMods, StatType statType)
        {
            float additive = 0f;
            float multiplicativeBonus = 0f;

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
                            multiplicativeBonus += localMods[i].Value / 100f;
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
                            multiplicativeBonus += globalMods[i].Value / 100f;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return (baseValue + additive) * (1f + multiplicativeBonus);
        }
    }
}