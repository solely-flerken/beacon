using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WeaponTargetingSystem : ISystem
    {
        private EntityQuery _enemyQuery;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponTargeting>();
            
            _enemyQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<EnemyTag, LocalToWorld>().Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NativeArray<LocalToWorld> enemyTransforms = _enemyQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            foreach ((RefRW<WeaponTargeting> targeting, RefRO<WeaponStats> weaponStats, RefRO<LocalToWorld> transform)
                     in SystemAPI.Query<RefRW<WeaponTargeting>, RefRO<WeaponStats>, RefRO<LocalToWorld>>())
            {
                targeting.ValueRW.HasTarget = false;

                float2 weaponPos = transform.ValueRO.Position.xy;
                float range = weaponStats.ValueRO.TargetRange;

                if (!FindNearestEnemy(weaponPos, enemyTransforms, range, out float2 targetPos)) continue;

                targeting.ValueRW.HasTarget = true;
                targeting.ValueRW.TargetPosition = targetPos;
            }
        }
        
        /// <summary>
        /// Finds the nearest enemy within maxRange.
        /// </summary>
        private static bool FindNearestEnemy(float2 origin, NativeArray<LocalToWorld> enemyTransforms, float maxRange, out float2 nearest)
        {
            nearest = float2.zero;
            
            float shortestSq = maxRange * maxRange;
            bool found = false;

            foreach (LocalToWorld transform in enemyTransforms)
            {
                float2 enemyPos = transform.Position.xy;
                float distanceSq = math.distancesq(origin, enemyPos);
                
                if (distanceSq >= shortestSq) continue;
                
                shortestSq = distanceSq;
                nearest = enemyPos;
                found = true;
            }

            return found;
        }
    }
}