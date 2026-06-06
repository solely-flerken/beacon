using System;
using System.Collections.Generic;
using _Game.Components;
using Unity.Mathematics;

namespace _Game.Util
{
    public static class ManagedStatResolver
    {
        private struct ModAccumulator
        {
            private float _additive;
            private float _multiplicativeBonus;
            private bool _hasOverwrite;
            private float _overwriteValue;

            public void Add(ModifierType type, float value)
            {
                switch (type)
                {
                    case ModifierType.Additive:
                        _additive += value;
                        break;
                    case ModifierType.Multiplicative:
                        _multiplicativeBonus += value / 100f;
                        break;
                    case ModifierType.Overwrite:
                        _hasOverwrite = true;
                        _overwriteValue = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public float Resolve(float baseValue)
            {
                if (_hasOverwrite) return math.max(0f, _overwriteValue);

                float flat = math.max(0f, baseValue + _additive);
                float mult = math.max(0f, 1f + _multiplicativeBonus);

                return flat * mult;
            }
        }

        public static float Resolve<TStatType>(TStatType stat, float baseValue, List<Modifier<TStatType>> mods) where TStatType : struct, Enum
        {
            ModAccumulator acc = default;

            foreach (Modifier<TStatType> mod in mods)
            {
                if (!mod.Stat.Equals(stat)) continue;

                acc.Add(mod.Type, mod.Value);
            }

            return acc.Resolve(baseValue);
        }
    }
}
