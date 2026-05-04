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
            float2 targetPos = targeting.TargetPosition;

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
            }
            else
            {
                ECB.AddComponent<EnemyProjectileTag>(sortKey, projectile);
            }

            ECB.SetComponentEnabled<ReadyToFire>(sortKey, entity, false);
        }
    }
}