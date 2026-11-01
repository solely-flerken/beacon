using System.Linq;
using _Game.Components;
using _Game.Gameplay;

namespace _Game.Cards.Constraints
{
    /// <summary>
    /// Satisfied when the beacon has at least one weapon of the specified types equipped.
    /// </summary>
    public class HasAnyWeaponTypeConstraint : ICardConstraint
    {
        public WeaponType[] WeaponTypes;

        public bool IsMet(CardDraftContext ctx)
        {
            if (RunManager.Instance == null) return false;

            foreach (WeaponType type in WeaponTypes)
            {
                if (RunManager.Instance.Loadout.HasWeaponOfType(type)) return true;
            }

            return false;
        }

        public string GetLabel() => $"Requires an equipped {string.Join(" or ", WeaponTypes.Select(t => t.ToString()))} weapon";
    }
}
