using Unity.Entities;
using UnityEngine;

namespace _Game.Gameplay
{
    public class WeaponSlot : MonoBehaviour
    {
        public Entity WeaponEntity { get; private set; } = Entity.Null;

        public void SetWeapon(Entity entity) => WeaponEntity = entity;
        public void Clear() => WeaponEntity = Entity.Null;
    }
}