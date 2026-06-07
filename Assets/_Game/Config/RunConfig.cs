using _Game.Components;
using _Game.Gameplay.Abilities;
using UnityEngine;

namespace _Game.Config
{
    [CreateAssetMenu(menuName = "LastBeacon/RunConfig")]
    public class RunConfig : ScriptableObject
    {
        public WeaponType[] startingWeapons = { WeaponType.Basic };
        public AbilityType[] startingAbilities = { AbilityType.Flare, AbilityType.Stun };
    }
}
