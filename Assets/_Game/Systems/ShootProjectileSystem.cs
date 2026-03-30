using _Game.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponTargetingSystem))]
    public partial struct ShootProjectileSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);

            // Query weapons that are ready to fire and shoot normal projectiles
            foreach ((RefRO<ShootProjectile> projectileData, RefRO<WeaponStats> stats, RefRO<ProjectileStats> projStats, RefRO<WeaponTargeting> targeting, RefRO<LocalToWorld> transform, Entity entity) in
                     SystemAPI
                         .Query<RefRO<ShootProjectile>, RefRO<WeaponStats>, RefRO<ProjectileStats>, RefRO<WeaponTargeting>, RefRO<LocalToWorld>>()
                         .WithAll<ReadyToFire>()
                         .WithEntityAccess())
            {
                if (!targeting.ValueRO.HasTarget) continue;

                float2 weaponPos = transform.ValueRO.Position.xy;
                float2 targetPos = targeting.ValueRO.TargetPosition;
                
                float minDist = math.max(1.2f + 0.5f, 0.0001f) ; // TODO: "MinShootDistance"
                
                float2 delta = targetPos - weaponPos;
                if (math.lengthsq(delta) < minDist) continue;
                float2 baseDir = math.normalize(delta);
                
                Entity prefabToSpawn = projectileData.ValueRO.ProjectilePrefab;

                float3 spawnPos = new(weaponPos + baseDir * 1.2f, 0f); // TODO: "SpawnOffset" (Weapon Size + epsilon)
                LocalTransform projTransform = SystemAPI.GetComponent<LocalTransform>(prefabToSpawn);

                Entity projectile = ecb.Instantiate(prefabToSpawn);
                ecb.SetComponent(projectile, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, projTransform.Scale));
                ecb.SetComponent(projectile, new ProjectileVelocity { Value = baseDir * projStats.ValueRO.Speed });
                ecb.SetComponent(projectile, new ProjectileDamage { Value = stats.ValueRO.Damage });
                ecb.SetComponent(projectile, new ProjectilePierce { Remaining = projStats.ValueRO.Pierce });

                SystemAPI.SetComponentEnabled<ReadyToFire>(entity, false);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}