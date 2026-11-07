using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    [RequireComponent(typeof(WeaponBaseAuthoring))]
    [RequireComponent(typeof(WeaponTargetingAuthoring))]
    public class WeaponMortarAuthoring : MonoBehaviour
    {
        public GameObject shellPrefab;
        public GameObject explosionPrefab;
        public GameObject shadowPrefab;

        [Header("Stats")] 
        public float explosionRadius = 5f;
        public float shellSpeed = 40f;
        public float arcHeightPerUnitDistance = 0.3f;

        private class Baker : Baker<WeaponMortarAuthoring>
        {
            public override void Bake(WeaponMortarAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new ProjectileStats
                {
                    BaseSpeed = authoring.shellSpeed,
                    BasePierce = 0,
                    Speed = authoring.shellSpeed,
                    Pierce = 0,
                });

                AddComponent(entity, new MortarStats
                {
                    ExplosionRadius = authoring.explosionRadius,
                });

                AddComponent(entity, new MortarLaunchData
                {
                    ShellPrefab = GetEntity(authoring.shellPrefab, TransformUsageFlags.Dynamic),
                    ExplosionPrefab = GetEntity(authoring.explosionPrefab, TransformUsageFlags.Dynamic),
                    ShadowPrefab = GetEntity(authoring.shadowPrefab, TransformUsageFlags.Dynamic),
                    ArcHeightPerUnit = authoring.arcHeightPerUnitDistance,
                });
            }
        }
    }
}
