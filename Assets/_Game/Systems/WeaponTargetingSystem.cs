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
        private ComponentLookup<LocalToWorld> _transformLookup;
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<EnemyIncomingDamage> _incomingDamageLookup;
        private ComponentLookup<Steering> _steeringLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponTargeting>();
            state.RequireForUpdate<SpatialGrids>();

            _transformLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: true);
            _incomingDamageLookup = state.GetComponentLookup<EnemyIncomingDamage>(isReadOnly: true);
            _steeringLookup = state.GetComponentLookup<Steering>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            if (!grids.EnemiesValid && !grids.AlliesValid) return;

            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _incomingDamageLookup.Update(ref state);
            _steeringLookup.Update(ref state);

            state.Dependency = new WeaponTargetingJob
            {
                TransformLookup = _transformLookup,
                HealthLookup = _healthLookup,
                IncomingDamageLookup = _incomingDamageLookup,
                SteeringLookup = _steeringLookup,
                Grids = grids,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct WeaponTargetingJob : IJobEntity
    {
        private const int RandomSampleAttempts = 8;

        [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<EnemyIncomingDamage> IncomingDamageLookup;
        [ReadOnly] public ComponentLookup<Steering> SteeringLookup;
        [ReadOnly] public SpatialGrids Grids;
        public float DeltaTime;

        private void Execute(Entity entity, ref WeaponTargeting targeting, in WeaponStats weaponStats, in LocalToWorld transform, in WeaponOwner owner, in AttackRules rules)
        {
            float2 weaponPos = transform.Position.xy;

            if (targeting.RandomSeed == 0)
                targeting.RandomSeed = (uint)entity.Index * 2654435761u + 1u;
            
            // The weapon picks its target from the units of every faction it can target, with no faction preferred
            FactionMask factions = rules.TargetFactions;
            if (factions == FactionMask.None)
            {
                targeting.CurrentTarget = Entity.Null;
                targeting.HasTarget = false;
                return;
            }

            if (targeting.StickyLock)
            {
                // Check if we still have a target
                if (targeting.CurrentTarget != Entity.Null)
                {
                    if (TransformLookup.TryGetComponent(targeting.CurrentTarget, out LocalToWorld targetTransform))
                    {
                        float2 targetPos = targetTransform.Position.xy;
                        
                        if (math.distancesq(weaponPos, targetPos) <= weaponStats.TargetRange * weaponStats.TargetRange)
                        {
                            // Drop sticky lock if enough damage is already inbound to kill the target
                            if (IsOverCommitted(targeting.CurrentTarget, HealthLookup, IncomingDamageLookup))
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

            bool foundTarget = TryFindTarget(targeting.Strategy, weaponPos, Grids, factions, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, true, owner.Value, ref targeting.RandomSeed, out GridUnit found);

            // Fallback: if all units are over-committed, fire anyway to avoid wasting fire rate
            if (!foundTarget)
            {
                foundTarget = TryFindTarget(targeting.Strategy, weaponPos, Grids, factions, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, false, owner.Value, ref targeting.RandomSeed, out found);
            }

            if (!foundTarget) return;

            float2 foundTargetPos = Grids.PositionOf(found);
            Entity foundTargetEntity = Grids.EntityOf(found);
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
            in SpatialGrids grids,
            FactionMask factions,
            float range,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            ref uint seed,
            out GridUnit found)
        {
            if (strategy == TargetStrategy.Random)
            {
                return FindRandom(origin, grids, factions, range, healthLookup, incomingDamageLookup, checkIncoming, exclude, ref seed, out found);
            }

            // Nearest and furthest
            bool furthest = strategy == TargetStrategy.Furthest;
            float bestSq = furthest ? 0f : range * range;
            bool foundAny = false;
            found = default;

            for (int faction = 0; faction < SpatialGrids.FactionCount; faction++)
            {
                if (!grids.Has(factions, faction)) continue;

                bool better = furthest
                    ? FindFurthest(origin, grids[faction], range, healthLookup, incomingDamageLookup, checkIncoming, exclude, ref bestSq, out int index)
                    : FindNearest(origin, grids[faction], range, healthLookup, incomingDamageLookup, checkIncoming, exclude, ref bestSq, out index);
                if (!better) continue;

                found = new GridUnit(faction, index);
                foundAny = true;
            }

            return foundAny;
        }

        /// <summary>
        /// Picks a uniformly random valid unit in range among all <paramref name="factions"/>: a few random draws first, then a full scan if none of them is valid.
        /// </summary>
        private static bool FindRandom(
            float2 origin,
            in SpatialGrids grids,
            FactionMask factions,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            Entity exclude,
            ref uint seed,
            out GridUnit found)
        {
            found = default;

            float maxRangeSq = maxRange * maxRange;
            Random rng = new(seed);

            // All grids share one layout.
            grids[0].GetCellBounds(origin - maxRange, origin + maxRange, out int2 minCell, out int2 maxCell);

            // Candidates per faction.
            FixedList64Bytes<int> candidateCounts = default;
            int candidateCount = 0;
            for (int faction = 0; faction < SpatialGrids.FactionCount; faction++)
            {
                int count = 0;
                if (grids.Has(factions, faction))
                {
                    SpatialGrid grid = grids[faction];
                    count = UseFlatScan(grid, minCell, maxCell) ? grid.Count : grid.CountInBox(minCell, maxCell);
                }

                candidateCounts.Add(count);
                candidateCount += count;
            }

            if (candidateCount == 0) return false;

            // Rejection sampling: draw random candidates until one is valid.
            for (int attempt = 0; attempt < RandomSampleAttempts; attempt++)
            {
                int n = rng.NextInt(0, candidateCount);
                int faction = 0;
                while (n >= candidateCounts[faction])
                {
                    n -= candidateCounts[faction];
                    faction++;
                }

                SpatialGrid grid = grids[faction];
                int i = UseFlatScan(grid, minCell, maxCell) ? n : grid.NthInBox(minCell, maxCell, n);
                if (math.distancesq(origin, grid.Positions[i]) > maxRangeSq) continue;
                if (grid.Entities[i] == exclude) continue;
                if (checkIncoming && IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup)) continue;

                found = new GridUnit(faction, i);
                seed = rng.state;
                return true;
            }

            // Few or no valid targets: scan all candidates and pick one uniformly (reservoir sampling)
            int validCount = 0;

            for (int faction = 0; faction < SpatialGrids.FactionCount; faction++)
            {
                if (candidateCounts[faction] == 0) continue;

                SpatialGrid grid = grids[faction];
                GridQuery candidates = UseFlatScan(grid, minCell, maxCell) ? grid.All() : grid.Query(minCell, maxCell);
                foreach (int i in candidates)
                {
                    if (math.distancesq(origin, grid.Positions[i]) > maxRangeSq) continue;
                    if (grid.Entities[i] == exclude) continue;
                    if (checkIncoming && IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup)) continue;

                    validCount++;
                    if (rng.NextInt(0, validCount) != 0) continue;
                    found = new GridUnit(faction, i);
                }
            }

            seed = rng.state;
            return validCount > 0;
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
            ref float bestSq,
            out int resultIndex)
        {
            resultIndex = -1;

            float maxRangeSq = maxRange * maxRange;

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
            ref float bestSq,
            out int resultIndex)
        {
            resultIndex = -1;

            float maxRangeSq = maxRange * maxRange;

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

                if (bestSq > 0f && MaxDistanceSqWithin(grid, origin, center, radius - 1) <= bestSq) break;
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
