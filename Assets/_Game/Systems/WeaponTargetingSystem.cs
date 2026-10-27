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
                EnemyGrid = SpatialGridShared.EnemyGrid,
                AllyGrid = SpatialGridShared.AllyGrid,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct WeaponTargetingJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyTagLookup;
        [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<EnemyIncomingDamage> IncomingDamageLookup;
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
            bool foundTarget = TryFindTarget(targeting.Strategy, weaponPos, grid, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, checkIncoming, ref targeting.RandomSeed, out int foundIndex);

            // Fallback: if all enemies are over-committed, fire anyway to avoid wasting fire rate
            if (!foundTarget && checkIncoming)
            {
                foundTarget = TryFindTarget(targeting.Strategy, weaponPos, grid, weaponStats.TargetRange, HealthLookup, IncomingDamageLookup, false, ref targeting.RandomSeed, out foundIndex);
            }

            if (!foundTarget) return;

            float2 foundTargetPos = grid.Positions[foundIndex];
            targeting.TargetVelocity = (foundTargetPos - prevTargetPos) / DeltaTime;
            targeting.HasTarget = true;
            targeting.TargetPosition = foundTargetPos;
            targeting.CurrentTarget = grid.Entities[foundIndex];
        }

        private static bool TryFindTarget(
            TargetStrategy strategy,
            float2 origin,
            SpatialGrid grid,
            float range,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            ref uint seed,
            out int foundIndex)
        {
            switch (strategy)
            {
                case TargetStrategy.Furthest:
                    return FindNearest(origin, grid, range, healthLookup, incomingDamageLookup, checkIncoming, out foundIndex, invert: true);
                case TargetStrategy.Random:
                    return FindRandom(origin, grid, range, healthLookup, incomingDamageLookup, checkIncoming, ref seed, out foundIndex);
                case TargetStrategy.Nearest:
                default:
                    return FindNearest(origin, grid, range, healthLookup, incomingDamageLookup, checkIncoming, out foundIndex);
            }
        }

        private static bool FindRandom(
            float2 origin,
            SpatialGrid grid,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            ref uint seed,
            out int randomIndex)
        {
            randomIndex = -1;

            float maxRangeSq = maxRange * maxRange;
            Random rng = new(seed);
            int count = 0;

            foreach (int i in Candidates(grid, origin, maxRange))
            {
                if (math.distancesq(origin, grid.Positions[i]) > maxRangeSq) continue;
                if (checkIncoming && IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup)) continue;

                count++;
                if (rng.NextInt(0, count) != 0) continue;
                randomIndex = i;
            }

            seed = rng.state;
            return randomIndex != -1;
        }

        private static bool FindNearest(
            float2 origin,
            SpatialGrid grid,
            float maxRange,
            ComponentLookup<Health> healthLookup,
            ComponentLookup<EnemyIncomingDamage> incomingDamageLookup,
            bool checkIncoming,
            out int resultIndex,
            bool invert = false)
        {
            resultIndex = -1;

            float maxRangeSq = maxRange * maxRange;
            float bestSq = invert ? 0f : maxRangeSq;

            foreach (int i in Candidates(grid, origin, maxRange))
            {
                float distSq = math.distancesq(origin, grid.Positions[i]);
                bool betterTarget = invert ? distSq <= maxRangeSq && distSq > bestSq : distSq < bestSq;
                if (!betterTarget) continue;
                if (checkIncoming && IsOverCommitted(grid.Entities[i], healthLookup, incomingDamageLookup)) continue;

                bestSq = distSq;
                resultIndex = i;
            }

            return resultIndex != -1;
        }

        /// <summary>
        /// All units that may be in range.
        /// </summary>
        private static GridQuery Candidates(SpatialGrid grid, float2 origin, float range)
        {
            grid.GetCellBounds(origin - range, origin + range, out int2 minCell, out int2 maxCell);
            return grid.Count <= maxCell.y - minCell.y + 1 ? grid.All() : grid.Query(minCell, maxCell);
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