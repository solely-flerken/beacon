using System;
using _Game.Components;

namespace _Game.Gameplay.Waves
{
    /// <summary>
    /// Defines the stat modifiers applied to an enemy when it spawns as an elite.
    /// </summary>
    public static class EliteLibrary
    {
        private static StatModifier Mod(StatType stat, ModifierType type, float value)
        {
            return new StatModifier { Stat = stat, Type = type, Value = value };
        }

        public static StatModifier[] Get(EnemyType type) => type switch
        {
            EnemyType.Basic => new[]
            {
                Mod(StatType.EnemyMaxHealth, ModifierType.Multiplicative, 100f),
            },
            EnemyType.Fast => new[]
            {
                Mod(StatType.EnemyMaxHealth, ModifierType.Multiplicative, 50f),
                Mod(StatType.EnemySpeed, ModifierType.Multiplicative, 50f),
            },
            EnemyType.Tank => new[]
            {
                Mod(StatType.EnemyMaxHealth, ModifierType.Multiplicative, 150f),
                Mod(StatType.EnemyContactDamage, ModifierType.Multiplicative, 50f),
            },
            _ => Array.Empty<StatModifier>(),
        };
    }
}