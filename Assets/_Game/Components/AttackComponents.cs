using Unity.Entities;

namespace _Game.Components
{
    public struct AttackStats : IComponentData
    {
        // Base Values (Authored)
        public int BaseArmorPenetration;

        // Result Values (Calculated)
        public int ArmorPenetration;
    }
}
