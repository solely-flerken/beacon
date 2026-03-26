using _Game.Components;
using _Game.Config;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyDataSystem))]
    [UpdateBefore(typeof(EnemyMovementSystem))]
    public partial struct SpatialGridSystem : ISystem
    {
        private NativeParallelMultiHashMap<int2, int> _cellMap;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyTag>();

            int gridWidth = (int)math.ceil(ArenaConfig.Width / ArenaConfig.SpatialGridCellSize);
            int gridHeight = (int)math.ceil(ArenaConfig.Height / ArenaConfig.SpatialGridCellSize);

            // Also limits count of enemies in each cell
            _cellMap = new NativeParallelMultiHashMap<int2, int>(gridWidth * gridHeight * 4, Allocator.Persistent);

            SpatialGridShared.CellSize = ArenaConfig.SpatialGridCellSize;
            SpatialGridShared.GridWidth = gridWidth;
            SpatialGridShared.GridHeight = gridHeight;
            SpatialGridShared.CellMap = _cellMap;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_cellMap.IsCreated) _cellMap.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!EnemyDataShared.IsValid) return;

            _cellMap.Clear();

            NativeArray<float2> positions = EnemyDataShared.Positions;
            int count = EnemyDataShared.Count;

            new RebuildGridJob
            {
                Positions = positions,
                CellSize = ArenaConfig.SpatialGridCellSize,
                Grid = _cellMap.AsParallelWriter(),
            }.Schedule(count, 64).Complete();
        }
    }

    [BurstCompile]
    internal struct RebuildGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public float CellSize;
        [WriteOnly] public NativeParallelMultiHashMap<int2, int>.ParallelWriter Grid;

        public void Execute(int i)
        {
            float2 pos = Positions[i];
            Grid.Add(new int2((int)math.floor(pos.x / CellSize), (int)math.floor(pos.y / CellSize)), i);
        }
    }
}