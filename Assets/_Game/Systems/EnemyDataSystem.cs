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
    public partial struct EnemyDataSystem : ISystem
    {
        private const int MaxEnemies = 10000;

        private NativeArray<float2> _positions;
        private NativeArray<Entity> _entities;

        public void OnCreate(ref SystemState state)
        {
            _positions = new NativeArray<float2>(MaxEnemies, Allocator.Persistent);
            _entities = new NativeArray<Entity>(MaxEnemies, Allocator.Persistent);

            EnemyDataShared.Positions = _positions;
            EnemyDataShared.Entities = _entities;
            EnemyDataShared.Count = 0;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_positions.IsCreated) _positions.Dispose();
            if (_entities.IsCreated) _entities.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform>().Build();

            int count = query.CalculateEntityCount();

            if (count == 0)
            {
                EnemyDataShared.Count = 0;
                return;
            }

            using NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.TempJob);

            new CopyPositionsJob
            {
                Transforms = transforms,
                Positions = _positions,
            }.Schedule(count, 64).Complete();

            for (int i = 0; i < count; i++)
            {
                _entities[i] = entities[i];
            }

            EnemyDataShared.Count = count;
        }
    }

    [BurstCompile]
    internal struct CopyPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<LocalTransform> Transforms;
        [WriteOnly] public NativeArray<float2> Positions;

        public void Execute(int i)
        {
            Positions[i] = new float2(Transforms[i].Position.x, Transforms[i].Position.y);
        }
    }
}