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

        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _enemyPositions = new NativeArray<float2>(InitialEnemyCapacity, Allocator.Persistent);
            _enemyEntities = new NativeArray<Entity>(InitialEnemyCapacity, Allocator.Persistent);

            _allyPositions = new NativeArray<float2>(InitialAllyCapacity, Allocator.Persistent);
            _allyEntities = new NativeArray<Entity>(InitialAllyCapacity, Allocator.Persistent);

            _unitQuery = SystemAPI.QueryBuilder().WithAll<UnitFaction, LocalTransform>().Build();

            state.EntityManager.CreateSingleton(new UnitData());
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_enemyPositions.IsCreated) _enemyPositions.Dispose();
            if (_enemyEntities.IsCreated) _enemyEntities.Dispose();
            if (_allyPositions.IsCreated) _allyPositions.Dispose();
            if (_allyEntities.IsCreated) _allyEntities.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            _unitQuery.SetSharedComponentFilter(new UnitFaction { Value = Faction.Enemy });
            int enemyCount = _unitQuery.CalculateEntityCount();
            EnsureCapacity(ref state, ref _enemyPositions, ref _enemyEntities, enemyCount);

            JobHandle enemyHandle = dependency;
            if (enemyCount > 0)
            {
                enemyHandle = new CopyUnitDataJob
                {
                    Positions = _enemyPositions,
                    Entities = _enemyEntities
                }.ScheduleParallel(_unitQuery, dependency);
            }

            _unitQuery.SetSharedComponentFilter(new UnitFaction { Value = Faction.Player });
            int allyCount = _unitQuery.CalculateEntityCount();
            EnsureCapacity(ref state, ref _allyPositions, ref _allyEntities, allyCount);

            JobHandle allyHandle = dependency;
            if (allyCount > 0)
            {
                allyHandle = new CopyUnitDataJob
                {
                    Positions = _allyPositions,
                    Entities = _allyEntities
                }.ScheduleParallel(_unitQuery, dependency);
            }

            _unitQuery.ResetFilter();

            state.Dependency = JobHandle.CombineDependencies(enemyHandle, allyHandle);

            // Registers this system as the writer, so systems reading UnitData wait for the copy jobs
            SystemAPI.GetSingletonRW<UnitData>().ValueRW = new UnitData
            {
                Enemies = new UnitSnapshot { Positions = _enemyPositions, Entities = _enemyEntities, Count = enemyCount },
                Allies = new UnitSnapshot { Positions = _allyPositions, Entities = _allyEntities, Count = allyCount },
            };
        }

        /// <summary>
        /// Reallocates the arrays (doubling) when count exceeds their length.
        /// </summary>
        private static void EnsureCapacity(ref SystemState state, ref NativeArray<float2> positions, ref NativeArray<Entity> entities, int count)
        {
            if (count <= positions.Length) return;

            state.EntityManager.CompleteAllTrackedJobs();

            int capacity = math.max(count, positions.Length * 2);
            positions.Dispose();
            entities.Dispose();
            positions = new NativeArray<float2>(capacity, Allocator.Persistent);
            entities = new NativeArray<Entity>(capacity, Allocator.Persistent);
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
