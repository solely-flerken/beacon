using System;
using System.Linq;
using _Game.Components;
using _Game.Gameplay.Abilities;

namespace _Game.Gameplay
{
    public class BeaconLoadout
    {
        public const int AbilitySlotCount = 2;

        private WeaponType?[] _weaponSlots;
        private readonly AbilityType?[] _abilitySlots = new AbilityType?[AbilitySlotCount];

        public int SlotCount => _weaponSlots?.Length ?? 0;

        public void Initialize(int slotCount)
        {
            _weaponSlots = new WeaponType?[slotCount];
        }

        public void SetWeapon(int slot, WeaponType type)
        {
            _weaponSlots[slot] = type;
        }

        public WeaponType? GetWeapon(int slot)
        {
            return _weaponSlots[slot];
        }

        public bool HasWeaponOfType(WeaponType type)
        {
            return _weaponSlots != null && _weaponSlots.Any(slot => slot == type);
        }

        public void SetAbility(int slot, AbilityType type)
        {
            if (slot >= 0 && slot < AbilitySlotCount)
            {
                _abilitySlots[slot] = type;
            }
        }

        public AbilityType? GetAbility(int slot)
        {
            if (slot >= 0 && slot < AbilitySlotCount)
            {
                return _abilitySlots[slot];
            }

            return null;
        }

        public void Reset()
        {
            _weaponSlots = null;
            Array.Clear(_abilitySlots, 0, _abilitySlots.Length);
        }
    }
}
