using _Game.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.Authoring
{
    [RequireComponent(typeof(WeaponBaseAuthoring))]
    [RequireComponent(typeof(WeaponTargetingAuthoring))]
    public class WeaponScatterAuthoring : MonoBehaviour
    {
        public GameObject projectilePrefab;

        [Header("Projectile Stats")] 
        public float speed = 40f;
        public int pierce = 1;

        [Header("Scatter Stats")] 
        public int projectileCount = 5;
        public float spreadAngle = 15f;

        [Range(0f, 1f)] 
        [Tooltip("Fraction of base speed each pellet can randomly deviate. 0.3 = ±30%.")]
        public float speedVariation = 0.3f;

        private class Baker : Baker<WeaponScatterAuthoring>
        {
            public override void Bake(WeaponScatterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new ProjectileStats
                {
                    BaseSpeed = authoring.speed,
                    BasePierce = authoring.pierce,
                    Speed = authoring.speed,
                    Pierce = authoring.pierce,
                });

                AddComponent(entity, new ScatterStats
                {
                    ProjectileCount = authoring.projectileCount,
                    SpreadAngle = authoring.spreadAngle * math.PI / 180f,
                    SpeedVariation = authoring.speedVariation,
                });

                AddComponent(entity, new ShootScatter
                {
                    ProjectilePrefab = GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic),
                });
            }
        }
    }
}