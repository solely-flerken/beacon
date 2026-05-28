using System.Linq;
using _Game.Components;

namespace _Game.Gameplay
{
    public class BeaconLoadout
    {
        private WeaponType?[] _weaponSlots;

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

        public void Reset()
        {
            _weaponSlots = null;
        }
    }
}
