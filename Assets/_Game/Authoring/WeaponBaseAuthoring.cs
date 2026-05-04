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

        private class Baker : Baker<WeaponBaseAuthoring>
        {
            public override void Bake(WeaponBaseAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new WeaponStats()
                {
                    BaseDamage = authoring.damage,
                    BaseFireRate = authoring.fireRate,
                    BaseTargetRange = authoring.targetRange,
                    Damage = authoring.damage,
                    FireRate = authoring.fireRate,
                    TargetRange = authoring.targetRange,
                });

                AddComponent(entity, new WeaponTypeRef
                {
                    WeaponType = authoring.weaponType
                });
                
                AddComponent(entity, new WeaponState
                {
                    Timer = 0f
                });

                AddComponent<WeaponTag>(entity);

                AddBuffer<StatModifier>(entity);

                AddComponent<WeaponStatUpdateRequired>(entity);

                AddComponent<ReadyToFire>(entity);
                SetComponentEnabled<ReadyToFire>(entity, false);

                Entity owner = authoring.transform.parent != null
                    ? GetEntity(authoring.transform.parent.gameObject, TransformUsageFlags.Dynamic)
                    : Entity.Null;
                AddComponent(entity, new WeaponOwner
                {
                    Value = owner
                });
            }
        }
    }
}