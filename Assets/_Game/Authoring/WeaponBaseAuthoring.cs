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
        [Range(0, 100)] public int armorPenetration;
        [Range(0, 100)] public int critChance;
        [Min(100)] public int critDamage = 150;

        private class Baker : Baker<WeaponBaseAuthoring>
        {
            public override void Bake(WeaponBaseAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<WeaponTag>(entity);
                
                // Classification. The owner's tags are added when the stats are resolved.
                AddComponent(entity, new Classification
                {
                    Tags = Tags.Weapon | authoring.weaponType.ToTag()
                });
                
                AddComponent<AttackRules>(entity);

                // Basic weapon stats
                AddComponent(entity, new WeaponStats
                {
                    BaseDamage = authoring.damage,
                    BaseFireRate = authoring.fireRate,
                    BaseTargetRange = authoring.targetRange,
                    Damage = authoring.damage,
                    FireRate = authoring.fireRate,
                    TargetRange = authoring.targetRange,
                });

                AddComponent(entity, new AttackStats
                {
                    BaseArmorPenetration = authoring.armorPenetration,
                    ArmorPenetration = authoring.armorPenetration,
                    BaseCritChance = authoring.critChance,
                    CritChance = authoring.critChance,
                    BaseCritDamage = authoring.critDamage,
                    CritDamage = authoring.critDamage,
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
