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
    public partial struct ShootScatterSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new ShootScatterJob
            {
                ECB = ecb,
                TransformLookup = _transformLookup,
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(ReadyToFire))]
    internal partial struct ShootScatterJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public float ElapsedTime;

        private void Execute(
            Entity entity,
            [EntityIndexInQuery] int sortKey,
            in ShootScatter scatterData,
            in ScatterStats scatterStats,
            in WeaponStats weaponStats,
            in ProjectileStats projStats,
            in WeaponTargeting targeting,
            in LocalToWorld transform,
            in Classification classification)
        {
            if (!targeting.HasTarget) return;

            float2 weaponPos = transform.Position.xy;
            float2 targetPos = targeting.TargetPosition;

            const float minDistSq = 1.7f * 1.7f; // TODO: "MinShootDistance"
            float2 delta = targetPos - weaponPos;

            if (math.lengthsq(delta) < minDistSq) return;
            
            float2 baseDir = math.normalize(delta);
            Entity prefab = scatterData.ProjectilePrefab;
            bool isPlayer = (classification.Faction & FactionMask.Player) != 0;

            float scale = TransformLookup.HasComponent(prefab) ? TransformLookup[prefab].Scale : 1f;

            uint seed = math.hash(new uint2((uint)entity.Index, math.asuint(ElapsedTime)));
            Random rng = new(math.max(1u, seed));
            float halfSpread = scatterStats.SpreadAngle * 0.5f;

            for (int i = 0; i < scatterStats.ProjectileCount; i++)
            {
                float angleOffset = rng.NextFloat(-halfSpread, halfSpread);
                float speedMul = 1f + rng.NextFloat(-scatterStats.SpeedVariation, scatterStats.SpeedVariation);

                math.sincos(angleOffset, out float sinA, out float cosA);
                float2 dir = new(baseDir.x * cosA - baseDir.y * sinA, baseDir.x * sinA + baseDir.y * cosA);
                float3 spawnPos = new(weaponPos + dir * 1.2f, 0f);

                // Spawn projectile
                LocalTransform spawnTransform = LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, scale);
                Entity projectile = ECB.Instantiate(sortKey, prefab);
                ECB.SetComponent(sortKey, projectile, spawnTransform);
                ECB.SetComponent(sortKey, projectile, new LocalToWorld { Value = spawnTransform.ToMatrix() });
                ECB.SetComponent(sortKey, projectile, new ProjectileVelocity { Value = dir * (projStats.Speed * speedMul) });
                ECB.SetComponent(sortKey, projectile, new ProjectileDamage { Value = weaponStats.Damage });
                ECB.SetComponent(sortKey, projectile, new ProjectilePierce { Remaining = projStats.Pierce });

                // Who owns this weapon?
                if (isPlayer)
                {
                    ECB.AddComponent<PlayerProjectileTag>(sortKey, projectile);
                    ECB.AddComponent(sortKey, projectile, new ProjectileWeapon { Value = entity });
                }
                else
                {
                    ECB.AddComponent<EnemyProjectileTag>(sortKey, projectile);
                }
            }

            ECB.SetComponentEnabled<ReadyToFire>(sortKey, entity, false);
        }
    }
}