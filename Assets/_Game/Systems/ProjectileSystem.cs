using _Game.Components;
using _Game.Core;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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

            // Check collisions
            bool hasHitQueue = DotsBridge.HitQueue.IsCreated;
            NativeQueue<HitEvent>.ParallelWriter hitQueueWriter = default;
            if (hasHitQueue) hitQueueWriter = DotsBridge.HitQueue.AsParallelWriter();

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
                    HitQueue = hitQueueWriter
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
                    HasHitQueue = hasHitQueue,
                    HitQueue = hitQueueWriter
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

                    // Add damage to the enemy's buffer
                    ECB.AppendToBuffer(sortKey, TargetEntities[enemyIdx], new DamageBufferElement
                    { 
                        Value = damage.Value 
                    });
                    
                    // Send Hit Event to Bridge
                    if (HasHitQueue)
                    {
                        HitQueue.Enqueue(new HitEvent 
                        { 
                            Position = transform.Position, 
                            Damage = damage.Value 
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