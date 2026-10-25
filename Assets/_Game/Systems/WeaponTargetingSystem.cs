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
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<EnemyIncomingDamage> _incomingDamageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponTargeting>();

            _enemyTagLookup = state.GetComponentLookup<EnemyTag>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
            _incomingDamageLookup = state.GetComponentLookup<EnemyIncomingDamage>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SpatialGridShared.EnemyGridValid && !SpatialGridShared.AllyGridValid) return;

            _enemyTagLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _incomingDamageLookup.Update(ref state);

            state.Dependency = new WeaponTargetingJob
            {
                EnemyTagLookup = _enemyTagLookup,
                TransformLookup = _transformLookup,
                HealthLookup = _healthLookup,
                IncomingDamageLookup = _incomingDamageLookup,
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
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<EnemyIncomingDamage> IncomingDamageLookup;
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
                            // Drop sticky lock if enough damage is already inbound to kill the target
                            if (!isEnemyWeapon && IsOverCommitted(targeting.CurrentTarget, HealthLookup, IncomingDamageLookup))
                            {
                                targeting.CurrentTarget = Entity.Null;
                                targeting.HasTarget = false;
                                return;
                            }
                            
                            targeting.TargetVelocity = (targetPos - targeting.TargetPosition) / DeltaTime;
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

            // Save previous target pos for velocity estimation
            float2 prevTargetPos = targeting.TargetPosition;

            // Acquire target
            targeting.HasTarget = false;
            targeting.CurrentTarget = Entity.Null;

            bool checkIncoming = !isEnemyWeapon;
            bool foundTarget = TryFindTarget(targeting.Strategy, weaponPos, positions, entities, cellMap, CellSize, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, checkIncoming, ref targeting.RandomSeed, out float2 foundTargetPos, out int foundTargetIdx);

            // Fallback: if all enemies are over-committed, fire anyway to avoid wasting fire rate
            if (!foundTarget && checkIncoming)
            {
                foundTarget = TryFindTarget(targeting.Strategy, weaponPos, positions, entities, cellMap, CellSize, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, false, ref targeting.RandomSeed, out foundTargetPos, out foundTargetIdx);
            }

            if (!foundTarget) return;

            targeting.TargetVelocity = (foundTargetPos - prevTargetPos) / DeltaTime;
            targeting.HasTarget = true;
            targeting.TargetPosition = foundTargetPos;
            targeting.CurrentTarget = entities[foundTargetIdx];
        }

        private static bool TryFindTarget(
            TargetStrategy strategy,
            float2 origin,
            NativeArray<float2> positions,
            NativeArray<Entity> entities,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float range,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            ref uint seed,
            out float2 foundPos,
            out int foundIdx)
        {
            switch (strategy)
            {
                case TargetStrategy.Furthest:
                    return FindNearest(origin, positions, entities, cellMap, cellSize, range, healthLookup, incomingDamageLookup, checkIncoming, out foundPos, out foundIdx, invert: true);
                case TargetStrategy.Random:
                    return FindRandom(origin, positions, entities, cellMap, cellSize, range, healthLookup, incomingDamageLookup, checkIncoming, ref seed, out foundPos, out foundIdx);
                case TargetStrategy.Nearest:
                default:
                    return FindNearest(origin, positions, entities, cellMap, cellSize, range, healthLookup, incomingDamageLookup, checkIncoming, out foundPos, out foundIdx);
            }
        }

        private static bool FindRandom(
            float2 origin,
            NativeArray<float2> positions,
            NativeArray<Entity> entities,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
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

            if (ShouldScanLinearly(positions.Length, cellRange))
            {
                for (int idx = 0; idx < positions.Length; idx++)
                {
                    if (math.distancesq(origin, positions[idx]) > maxRangeSq) continue;
                    if (checkIncoming && IsOverCommitted(entities[idx], healthLookup, incomingDamageLookup)) continue;

                    count++;
                    if (rng.NextInt(0, count) != 0) continue;
                    randomPos = positions[idx];
                    randomIdx = idx;
                }

                seed = rng.state;
                return randomIdx != -1;
            }

            for (int cx = -cellRange; cx <= cellRange; cx++)
            {
                for (int cy = -cellRange; cy <= cellRange; cy++)
                {
                    int2 cell = centerCell + new int2(cx, cy);

                    if (!cellMap.TryGetFirstValue(cell, out int idx, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                    do
                    {
                        if (math.distancesq(origin, positions[idx]) > maxRangeSq) continue;
                        if (checkIncoming && IsOverCommitted(entities[idx], healthLookup, incomingDamageLookup)) continue;

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
            NativeArray<Entity> entities,
            NativeParallelMultiHashMap<int2, int> cellMap,
            float cellSize,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
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

            if (ShouldScanLinearly(positions.Length, cellRange))
            {
                for (int idx = 0; idx < positions.Length; idx++)
                {
                    float distSq = math.distancesq(origin, positions[idx]);
                    bool betterTarget = invert ? distSq <= maxRangeSq && distSq > bestSq : distSq < bestSq;
                    if (!betterTarget) continue;
                    if (checkIncoming && IsOverCommitted(entities[idx], healthLookup, incomingDamageLookup)) continue;

                    bestSq = distSq;
                    resultPos = positions[idx];
                    resultIdx = idx;
                    found = true;
                }

                return found;
            }

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
                        if (checkIncoming && IsOverCommitted(entities[idx], healthLookup, incomingDamageLookup)) continue;

                        bestSq = distSq;
                        resultPos = positions[idx];
                        resultIdx = idx;
                        found = true;
                    } while (cellMap.TryGetNextValue(out idx, ref it));
                }
            }

            return found;
        }

        /// <summary>
        /// Returns true if iterating all candidates is cheaper than probing the (2 * cellRange + 1)^2 grid cells in range.
        /// </summary>
        private static bool ShouldScanLinearly(int candidateCount, int cellRange)
        {
            int side = 2 * cellRange + 1;
            return candidateCount <= side * side;
        }

        /// <summary>
        /// Returns true if the total damage already reserved by in-flight projectiles is enough to kill this enemy,
        /// meaning firing another shot at it would be wasted. Weapons that return true here should pick a different target.
        /// </summary>
        private static bool IsOverCommitted(Entity entity, ComponentLookup<Health> healthLookup, ComponentLookup<EnemyIncomingDamage> incomingDamageLookup)
        {
            return incomingDamageLookup.TryGetComponent(entity, out EnemyIncomingDamage incoming)
                   && incoming.Value > 0f
                   && healthLookup.TryGetComponent(entity, out Health health)
                   && incoming.Value >= health.Current;
        }
    }
}