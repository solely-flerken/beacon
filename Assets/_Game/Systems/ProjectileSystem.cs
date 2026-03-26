using _Game.Components;
using _Game.Core;
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
    public partial struct ProjectileSystem : ISystem
    {
        public struct HitRecord
        {
            public Entity ProjectileEntity;
            public Entity EnemyEntity;
            public float Damage;
        }

        private NativeQueue<HitRecord> _hitQueue;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
            _hitQueue = new NativeQueue<HitRecord>(Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_hitQueue.IsCreated) _hitQueue.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Move Projectiles
            new MoveProjectilesJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
            state.Dependency.Complete();

            // Collision detection
            _hitQueue.Clear();

            if (!EnemyDataShared.IsValid || !SpatialGridShared.IsValid) return;
            
            new CollisionJob
            {
                EnemyPositions = EnemyDataShared.Positions,
                EnemyEntities = EnemyDataShared.Entities,
                SpatialGrid = SpatialGridShared.CellMap,
                SpatialGridCellSize = SpatialGridShared.CellSize,
                HitRadius = 0.5f,
                HitQueue = _hitQueue.AsParallelWriter(),
            }.ScheduleParallel();
            state.Dependency.Complete();

            // Process hits
            EntityManager em = state.EntityManager;

            while (_hitQueue.TryDequeue(out HitRecord hit))
            {
                if (!em.Exists(hit.EnemyEntity)) continue;

                EnemyHealth health = em.GetComponentData<EnemyHealth>(hit.EnemyEntity);
                health.Current -= hit.Damage;

                if (health.Current <= 0f)
                {
                    float3 pos = em.GetComponentData<LocalTransform>(hit.EnemyEntity).Position;
                    EnemyReward reward = em.GetComponentData<EnemyReward>(hit.EnemyEntity);

                    if (DotsBridge.DeathQueue.IsCreated)
                    {
                        DotsBridge.DeathQueue.Enqueue(new DeathEvent { Position = pos, Exp = reward.ExpValue });
                    }

                    em.DestroyEntity(hit.EnemyEntity);
                }
                else
                {
                    em.SetComponentData(hit.EnemyEntity, health);

                    if (DotsBridge.HitQueue.IsCreated)
                    {
                        float3 pos = em.GetComponentData<LocalTransform>(hit.EnemyEntity).Position;
                        DotsBridge.HitQueue.Enqueue(new HitEvent { Position = pos, Damage = hit.Damage });
                    }
                }

                // Handle pierce
                if (!em.Exists(hit.ProjectileEntity)) continue;
                
                ProjectilePierce pierce = em.GetComponentData<ProjectilePierce>(hit.ProjectileEntity);
                pierce.Remaining--;
                if (pierce.Remaining < 0)
                {
                    em.DestroyEntity(hit.ProjectileEntity);
                }
                else
                {
                    em.SetComponentData(hit.ProjectileEntity, pierce);
                }
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
        private static readonly int2[] NeighbourOffsets =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1, 0), new(0, 0), new(1, 0),
            new(-1, 1), new(0, 1), new(1, 1),
        };

        [ReadOnly] public NativeArray<float2> EnemyPositions;
        [ReadOnly] public NativeArray<Entity> EnemyEntities;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> SpatialGrid;
        [ReadOnly] public float SpatialGridCellSize;
        [ReadOnly] public float HitRadius;

        [WriteOnly] public NativeQueue<ProjectileSystem.HitRecord>.ParallelWriter HitQueue;

        private void Execute(Entity projectile, in LocalTransform transform, in ProjectileDamage damage)
        {
            float2 pos = new(transform.Position.x, transform.Position.y);
            float hitRadiusSq = HitRadius * HitRadius;
            int2 cell = new((int)math.floor(pos.x / SpatialGridCellSize), (int)math.floor(pos.y / SpatialGridCellSize));

            foreach (int2 offset in NeighbourOffsets)
            {
                if (!SpatialGrid.TryGetFirstValue(cell + offset, out int enemyIdx, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                do
                {
                    if (math.distancesq(pos, EnemyPositions[enemyIdx]) > hitRadiusSq) continue;

                    HitQueue.Enqueue(new ProjectileSystem.HitRecord
                    {
                        ProjectileEntity = projectile,
                        EnemyEntity = EnemyEntities[enemyIdx],
                        Damage = damage.Value,
                    });
                    return;
                } while (SpatialGrid.TryGetNextValue(out enemyIdx, ref it));
            }
        }
    }
}