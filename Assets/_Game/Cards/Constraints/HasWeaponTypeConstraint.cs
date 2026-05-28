using _Game.Components;
using _Game.Gameplay;

namespace _Game.Cards.Constraints
{
    /// <summary>
    /// Satisfied when the beacon has at least one weapon of the specified type equipped.
    /// </summary>
    public class HasWeaponTypeConstraint : ICardConstraint
    {
        public WeaponType WeaponType;
        
        public bool IsMet(CardDraftContext ctx) => RunManager.Instance != null && RunManager.Instance.Loadout.HasWeaponOfType(WeaponType);

        public string GetLabel() => $"Requires an equipped {WeaponType.ToString()} weapon";
    }
}
