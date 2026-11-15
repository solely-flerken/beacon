using _Game.Components;
using _Game.Shared;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialGridSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<DamageEvents>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Move all Projectiles
            state.Dependency = new MoveProjectilesJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);

            // Check collisions
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            if (!grids.EnemiesValid && !grids.AlliesValid) return;

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;

            state.Dependency = new CollisionJob
            {
                Grids = grids,
                HitRadius = 0.5f,
                ECB = ecb.AsParallelWriter(),
                DamageQueue = damageQueue.AsParallelWriter(),
                HitCooldowns = SystemAPI.GetBufferLookup<HitCooldown>(true),
                ElapsedTime = SystemAPI.Time.ElapsedTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct MoveProjectilesJob : IJobEntity
    {
        [ReadOnly] public float DeltaTime;

        private void Execute(ref LocalTransform transform, in ProjectileVelocity velocity)
        {
            float2 pos = new float2(transform.Position.x, transform.Position.y) + velocity.Value * DeltaTime;
            transform.Position = new float3(pos.x, pos.y, 0f);
        }
    }

    [BurstCompile]
    internal partial struct CollisionJob : IJobEntity
    {
        [ReadOnly] public SpatialGrids Grids;
        [ReadOnly] public float HitRadius;
        
        public EntityCommandBuffer.ParallelWriter ECB;

        [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;

        public double ElapsedTime;

        private void Execute(Entity projectile, [EntityIndexInQuery] int sortKey, in LocalTransform transform, ref ProjectileVelocity velocity, ref ProjectileDamage damage, ref ProjectilePierce pierce, ref ProjectileBounce bounce, ref ProjectileTarget reservation, in ProjectileSource source)
        {
            float2 pos = new(transform.Position.x, transform.Position.y);
            float bestSq = HitRadius * HitRadius;
            Entity target = Entity.Null;

            for (int faction = 0; faction < SpatialGrids.FactionCount; faction++)
            {
                if (!Grids.Has(source.Rules.TargetFactions, faction)) continue;

                SpatialGrid grid = Grids[faction];
                foreach (int i in grid.QueryNeighbors(pos, 1))
                {
                    float distSq = math.distancesq(pos, grid.Positions[i]);
                    if (distSq > bestSq) continue;

                    Entity candidate = grid.Entities[i];
                    if (candidate == source.Owner || HasHit(projectile, candidate)) continue;

                    bestSq = distSq;
                    target = candidate;
                }
            }

            if (target == Entity.Null) return;

            // Remember the hit, so the projectile can't damage this unit again
            if (HitCooldowns.HasBuffer(target))
            {
                ECB.AppendToBuffer(sortKey, target, new HitCooldown { Source = projectile, Expiry = float.PositiveInfinity });
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"[ProjectileSystem] Entity {target.Index} is missing HitCooldown! It can be hit again.");
#endif
            }

            DamageQueue.Enqueue(new DamageEvent
            {
                Target = target,
                Value = damage.Value,
                Source = source.Weapon,
                ReportHit = source.Rules.ReportHits,
                Position = transform.Position,
            });

            // Handle pierce, then ricochet
            pierce.Remaining--;
            if (pierce.Remaining >= 0) return;

            if (bounce.Remaining > 0 && TryBounce(projectile, target, pos, ref velocity, ref damage, ref bounce, ref reservation, source)) return;

            ECB.DestroyEntity(sortKey, projectile);
        }

        /// <summary>
        /// True if the projectile already hit <paramref name="target"/>.
        /// </summary>
        private bool HasHit(Entity projectile, Entity target)
        {
            if (!HitCooldowns.TryGetBuffer(target, out DynamicBuffer<HitCooldown> cds)) return false;

            for (int c = 0; c < cds.Length; c++)
            {
                if (cds[c].Source == projectile) return true;
            }

            return false;
        }

        /// <summary>
        /// Redirects the projectile to the nearest unit in bounce range it hasn't hit yet, and moves its overkill reservation there.
        /// False if there is none.
        /// </summary>
        private bool TryBounce(Entity projectile, Entity lastTarget, float2 pos, ref ProjectileVelocity velocity, ref ProjectileDamage damage, ref ProjectileBounce bounce, ref ProjectileTarget reservation, in ProjectileSource source)
        {
            RicochetFilter filter = new() { Projectile = projectile, LastTarget = lastTarget, Owner = source.Owner, HitCooldowns = HitCooldowns };
            if (!HopTargeting.FindNearest(pos, Grids, source.Rules.TargetFactions, bounce.Range, ref filter, out GridUnit next)) return false;

            float2 toNext = Grids.PositionOf(next) - pos;
            float speed = math.length(velocity.Value);

            velocity.Value = math.normalizesafe(toNext) * speed;
            damage.Value = math.max(damage.Value * bounce.Falloff, bounce.MinDamage);
            bounce.Remaining--;

            reservation.Value = Grids.EntityOf(next);
            reservation.ReservationExpiry = ElapsedTime + math.length(toNext) / math.max(speed, 0.01f) * 1.5;

            return true;
        }
    }

    internal struct RicochetFilter : IHopFilter
    {
        public Entity Projectile;
        public Entity LastTarget;
        public Entity Owner;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;

        public bool IsExcluded(GridUnit unit, Entity entity)
        {
            if (entity == LastTarget || entity == Owner) return true;
            if (!HitCooldowns.TryGetBuffer(entity, out DynamicBuffer<HitCooldown> cds)) return false;

            for (int c = 0; c < cds.Length; c++)
            {
                if (cds[c].Source == Projectile) return true;
            }

            return false;
        }
    }
}
