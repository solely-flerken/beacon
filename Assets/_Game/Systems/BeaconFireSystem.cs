using _Game.Authoring;
using _Game.Components;
using _Game.Shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BeaconFireSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!EnemyDataShared.IsValid || EnemyDataShared.Count == 0) return;
            
            if (!SystemAPI.TryGetSingleton(out PrefabRefs prefabs)) return;
            if (!SystemAPI.HasComponent<ProjectileSpeed>(prefabs.Projectile)) return;

            NativeArray<float2> enemyPositions = EnemyDataShared.Positions;
            int enemyCount = EnemyDataShared.Count;
            
            float bakedSpeed = SystemAPI.GetComponent<ProjectileSpeed>(prefabs.Projectile).Value;
            LocalTransform projTransform = SystemAPI.GetComponent<LocalTransform>(prefabs.Projectile);
            
            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);

            foreach ((RefRW<BeaconWeapon> weapon, RefRO<BeaconStats> stats, RefRO<LocalTransform> transform) in SystemAPI.Query<RefRW<BeaconWeapon>, RefRO<BeaconStats>, RefRO<LocalTransform>>())
            {
                if (!stats.ValueRO.IsAlive) continue;

                weapon.ValueRW.Timer += SystemAPI.Time.DeltaTime;
                if (weapon.ValueRO.Timer < 1f / weapon.ValueRO.FireRate) continue;

                float2 beaconPos = transform.ValueRO.Position.xy;

                if (!FindNearestEnemy(beaconPos, enemyPositions, enemyCount, weapon.ValueRO.TargetRange, out float2 targetPos)) continue;
                
                // TODO: Own authoring field 'MinShootDistance' or remove completely
                float minDist = stats.ValueRO.ArrivalRadius + 0.5f;
                if (math.distancesq(beaconPos, targetPos) < minDist * minDist) continue;

                weapon.ValueRW.Timer = 0f;

                float2 dir = math.normalize(targetPos - beaconPos);
                float3 spawnPos = new(beaconPos + dir * stats.ValueRO.ArrivalRadius, 0f);

                Entity projectile = ecb.Instantiate(prefabs.Projectile);
                ecb.SetComponent(projectile, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, projTransform.Scale));
                ecb.SetComponent(projectile, new ProjectileVelocity { Value = dir * bakedSpeed });
            }

            ecb.Playback(state.EntityManager);
        }

        /// <summary>
        /// Finds the nearest enemy within maxRange.
        /// </summary>
        private static bool FindNearestEnemy(float2 origin, NativeArray<float2> positions, int count, float maxRange, out float2 nearest)
        {
            nearest = float2.zero;
            
            float shortestSq = maxRange * maxRange;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                float distanceSq = math.distancesq(origin, positions[i]);
                if (distanceSq >= shortestSq) continue;
                
                shortestSq = distanceSq;
                nearest = positions[i];
                found = true;
            }

            return found;
        }
    }
}