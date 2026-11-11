using Unity.Entities;

namespace _Game.Components
{
    public struct Health : IComponentData
    {
        public float BaseMax;
        public float Max;
        public float Current;
    }

    /// <summary>
    /// Reduces incoming damage with diminishing returns.
    /// Every 100 armor adds one more base life bar of hits: 100 armor = 50% damage taken, 200 = 33%, 300 = 25%.
    /// Negative armor increases damage taken, up to at most double.
    /// </summary>
    public struct Armor : IComponentData
    {
        public float BaseValue;
        public float Value;

        /// <summary>
        /// Fraction of the raw damage that gets through.
        /// </summary>
        public static float DamageMultiplier(float armor)
        {
            return armor >= 0f ? 100f / (100f + armor) : 2f - 100f / (100f - armor);
        }

        /// <summary>
        /// Armor left after the attacker's penetration.
        /// Only strips positive armor, so it does nothing against unarmored or negative-armor targets.
        /// </summary>
        public static float AfterPenetration(float armor, int penetrationPercent)
        {
            if (armor <= 0f) return armor;
            return armor * (1f - Unity.Mathematics.math.saturate(penetrationPercent * 0.01f));
        }
    }
}
