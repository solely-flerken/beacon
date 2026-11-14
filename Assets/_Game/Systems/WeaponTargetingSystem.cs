using _Game.Components;
using _Game.Shared;
using _Game.Util;
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
        private ComponentLookup<Steering> _steeringLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponTargeting>();
            state.RequireForUpdate<SpatialGrids>();

            _enemyTagLookup = state.GetComponentLookup<EnemyTag>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
            _incomingDamageLookup = state.GetComponentLookup<EnemyIncomingDamage>(isReadOnly: true);
            _steeringLookup = state.GetComponentLookup<Steering>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            if (!grids.EnemiesValid && !grids.AlliesValid) return;

            _enemyTagLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _incomingDamageLookup.Update(ref state);
            _steeringLookup.Update(ref state);

            state.Dependency = new WeaponTargetingJob
            {
                EnemyTagLookup = _enemyTagLookup,
                TransformLookup = _transformLookup,
                HealthLookup = _healthLookup,
                IncomingDamageLookup = _incomingDamageLookup,
                SteeringLookup = _steeringLookup,
                EnemyGrid = grids.Enemies,
                AllyGrid = grids.Allies,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct WeaponTargetingJob : IJobEntity
    {
        private const int RandomSampleAttempts = 8;

        [ReadOnly] public ComponentLookup<EnemyTag> EnemyTagLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<EnemyIncomingDamage> IncomingDamageLookup;
        [ReadOnly] public ComponentLookup<Steering> SteeringLookup;
        [ReadOnly] public SpatialGrid EnemyGrid;
        [ReadOnly] public SpatialGrid AllyGrid;
        public float DeltaTime;

        private void Execute(Entity entity, ref WeaponTargeting targeting, in WeaponStats weaponStats, in LocalToWorld transform, in WeaponOwner owner)
        {
            float2 weaponPos = transform.Position.xy;

            if (targeting.RandomSeed == 0)
                targeting.RandomSeed = (uint)entity.Index * 2654435761u + 1u;
            
            bool isEnemyWeapon = EnemyTagLookup.HasComponent(owner.Value);
            SpatialGrid grid = isEnemyWeapon ? AllyGrid : EnemyGrid;

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
                            
                            targeting.TargetVelocity = EstimateVelocity(targeting.CurrentTarget, targetPos, targeting.CurrentTarget, targeting.TargetPosition);
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

            // Save previous target for velocity estimation
            Entity prevTarget = targeting.CurrentTarget;
            float2 prevTargetPos = targeting.TargetPosition;

            // Acquire target
            targeting.HasTarget = false;
            targeting.CurrentTarget = Entity.Null;

            bool checkIncoming = !isEnemyWeapon;
            bool foundTarget = TryFindTarget(targeting.Strategy, weaponPos, grid, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, checkIncoming, owner.Value, ref targeting.RandomSeed, out int foundIndex);

            // Fallback: if all enemies are over-committed, fire anyway to avoid wasting fire rate
            if (!foundTarget && checkIncoming)
            {
                foundTarget = TryFindTarget(targeting.Strategy, weaponPos, grid, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, false, owner.Value, ref targeting.RandomSeed, out foundIndex);
            }

            if (!foundTarget) return;

            float2 foundTargetPos = grid.Positions[foundIndex];
            Entity foundTargetEntity = grid.Entities[foundIndex];
            targeting.TargetVelocity = EstimateVelocity(foundTargetEntity, foundTargetPos, prevTarget, prevTargetPos);
            targeting.HasTarget = true;
            targeting.TargetPosition = foundTargetPos;
            targeting.CurrentTarget = foundTargetEntity;
        }

        /// <summary>
        /// Velocity for intercept prediction. Units with steering report it directly; otherwise derived from the last known position.
        /// </summary>
        private float2 EstimateVelocity(Entity target, float2 targetPos, Entity prevTarget, float2 prevTargetPos)
        {
            if (SteeringLookup.TryGetComponent(target, out Steering steering)) return steering.Velocity;
            if (target != prevTarget || DeltaTime <= 0f) return float2.zero;
            return (targetPos - prevTargetPos) / DeltaTime;
        }

        private static bool TryFindTarget(
            TargetStrategy strategy,
            float2 origin,
            SpatialGrid grid,
            float range,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            ref uint seed,
            out int foundIndex)
        {
            switch (strategy)
            {
                case TargetStrategy.Random:
                    return FindRandom(origin, grid, range, healthLookup, incomingDamageLookup, checkIncoming, exclude, ref seed, out foundIndex);
                case TargetStrategy.Furthest:
                    return FindFurthest(origin, grid, range, healthLookup, incomingDamageLookup, checkIncoming, exclude, out foundIndex);
                case TargetStrategy.Nearest:
                default:
                    return FindNearest(origin, grid, range, healthLookup, incomingDamageLookup, checkIncoming, exclude, out foundIndex);
            }
        }

        /// <summary>
        /// Picks a uniformly random valid unit in range: a few random draws first, then a full scan if none of them is valid.
        /// </summary>
        private static bool FindRandom(
            float2 origin,
            SpatialGrid grid,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            ref uint seed,
            out int randomIndex)
        {
            randomIndex = -1;

            float maxRangeSq = maxRange * maxRange;
            Random rng = new(seed);

            grid.GetCellBounds(origin - maxRange, origin + maxRange, out int2 minCell, out int2 maxCell);
            bool flat = UseFlatScan(grid, minCell, maxCell);
            int candidateCount = flat ? grid.Count : grid.CountInBox(minCell, maxCell);
            if (candidateCount == 0) return false;

            // Rejection sampling: draw random candidates until one is valid.
            for (int attempt = 0; attempt < RandomSampleAttempts; attempt++)
            {
                int n = rng.NextInt(0, candidateCount);
                int i = flat ? n : grid.NthInBox(minCell, maxCell, n);
                if (math.distancesq(origin, grid.Positions[i]) > maxRangeSq) continue;
                if (grid.Entities[i] == exclude) continue;
                if (checkIncoming && IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup)) continue;

                randomIndex = i;
                seed = rng.state;
                return true;
            }

            // Few or no valid targets: scan all candidates and pick one uniformly (reservoir sampling)
            int count = 0;
            
            GridQuery candidates = flat ? grid.All() : grid.Query(minCell, maxCell);
            foreach (int i in candidates)
            {
                if (math.distancesq(origin, grid.Positions[i]) > maxRangeSq) continue;
                if (grid.Entities[i] == exclude) continue;
                if (checkIncoming && IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup)) continue;

                count++;
                if (rng.NextInt(0, count) != 0) continue;
                randomIndex = i;
            }

            seed = rng.state;
            return randomIndex != -1;
        }

        /// <summary>
        /// Searches ring by ring outward from the weapon's cell and stops once no unvisited unit can be nearer than the best so far.
        /// </summary>
        private static bool FindNearest(
            float2 origin,
            SpatialGrid grid,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            out int resultIndex)
        {
            resultIndex = -1;

            float maxRangeSq = maxRange * maxRange;
            float bestSq = maxRangeSq;

            grid.GetCellBounds(origin - maxRange, origin + maxRange, out int2 minCell, out int2 maxCell);
            if (UseFlatScan(grid, minCell, maxCell))
            {
                foreach (int i in grid.All())
                {
                    if (!IsBetterTarget(i, origin, grid, maxRangeSq, false, healthLookup, incomingDamageLookup, checkIncoming, exclude, bestSq, out float distSq)) continue;

                    bestSq = distSq;
                    resultIndex = i;
                }

                return resultIndex != -1;
            }

            int2 center = grid.WorldToCell(origin);
            int maxRadius = math.cmax(math.max(center - minCell, maxCell - center));
            float edgeDist = grid.DistanceToCellEdge(origin);

            for (int radius = 0; radius <= maxRadius; radius++)
            {
                float minDist = radius == 0 ? 0f : (radius - 1) * grid.CellSize + edgeDist;
                if (minDist * minDist >= bestSq) break;

                foreach (int i in grid.QueryRing(center, radius))
                {
                    if (!IsBetterTarget(i, origin, grid, maxRangeSq, false, healthLookup, incomingDamageLookup, checkIncoming, exclude, bestSq, out float distSq)) continue;

                    bestSq = distSq;
                    resultIndex = i;
                }
            }

            return resultIndex != -1;
        }

        /// <summary>
        /// Searches ring by ring inward from the edge of the range and stops once no unvisited unit can be further than the best so far.
        /// </summary>
        private static bool FindFurthest(
            float2 origin,
            SpatialGrid grid,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            out int resultIndex)
        {
            resultIndex = -1;

            float maxRangeSq = maxRange * maxRange;
            float bestSq = 0f;

            grid.GetCellBounds(origin - maxRange, origin + maxRange, out int2 minCell, out int2 maxCell);
            if (UseFlatScan(grid, minCell, maxCell))
            {
                foreach (int i in grid.All())
                {
                    if (!IsBetterTarget(i, origin, grid, maxRangeSq, true, healthLookup, incomingDamageLookup, checkIncoming, exclude, bestSq, out float distSq)) continue;

                    bestSq = distSq;
                    resultIndex = i;
                }

                return resultIndex != -1;
            }

            int2 center = grid.WorldToCell(origin);
            int maxRadius = math.cmax(math.max(center - minCell, maxCell - center));

            for (int radius = maxRadius; radius >= 0; radius--)
            {
                foreach (int i in grid.QueryRing(center, radius))
                {
                    if (!IsBetterTarget(i, origin, grid, maxRangeSq, true, healthLookup, incomingDamageLookup, checkIncoming, exclude, bestSq, out float distSq)) continue;

                    bestSq = distSq;
                    resultIndex = i;
                }

                if (resultIndex != -1 && MaxDistanceSqWithin(grid, origin, center, radius - 1) <= bestSq) break;
            }

            return resultIndex != -1;
        }

        /// <summary>
        /// Squared distance from <paramref name="origin"/> to the farthest corner of the cells within <paramref name="radius"/> of <paramref name="centerCell"/>, so no unit inside can be further.
        /// Infinite if the box touches an edge cell, since those also hold units from outside the grid, which can be any distance away.
        /// </summary>
        private static float MaxDistanceSqWithin(SpatialGrid grid, float2 origin, int2 centerCell, int radius)
        {
            if (radius < 0) return 0f;

            int2 min = centerCell - radius;
            int2 max = centerCell + radius;
            if (math.any(min <= 0) || max.x >= grid.Width - 1 || max.y >= grid.Height - 1)
            {
                return float.PositiveInfinity;
            }

            float2 boxMin = grid.Origin + (float2)min * grid.CellSize;
            float2 boxMax = grid.Origin + (float2)(max + 1) * grid.CellSize;
            return math.lengthsq(math.max(math.abs(origin - boxMin), math.abs(boxMax - origin)));
        }

        /// <summary>
        /// True if unit <paramref name="i"/> is nearer than <paramref name="bestSq"/> (further, if <paramref name="invert"/>), in range, not <paramref name="exclude"/> (the weapon's owner), and not over-committed.
        /// </summary>
        private static bool IsBetterTarget(
            int i,
            float2 origin,
            SpatialGrid grid,
            float maxRangeSq,
            bool invert,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            float bestSq,
            out float distSq)
        {
            distSq = math.distancesq(origin, grid.Positions[i]);
            bool betterTarget = invert ? distSq <= maxRangeSq && distSq > bestSq : distSq < bestSq;
            if (!betterTarget) return false;
            if (grid.Entities[i] == exclude) return false;
            return !checkIncoming || !IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup);
        }

        /// <summary>
        /// True if there are so few units that scanning all of them is cheaper than walking the cells.
        /// </summary>
        private static bool UseFlatScan(SpatialGrid grid, int2 minCell, int2 maxCell)
        {
            return grid.Count <= maxCell.y - minCell.y + 1;
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
