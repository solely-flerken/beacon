using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponTargetingSystem))]
    public partial struct ShootProjectileSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PlayerTag> _playerTagLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _playerTagLookup = state.GetComponentLookup<PlayerTag>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _playerTagLookup.Update(ref state);

            BeginSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new ShootProjectileJob
            {
                ECB = ecb,
                TransformLookup = _transformLookup,
                PlayerTagLookup = _playerTagLookup
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(ReadyToFire))]
    internal partial struct ShootProjectileJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public ComponentLookup<PlayerTag> PlayerTagLookup;

        private void Execute(
            Entity entity,
            [EntityIndexInQuery] int sortKey,
            in ShootProjectile projectileData,
            in WeaponStats stats,
            in ProjectileStats projStats,
            in WeaponTargeting targeting,
            in LocalToWorld transform,
            in WeaponOwner owner)
        {
            if (!targeting.HasTarget) return;

            float2 weaponPos = transform.Position.xy;
            float2 targetPos = PredictIntercept(weaponPos, targeting.TargetPosition, targeting.TargetVelocity, projStats.Speed);

            const float minDistSq = 1.7f * 1.7f; // TODO: "MinShootDistance"
            float2 delta = targetPos - weaponPos;

            if (math.lengthsq(delta) < minDistSq) return;

            float2 baseDir = math.normalize(delta);
            Entity prefab = projectileData.ProjectilePrefab;
            float3 spawnPos = new(weaponPos + baseDir * 1.2f, 0f); // TODO: "SpawnOffset" (Weapon Size + epsilon)

            float scale = TransformLookup.HasComponent(prefab) ? TransformLookup[prefab].Scale : 1f;

            // Spawn projectile
            Entity projectile = ECB.Instantiate(sortKey, prefab);
            ECB.SetComponent(sortKey, projectile, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, scale));
            ECB.SetComponent(sortKey, projectile, new ProjectileVelocity { Value = baseDir * projStats.Speed });
            ECB.SetComponent(sortKey, projectile, new ProjectileDamage { Value = stats.Damage });
            ECB.SetComponent(sortKey, projectile, new ProjectilePierce { Remaining = projStats.Pierce });

            // Who owns this weapon?
            if (PlayerTagLookup.HasComponent(owner.Value))
            {
                ECB.AddComponent<PlayerProjectileTag>(sortKey, projectile);
                ECB.AddComponent(sortKey, projectile, new ProjectileWeapon { Value = entity });
            }
            else
            {
                ECB.AddComponent<EnemyProjectileTag>(sortKey, projectile);
            }

            ECB.SetComponentEnabled<ReadyToFire>(sortKey, entity, false);
        }

        private static float2 PredictIntercept(float2 shooterPos, float2 targetPos, float2 targetVelocity, float projectileSpeed)
        {
            if (math.lengthsq(targetVelocity) < 0.01f) return targetPos;

            float2 toTarget = targetPos - shooterPos;
            float quadraticA = math.dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
            float quadraticB = 2f * math.dot(toTarget, targetVelocity);
            float quadraticC = math.dot(toTarget, toTarget);

            float interceptTime;
            if (math.abs(quadraticA) < 0.001f)
            {
                if (math.abs(quadraticB) < 0.001f) return targetPos;
                
                interceptTime = -quadraticC / quadraticB;
            }
            else
            {
                float discriminant = quadraticB * quadraticB - 4f * quadraticA * quadraticC;
                if (discriminant < 0f) return targetPos;
                
                float sqrtDiscriminant = math.sqrt(discriminant);
                float timeEarly = (-quadraticB - sqrtDiscriminant) / (2f * quadraticA);
                float timeLate  = (-quadraticB + sqrtDiscriminant) / (2f * quadraticA);
                interceptTime = timeEarly > 0f ? timeEarly : timeLate;
            }

            return interceptTime > 0f ? targetPos + targetVelocity * interceptTime : targetPos;
        }
    }
}