using _Game.Components;
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
    [UpdateBefore(typeof(SpatialGridSystem))]
    public partial struct UnitDataSystem : ISystem
    {
        // Starting capacities. Arrays grow on demand, so these are not limits.
        private const int InitialEnemyCapacity = 10000;
        private const int InitialAllyCapacity = 1000;

        private NativeArray<float2> _enemyPositions;
        private NativeArray<Entity> _enemyEntities;

        private NativeArray<float2> _allyPositions;
        private NativeArray<Entity> _allyEntities;

        private EntityQuery _enemyQuery;
        private EntityQuery _allyQuery;

        public void OnCreate(ref SystemState state)
        {
            _enemyPositions = new NativeArray<float2>(InitialEnemyCapacity, Allocator.Persistent);
            _enemyEntities = new NativeArray<Entity>(InitialEnemyCapacity, Allocator.Persistent);
            EnemyDataShared.Positions = _enemyPositions;
            EnemyDataShared.Entities = _enemyEntities;

            _allyPositions = new NativeArray<float2>(InitialAllyCapacity, Allocator.Persistent);
            _allyEntities = new NativeArray<Entity>(InitialAllyCapacity, Allocator.Persistent);
            AllyDataShared.Positions = _allyPositions;
            AllyDataShared.Entities = _allyEntities;

            _enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform>().Build();
            _allyQuery = SystemAPI.QueryBuilder().WithAll<PlayerTag, LocalTransform>().Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_enemyPositions.IsCreated) _enemyPositions.Dispose();
            if (_enemyEntities.IsCreated) _enemyEntities.Dispose();
            if (_allyPositions.IsCreated) _allyPositions.Dispose();
            if (_allyEntities.IsCreated) _allyEntities.Dispose();

            EnemyDataShared.Positions = default;
            EnemyDataShared.Entities = default;
            EnemyDataShared.Count = 0;
            AllyDataShared.Positions = default;
            AllyDataShared.Entities = default;
            AllyDataShared.Count = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            EnemyDataShared.Count = _enemyQuery.CalculateEntityCount();
            AllyDataShared.Count = _allyQuery.CalculateEntityCount();

            if (EnsureCapacity(ref state, ref _enemyPositions, ref _enemyEntities, EnemyDataShared.Count))
            {
                EnemyDataShared.Positions = _enemyPositions;
                EnemyDataShared.Entities = _enemyEntities;
            }

            if (EnsureCapacity(ref state, ref _allyPositions, ref _allyEntities, AllyDataShared.Count))
            {
                AllyDataShared.Positions = _allyPositions;
                AllyDataShared.Entities = _allyEntities;
            }

            JobHandle enemyHandle = default;
            if (EnemyDataShared.Count > 0)
            {
                enemyHandle = new CopyUnitDataJob
                {
                    Positions = _enemyPositions,
                    Entities = _enemyEntities
                }.ScheduleParallel(_enemyQuery, state.Dependency);
            }

            JobHandle allyHandle = default;
            if (AllyDataShared.Count > 0)
            {
                allyHandle = new CopyUnitDataJob
                {
                    Positions = _allyPositions,
                    Entities = _allyEntities
                }.ScheduleParallel(_allyQuery, state.Dependency);
            }

            state.Dependency = JobHandle.CombineDependencies(enemyHandle, allyHandle, state.Dependency);
            state.Dependency.Complete();
        }

        /// <summary>
        /// Reallocates the arrays (doubling) when count exceeds their length. Returns true if they were replaced.
        /// </summary>
        private static bool EnsureCapacity(ref SystemState state, ref NativeArray<float2> positions, ref NativeArray<Entity> entities, int count)
        {
            if (count <= positions.Length) return false;

            state.EntityManager.CompleteAllTrackedJobs();

            int capacity = math.max(count, positions.Length * 2);
            positions.Dispose();
            entities.Dispose();
            positions = new NativeArray<float2>(capacity, Allocator.Persistent);
            entities = new NativeArray<Entity>(capacity, Allocator.Persistent);
            
            return true;
        }
    }

    [BurstCompile]
    internal partial struct CopyUnitDataJob : IJobEntity
    {
        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float2> Positions;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<Entity> Entities;

        private void Execute(Entity entity, [EntityIndexInQuery] int index, in LocalTransform transform)
        {
            Positions[index] = new float2(transform.Position.x, transform.Position.y);
            Entities[index] = entity;
        }
    }
}
