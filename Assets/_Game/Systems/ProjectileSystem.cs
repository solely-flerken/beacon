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
        private EntityQuery _playerProjectiles;
        private EntityQuery _enemyProjectiles;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<DamageEvents>();

            _playerProjectiles = SystemAPI.QueryBuilder()
                .WithAll<PlayerProjectileTag, LocalTransform, ProjectileVelocity, ProjectileDamage, ProjectilePierce, ProjectileBounce, ProjectileTarget>()
                .Build();

            _enemyProjectiles = SystemAPI.QueryBuilder()
                .WithAll<EnemyProjectileTag, LocalTransform, ProjectileVelocity, ProjectileDamage, ProjectilePierce, ProjectileBounce, ProjectileTarget>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Move all Projectiles
            state.Dependency = new MoveProjectilesJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();

            // Check collisions
            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;
            NativeQueue<DamageEvent>.ParallelWriter damageQueueWriter = damageQueue.AsParallelWriter();
            BufferLookup<HitCooldown> hitCooldownLookup = SystemAPI.GetBufferLookup<HitCooldown>(true);

            ComponentLookup<ProjectileWeapon> weaponSourceLookup = SystemAPI.GetComponentLookup<ProjectileWeapon>(true);
            double elapsedTime = SystemAPI.Time.ElapsedTime;

            // PLAYER PROJECTILES vs ENEMIES
            if (grids.EnemiesValid && !_playerProjectiles.IsEmpty)
            {
                EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

                state.Dependency = new CollisionJob
                {
                    SpatialGrid = grids.Enemies,
                    HitRadius = 0.5f,
                    ECB = ecb,
                    ReportHits = true,
                    DamageQueue = damageQueueWriter,
                    HitCooldowns = hitCooldownLookup,
                    WeaponSourceLookup = weaponSourceLookup,
                    ElapsedTime = elapsedTime,
                }.ScheduleParallel(_playerProjectiles, state.Dependency);
            }

            // ENEMY PROJECTILES vs PLAYER
            if (grids.AlliesValid && !_enemyProjectiles.IsEmpty)
            {
                EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

                state.Dependency = new CollisionJob
                {
                    SpatialGrid = grids.Allies,
                    HitRadius = 0.5f,
                    ECB = ecb,
                    ReportHits = false, // TODO: enemy -> beacon hits don't count as player weapon stats. Need a dedicated stat for them
                    DamageQueue = damageQueueWriter,
                    HitCooldowns = hitCooldownLookup,
                    WeaponSourceLookup = weaponSourceLookup,
                    ElapsedTime = elapsedTime,
                }.ScheduleParallel(_enemyProjectiles, state.Dependency);
            }
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
        [ReadOnly] public SpatialGrid SpatialGrid;
        [ReadOnly] public float HitRadius;
        
        public EntityCommandBuffer.ParallelWriter ECB;
        
        /// <summary>
        /// Credit hits to the firing weapon.
        /// </summary>
        public bool ReportHits;

        [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;
        [ReadOnly] public ComponentLookup<ProjectileWeapon> WeaponSourceLookup;

        public double ElapsedTime;

        private void Execute(Entity projectile, [EntityIndexInQuery] int sortKey, in LocalTransform transform, ref ProjectileVelocity velocity, ref ProjectileDamage damage, ref ProjectilePierce pierce, ref ProjectileBounce bounce, ref ProjectileTarget reservation)
        {
            float2 pos = new(transform.Position.x, transform.Position.y);
            float hitRadiusSq = HitRadius * HitRadius;

            foreach (int i in SpatialGrid.QueryNeighbors(pos, 1))
            {
                if (math.distancesq(pos, SpatialGrid.Positions[i]) > hitRadiusSq) continue;

                Entity target = SpatialGrid.Entities[i];

                // Check if projectile can damage the current entity
                if (HitCooldowns.HasBuffer(target))
                {
                    bool onCooldown = false;
                    DynamicBuffer<HitCooldown> cds = HitCooldowns[target];

                    for (int c = 0; c < cds.Length; c++)
                    {
                        if (cds[c].Source != projectile) continue;

                        onCooldown = true;
                        break;
                    }

                    // Projectile can't hit the entity - continue with next
                    if (onCooldown) continue;

                    ECB.AppendToBuffer(sortKey, target, new HitCooldown { Source = projectile, Expiry = float.PositiveInfinity });
                }
                else
                {
#if UNITY_EDITOR
                    Debug.Log($"[ProjectileSystem] Entity {target.Index} is missing HitCooldown! Skipping hit.");
#endif
                }

                Entity weaponEntity = ReportHits && WeaponSourceLookup.HasComponent(projectile)
                    ? WeaponSourceLookup[projectile].Value
                    : Entity.Null;

                DamageQueue.Enqueue(new DamageEvent
                {
                    Target = target,
                    Value = damage.Value,
                    Weapon = weaponEntity,
                    Position = transform.Position,
                });

                // Handle pierce, then ricochet
                pierce.Remaining--;
                if (pierce.Remaining >= 0) return;

                if (bounce.Remaining > 0 && TryBounce(projectile, target, pos, ref velocity, ref damage, ref bounce, ref reservation)) return;

                ECB.DestroyEntity(sortKey, projectile);
                return;
            }
        }

        /// <summary>
        /// Redirects the projectile to the nearest enemy in bounce range it hasn't hit yet, and moves its overkill reservation there.
        /// False if there is none.
        /// </summary>
        private bool TryBounce(Entity projectile, Entity lastTarget, float2 pos, ref ProjectileVelocity velocity, ref ProjectileDamage damage, ref ProjectileBounce bounce, ref ProjectileTarget reservation)
        {
            RicochetFilter filter = new() { Projectile = projectile, LastTarget = lastTarget, HitCooldowns = HitCooldowns };
            if (!HopTargeting.FindNearest(pos, SpatialGrid, bounce.Range, ref filter, out int next)) return false;

            float2 toNext = SpatialGrid.Positions[next] - pos;
            float speed = math.length(velocity.Value);

            velocity.Value = math.normalizesafe(toNext) * speed;
            damage.Value = math.max(damage.Value * bounce.Falloff, bounce.MinDamage);
            bounce.Remaining--;

            reservation.Value = SpatialGrid.Entities[next];
            reservation.ReservationExpiry = ElapsedTime + math.length(toNext) / math.max(speed, 0.01f) * 1.5;

            return true;
        }
    }

    internal struct RicochetFilter : IHopFilter
    {
        public Entity Projectile;
        public Entity LastTarget;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;

        public bool IsExcluded(Entity entity)
        {
            if (entity == LastTarget) return true;
            if (!HitCooldowns.TryGetBuffer(entity, out DynamicBuffer<HitCooldown> cds)) return false;

            for (int c = 0; c < cds.Length; c++)
            {
                if (cds[c].Source == Projectile) return true;
            }

            return false;
        }
    }
}
