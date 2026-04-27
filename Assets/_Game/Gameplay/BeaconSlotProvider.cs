using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay
{
    public class BeaconSlotProvider : MonoBehaviour
    {
        private WeaponSlot[] _weaponSlots;

        public int WeaponSlotCount => _weaponSlots.Length;

        private void Awake()
        {
            _weaponSlots = GetComponentsInChildren<WeaponSlot>();
        }

        public bool TryGetSlot(int index, out Transform slot)
        {
            if (_weaponSlots != null && index >= 0 && index < _weaponSlots.Length && _weaponSlots[index])
            {
                slot = _weaponSlots[index].transform;
                return true;
            }

            slot = null;
            return false;
        }

        public void SetWeaponEntity(int index, Entity entity)
        {
            _weaponSlots[index].SetWeapon(entity);
        }

        public void ClearWeaponEntities()
        {
            foreach (WeaponSlot weaponSlot in _weaponSlots)
            {
                weaponSlot.Clear();
            }
        }
    }
}