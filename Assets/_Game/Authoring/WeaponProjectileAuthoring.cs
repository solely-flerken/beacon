using _Game.Components;
using Unity.Entities;
using UnityEngine;

namespace _Game.Authoring
{
    [RequireComponent(typeof(WeaponBaseAuthoring))]
    public class WeaponProjectileAuthoring : MonoBehaviour
    {
        public GameObject projectilePrefab;

        [Header("Projectile Stats")]
        public float speed = 20f;
        public int pierce = 0;

        [Header("Ricochet")]
        public int bounceCount = 0;
        public float bounceRange = 8f;
        [Tooltip("Damage multiplier per bounce.")]
        public float bounceFalloff = 0.85f;
        [Tooltip("Bounce damage never drops below this fraction of the shot's damage.")]
        public float bounceDamageFloor = 0.5f;

        private class Baker : Baker<WeaponProjectileAuthoring>
        {
            public override void Bake(WeaponProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new ProjectileStats
                {
                    BaseSpeed = authoring.speed,
                    BasePierce = authoring.pierce,
                    BaseBounceCount = authoring.bounceCount,
                    BaseBounceRange = authoring.bounceRange,
                    BaseBounceFalloff = authoring.bounceFalloff,
                    BaseBounceDamageFloor = authoring.bounceDamageFloor,
                    Speed = authoring.speed,
                    Pierce = authoring.pierce,
                    BounceCount = authoring.bounceCount,
                    BounceRange = authoring.bounceRange,
                    BounceFalloff = authoring.bounceFalloff,
                    BounceDamageFloor = authoring.bounceDamageFloor,
                });

                AddComponent(entity, new ShootProjectile
                {
                    ProjectilePrefab = GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic),
                });
            }
        }
    }
}
