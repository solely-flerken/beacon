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
        private ComponentLookup<LocalToWorld> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponTargeting>();

            _enemyTagLookup = state.GetComponentLookup<EnemyTag>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SpatialGridShared.EnemyGridValid && !SpatialGridShared.AllyGridValid) return;

            _enemyTagLookup.Update(ref state);
            _transformLookup.Update(ref state);

            state.Dependency = new WeaponTargetingJob
            {
                EnemyTagLookup = _enemyTagLookup,
                TransformLookup = _transformLookup,
                EnemyPositions = EnemyDataShared.Positions,
                EnemyEntities = EnemyDataShared.Entities,
                AllyPositions = AllyDataShared.Positions,
                AllyEntities = AllyDataShared.Entities,
                EnemyCellMap = SpatialGridShared.EnemyCellMap,
                AllyCellMap = SpatialGridShared.AllyCellMap,
                CellSize = SpatialGridShared.CellSize,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct WeaponTargetingJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyTagLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

        [ReadOnly] public NativeArray<float2> EnemyPositions;
        [ReadOnly] public NativeArray<Entity> EnemyEntities;
        [ReadOnly] public NativeArray<float2> AllyPositions;
        [ReadOnly] public NativeArray<Entity> AllyEntities;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> EnemyCellMap;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> AllyCellMap;
        public float CellSize;
        public float DeltaTime;

        private void Execute(ref WeaponTargeting targeting, in WeaponStats weaponStats, in LocalToWorld transform, in WeaponOwner owner)
        {
            float2 weaponPos = transform.Position.xy;
            
            bool isEnemyWeapon = EnemyTagLookup.HasComponent(owner.Value);
            NativeArray<float2> positions = isEnemyWeapon ? AllyPositions : EnemyPositions;
            NativeArray<Entity> entities = isEnemyWeapon ? AllyEntities : EnemyEntities;
            NativeParallelMultiHashMap<int2, int> cellMap = isEnemyWeapon ? AllyCellMap : EnemyCellMap;

            if (targeting.StickyLock)
            {
                // Check if we still have a target
                if (targeting.CurrentTarget != Entity.Null)
                {
                    if (TransformLookup.TryGetComponent(targeting.CurrentTarget, out LocalToWorld enemyTransform))
                    {
                        float2 targetPos = enemyTransform.Position.xy;
                        
                        if (math.distancesq(weaponPos, targetPos) <= weaponStats.TargetRange * weaponStats.TargetRange)
                        {
                            targeting.HasTarget = true;
                            targeting.TargetPosition = targetPos;
                            return;
                        }
                    }

                    targeting.CurrentTarget = Entity.Null;
                    targeting.SwapCooldownRemaining = targeting.SwapCooldown;
                    targeting.HasTarget = false;
                    return;
                }

                if (targeting.SwapCooldownRemaining > 0f)
                {
                    targeting.SwapCooldownRemaining -= DeltaTime;
                    targeting.HasTarget = false;
                    return;
                }
            }

            // Acquire target
            targeting.HasTarget = false;
            targeting.CurrentTarget = Entity.Null;
            
            if (!FindNearest(weaponPos, positions, cellMap, CellSize, weaponStats.TargetRange, out float2 targetPos2, out int bestIdx)) return;
            
            targeting.HasTarget = true;
            targeting.TargetPosition = targetPos2;
            targeting.CurrentTarget = entities[bestIdx];
        }

        private static bool FindNearest(
            float2 origin,
            NativeArray<float2> positions,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float maxRange,
            out float2 nearestPos,
            out int nearestIdx)
        {
            nearestPos = float2.zero;
            nearestIdx = -1;
            
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
                        nearestPos = positions[idx];
                        nearestIdx = idx;
                        found = true;
                    } while (cellMap.TryGetNextValue(out idx, ref it));
                }
            }

            return found;
        }
    }
}