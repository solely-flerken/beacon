using _Game.Components;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialGridSystem))]
    public partial struct WeaponTargetingSystem : ISystem
    {
        private ComponentLookup<EnemyTag> _enemyTagLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponTargeting>();

            _enemyTagLookup = state.GetComponentLookup<EnemyTag>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SpatialGridShared.EnemyGridValid && !SpatialGridShared.AllyGridValid) return;

            _enemyTagLookup.Update(ref state);

            state.Dependency = new WeaponTargetingJob
            {
                EnemyTagLookup = _enemyTagLookup,
                EnemyPositions = EnemyDataShared.Positions,
                AllyPositions = AllyDataShared.Positions,
                EnemyCellMap = SpatialGridShared.EnemyCellMap,
                AllyCellMap = SpatialGridShared.AllyCellMap,
                CellSize = SpatialGridShared.CellSize,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct WeaponTargetingJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyTagLookup;
        
        [ReadOnly] public NativeArray<float2> EnemyPositions;
        [ReadOnly] public NativeArray<float2> AllyPositions;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> EnemyCellMap;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> AllyCellMap;
        public float CellSize;

        private void Execute(ref WeaponTargeting targeting, in WeaponStats weaponStats, in LocalToWorld transform, in WeaponOwner owner)
        {
            targeting.HasTarget = false;

            float2 weaponPos = transform.Position.xy;
            float range = weaponStats.TargetRange;

            bool isEnemyWeapon = EnemyTagLookup.HasComponent(owner.Value);
            NativeArray<float2> positions = isEnemyWeapon ? AllyPositions : EnemyPositions;
            NativeParallelMultiHashMap<int2, int> cellMap = isEnemyWeapon ? AllyCellMap : EnemyCellMap;

            if (!FindNearest(weaponPos, positions, cellMap, CellSize, range, out float2 targetPos)) return;

            targeting.HasTarget = true;
            targeting.TargetPosition = targetPos;
        }

        private static bool FindNearest(
            float2 origin,
            NativeArray<float2> positions,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float maxRange,
            out float2 nearest)
        {
            nearest = float2.zero;
            float shortestSq = maxRange * maxRange;
            bool found = false;

            int2 centerCell = new((int)math.floor(origin.x / cellSize), (int)math.floor(origin.y / cellSize));
            int cellRange = (int)math.ceil(maxRange / cellSize);

            for (int cx = -cellRange; cx <= cellRange; cx++)
            {
                for (int cy = -cellRange; cy <= cellRange; cy++)
                {
                    int2 cell = centerCell + new int2(cx, cy);
                    
                    // Is an entity present in the current cell?
                    if (!cellMap.TryGetFirstValue(cell, out int idx, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                    // Check all entities in the current cell
                    do
                    {
                        float distSq = math.distancesq(origin, positions[idx]);
                        if (distSq >= shortestSq) continue;
                        
                        shortestSq = distSq;
                        nearest = positions[idx];
                        found = true;
                    } while (cellMap.TryGetNextValue(out idx, ref it));
                }
            }

            return found;
        }
    }
}