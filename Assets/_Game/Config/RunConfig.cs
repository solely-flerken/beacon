using _Game.Components;
using UnityEngine;

namespace _Game.Config
{
    [CreateAssetMenu(menuName = "LastBeacon/RunConfig")]
    public class RunConfig : ScriptableObject
    {
        public int weaponSlotCount = 6;
        public WeaponType[] startingWeapons = { WeaponType.Basic };
    }
}
