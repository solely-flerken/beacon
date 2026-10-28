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
                .WithAll<PlayerProjectileTag, LocalTransform, ProjectileVelocity, ProjectileDamage, ProjectilePierce>()
                .Build();

            _enemyProjectiles = SystemAPI.QueryBuilder()
                .WithAll<EnemyProjectileTag, LocalTransform, ProjectileVelocity, ProjectileDamage, ProjectilePierce>()
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

        private void Execute(Entity projectile, [EntityIndexInQuery] int sortKey, in LocalTransform transform, in ProjectileDamage damage, ref ProjectilePierce pierce)
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

                // Handle pierce
                pierce.Remaining--;
                if (pierce.Remaining < 0)
                {
                    ECB.DestroyEntity(sortKey, projectile);
                }
                return;
            }
        }
    }
}
