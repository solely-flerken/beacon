using _Game.Components;

namespace _Game.Util
{
    /// <summary>
    /// Shared damage formulas that combine attacker and target stats.
    /// Single source of truth for how raw damage becomes applied damage.
    /// </summary>
    public static class DamageMath
    {
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
