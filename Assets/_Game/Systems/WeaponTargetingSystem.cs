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

        private void Execute(Entity entity, ref WeaponTargeting targeting, in WeaponStats weaponStats, in LocalToWorld transform, in WeaponOwner owner)
        {
            float2 weaponPos = transform.Position.xy;

            if (targeting.RandomSeed == 0)
                targeting.RandomSeed = (uint)entity.Index * 2654435761u + 1u;
            
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

            bool foundTarget;
            float2 foundTargetPos;
            int foundTargetIdx;

            switch (targeting.Strategy)
            {
                case TargetStrategy.Furthest:
                    foundTarget = FindNearest(weaponPos, positions, cellMap, CellSize, weaponStats.TargetRange, out foundTargetPos, out foundTargetIdx, invert: true);
                    break;
                case TargetStrategy.Random:
                    foundTarget = FindRandom(weaponPos, positions, cellMap, CellSize, weaponStats.TargetRange, ref targeting.RandomSeed, out foundTargetPos, out foundTargetIdx);
                    break;
                case TargetStrategy.Nearest:
                default:
                    foundTarget = FindNearest(weaponPos, positions, cellMap, CellSize, weaponStats.TargetRange, out foundTargetPos, out foundTargetIdx);
                    break;
            }

            if (!foundTarget) return;

            targeting.HasTarget = true;
            targeting.TargetPosition = foundTargetPos;
            targeting.CurrentTarget = entities[foundTargetIdx];
        }

        private static bool FindRandom(
            float2 origin,
            NativeArray<float2> positions,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float maxRange,
            ref uint seed,
            out float2 randomPos,
            out int randomIdx)
        {
            randomPos = float2.zero;
            randomIdx = -1;

            float maxRangeSq = maxRange * maxRange;
            int2 centerCell = new((int)math.floor(origin.x / cellSize), (int)math.floor(origin.y / cellSize));
            int cellRange = (int)math.ceil(maxRange / cellSize);

            Random rng = new(seed);
            int count = 0;

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
                        if (math.distancesq(origin, positions[idx]) > maxRangeSq) continue;

                        count++;
                        if (rng.NextInt(0, count) != 0) continue;
                        randomPos = positions[idx];
                        randomIdx = idx;
                    } while (cellMap.TryGetNextValue(out idx, ref it));
                }
            }

            seed = rng.state;
            return randomIdx != -1;
        }

        private static bool FindNearest(
            float2 origin,
            NativeArray<float2> positions,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float maxRange,
            out float2 resultPos,
            out int resultIdx,
            bool invert = false)
        {
            resultPos = float2.zero;
            resultIdx = -1;

            float maxRangeSq = maxRange * maxRange;
            float bestSq = invert ? 0f : maxRangeSq;
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
                        bool betterTarget = invert ? distSq <= maxRangeSq && distSq > bestSq : distSq < bestSq;
                        if (!betterTarget) continue;
                        
                        bestSq = distSq;
                        resultPos = positions[idx];
                        resultIdx = idx;
                        found = true;
                    } while (cellMap.TryGetNextValue(out idx, ref it));
                }
            }

            return found;
        }
    }
}