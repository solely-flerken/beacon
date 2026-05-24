using _Game.Components;
using _Game.Core;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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

            // Depend on any prior HitQueue writers (e.g. BeamDamageJob) before scheduling collision jobs
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, DotsBridge.BridgeDependencies);

            // Check collisions
            bool hasHitQueue = DotsBridge.HitQueue.IsCreated;
            NativeQueue<HitEvent>.ParallelWriter hitQueueWriter = default;
            if (hasHitQueue) hitQueueWriter = DotsBridge.HitQueue.AsParallelWriter();

            BufferLookup<DamageBufferElement> damageBufferLookup = SystemAPI.GetBufferLookup<DamageBufferElement>(true);
            BufferLookup<HitCooldown> hitCooldownLookup = SystemAPI.GetBufferLookup<HitCooldown>(true);
            
            ComponentLookup<ProjectileWeapon> weaponSourceLookup = SystemAPI.GetComponentLookup<ProjectileWeapon>(true);

            // PLAYER PROJECTILES vs ENEMIES
            if (SpatialGridShared.EnemyGridValid && !_playerProjectiles.IsEmpty)
            {
                EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
                
                state.Dependency = new CollisionJob
                {
                    TargetPositions = EnemyDataShared.Positions,
                    TargetEntities = EnemyDataShared.Entities,
                    SpatialGrid = SpatialGridShared.EnemyCellMap,
                    SpatialGridCellSize = SpatialGridShared.CellSize,
                    HitRadius = 0.5f,
                    ECB = ecb,
                    HasHitQueue = hasHitQueue,
                    HitQueue = hitQueueWriter,
                    DamageBuffer = damageBufferLookup,
                    HitCooldowns = hitCooldownLookup,
                    WeaponSourceLookup = weaponSourceLookup,
                }.ScheduleParallel(_playerProjectiles, state.Dependency);
            }

            // ENEMY PROJECTILES vs PLAYER
            if (SpatialGridShared.AllyGridValid && !_enemyProjectiles.IsEmpty)
            {
                EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
                
                state.Dependency = new CollisionJob
                {
                    TargetPositions = AllyDataShared.Positions,
                    TargetEntities = AllyDataShared.Entities,
                    SpatialGrid = SpatialGridShared.AllyCellMap,
                    SpatialGridCellSize = SpatialGridShared.CellSize,
                    HitRadius = 0.5f,
                    ECB = ecb,
                    HasHitQueue = false, // TODO: enemy -> beacon hits don't count as player weapon stats. Need to create a dedicated HitQueue
                    HitQueue = hitQueueWriter,
                    DamageBuffer = damageBufferLookup,
                    HitCooldowns = hitCooldownLookup,
                    WeaponSourceLookup = weaponSourceLookup,
                }.ScheduleParallel(_enemyProjectiles, state.Dependency);
            }
            
            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
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
        private static readonly int2[] NeighbourOffsets =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1, 0), new(0, 0), new(1, 0),
            new(-1, 1), new(0, 1), new(1, 1),
        };

        [ReadOnly] public NativeArray<float2> TargetPositions;
        [ReadOnly] public NativeArray<Entity> TargetEntities;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> SpatialGrid;
        [ReadOnly] public float SpatialGridCellSize;
        [ReadOnly] public float HitRadius;
        
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public bool HasHitQueue;
        [WriteOnly] public NativeQueue<HitEvent>.ParallelWriter HitQueue;
        
        [ReadOnly] public BufferLookup<DamageBufferElement> DamageBuffer;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;
        [ReadOnly] public ComponentLookup<ProjectileWeapon> WeaponSourceLookup;

        private void Execute(Entity projectile, [EntityIndexInQuery] int sortKey, in LocalTransform transform, in ProjectileDamage damage, ref ProjectilePierce pierce)
        {
            float2 pos = new(transform.Position.x, transform.Position.y);
            float hitRadiusSq = HitRadius * HitRadius;
            int2 cell = new((int)math.floor(pos.x / SpatialGridCellSize), (int)math.floor(pos.y / SpatialGridCellSize));

            foreach (int2 offset in NeighbourOffsets)
            {
                if (!SpatialGrid.TryGetFirstValue(cell + offset, out int enemyIdx, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                do
                {
                    if (math.distancesq(pos, TargetPositions[enemyIdx]) > hitRadiusSq) continue;

                    Entity target = TargetEntities[enemyIdx];

                    // Check if projectile can damage the current entity
                    if (HitCooldowns.HasBuffer(target))
                    {
                        bool onCooldown = false;
                        DynamicBuffer<HitCooldown> cds = HitCooldowns[target];
                        
                        for (int i = 0; i < cds.Length; i++)
                        {
                            if (cds[i].Source != projectile) continue;
                            
                            onCooldown = true;
                            break;
                        }
                        
                        // Projectile can't hit the entity - continue with next
                        if (onCooldown) continue;
                        
                        ECB.AppendToBuffer(sortKey, target, new HitCooldown { Source = projectile, Expiry = float.PositiveInfinity });
                    }
                    else
                    {
                        Debug.Log($"[ProjectileSystem] Entity {target.Index} is missing HitCooldown! Skipping hit."); // TODO: Development only. Remove later.
                    }

                    Entity weaponEntity = WeaponSourceLookup.HasComponent(projectile) ? WeaponSourceLookup[projectile].Value : Entity.Null;

                    // Append damage
                    if (DamageBuffer.HasBuffer(target))
                    {
                        ECB.AppendToBuffer(sortKey, target, new DamageBufferElement { Value = damage.Value });
                    }
                    else
                    {
                        Debug.Log($"[ProjectileSystem] Entity {target.Index} is missing DamageBuffer! Skipping hit."); // TODO: Development only. Remove later.
                    }

                    // Send Hit Event to Bridge
                    if (HasHitQueue)
                    {
                        HitQueue.Enqueue(new HitEvent
                        {
                            Position = transform.Position,
                            Damage = damage.Value,
                            WeaponEntity = weaponEntity,
                        });
                    }
                    
                    // Handle pierce 
                    pierce.Remaining--;
                    if (pierce.Remaining <= 0)
                    {
                        ECB.DestroyEntity(sortKey, projectile);
                    }
                    return;
                } while (SpatialGrid.TryGetNextValue(out enemyIdx, ref it));
            }
        }
    }
}