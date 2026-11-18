using Unity.Entities;

namespace _Game.Components
{
    public struct AttackStats : IComponentData
    {
        // Base Values (Authored)
        public int BaseArmorPenetration;
        public int BaseCritChance;
        public int BaseCritDamage;

        // Result Values (Calculated)
        public int ArmorPenetration;
        public int CritChance;
        public int CritDamage;

        // Extra damage in percent vs. elite / boss targets. Base is 0, only modifiers raise it.
        public int BonusDamageElite;
        public int BonusDamageBoss;
    }
}
