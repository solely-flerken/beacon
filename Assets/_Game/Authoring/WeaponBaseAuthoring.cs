using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    public class WeaponBaseAuthoring : MonoBehaviour
    {
        public WeaponType weaponType = WeaponType.Basic;
        
        [Header("Base Stats")] 
        public float damage = 10f;
        public float fireRate = 10f;
        public float targetRange = 40f;
        [Range(0f, 1f)] public float armorPenetration;

        private class Baker : Baker<WeaponBaseAuthoring>
        {
            public override void Bake(WeaponBaseAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<WeaponTag>(entity);
                
                // Classification
                AddComponent(entity, new Classification
                {
                    Target = TargetMask.Weapon
                    // Other values are set at spawn time
                });
                
                // Basic weapon stats
                AddComponent(entity, new WeaponStats
                {
                    BaseDamage = authoring.damage,
                    BaseFireRate = authoring.fireRate,
                    BaseTargetRange = authoring.targetRange,
                    BaseArmorPenetration = authoring.armorPenetration,
                    Damage = authoring.damage,
                    FireRate = authoring.fireRate,
                    TargetRange = authoring.targetRange,
                    ArmorPenetration = authoring.armorPenetration,
                });
                
                // Reference to the weapon type 
                // TODO: Replace with classification and completely remove WeaponTypeRef
                AddComponent(entity, new WeaponTypeRef
                {
                    WeaponType = authoring.weaponType
                });
                
                // Weapon owner
                Entity owner = authoring.transform.parent != null ? GetEntity(authoring.transform.parent.gameObject, TransformUsageFlags.Dynamic) : Entity.Null;
                AddComponent(entity, new WeaponOwner
                {
                    Value = owner
                });
                
                // Weapon state
                AddComponent(entity, new WeaponState
                {
                    Timer = 0f
                });
                AddComponent<ReadyToFire>(entity);
                SetComponentEnabled<ReadyToFire>(entity, false);
                
                // Modifier
                AddBuffer<StatModifier>(entity);
                AddComponent<WeaponStatUpdateRequired>(entity);
            }
        }
    }
}
