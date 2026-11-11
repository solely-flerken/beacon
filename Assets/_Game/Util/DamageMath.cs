using _Game.Components;
using Unity.Mathematics;

namespace _Game.Util
{
    /// <summary>
    /// Shared damage formulas that combine attacker and target stats.
    /// Single source of truth for how raw damage becomes applied damage.
    /// </summary>
    public static class DamageMath
    {
        /// <summary>
        /// Rolls the attacker's crit. On a crit the damage is scaled by CritDamage percent.
        /// </summary>
        public static float RollCrit(float rawDamage, in AttackStats attacker, ref Random rng)
        {
            if (attacker.CritChance <= 0) return rawDamage;
            if (attacker.CritChance < 100 && rng.NextInt(100) >= attacker.CritChance) return rawDamage;
            return rawDamage * attacker.CritDamage * 0.01f;
        }

        /// <summary>
        /// Damage that gets through the target's armor, after the attacker's armor penetration.
        /// </summary>
        public static float Mitigate(float rawDamage, float targetArmor, in AttackStats attacker)
        {
            float armor = Armor.AfterPenetration(targetArmor, attacker.ArmorPenetration);
            return rawDamage * Armor.DamageMultiplier(armor);
        }
    }
}
