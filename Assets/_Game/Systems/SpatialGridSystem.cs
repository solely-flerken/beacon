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
    [UpdateAfter(typeof(UnitDataSystem))]
    [UpdateBefore(typeof(EnemyMovementSystem))]
    public partial struct SpatialGridSystem : ISystem
    {
        private NativeParallelMultiHashMap<int2, int> _enemyCellMap;
        private NativeParallelMultiHashMap<int2, int> _allyCellMap;

        public void OnCreate(ref SystemState state)
        {
            int gridWidth = (int)math.ceil(ArenaConfig.Width / ArenaConfig.SpatialGridCellSize);
            int gridHeight = (int)math.ceil(ArenaConfig.Height / ArenaConfig.SpatialGridCellSize);
            int capacity = gridWidth * gridHeight * 4;
            
            // Also limits count of enemies in each cell
            _enemyCellMap = new NativeParallelMultiHashMap<int2, int>(capacity, Allocator.Persistent);
            _allyCellMap = new NativeParallelMultiHashMap<int2, int>(capacity, Allocator.Persistent);

            SpatialGridShared.CellSize = ArenaConfig.SpatialGridCellSize;
            SpatialGridShared.GridWidth = gridWidth;
            SpatialGridShared.GridHeight = gridHeight;
            
            SpatialGridShared.EnemyCellMap = _enemyCellMap;
            SpatialGridShared.AllyCellMap = _allyCellMap;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_enemyCellMap.IsCreated) _enemyCellMap.Dispose();
            if (_allyCellMap.IsCreated) _allyCellMap.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle enemyHandle = default;
            if (EnemyDataShared.IsValid)
            {
                _enemyCellMap.Clear();
                
                enemyHandle = new RebuildGridJob
                {
                    Positions = EnemyDataShared.Positions,
                    CellSize = ArenaConfig.SpatialGridCellSize,
                    Grid = _enemyCellMap.AsParallelWriter(),
                }.Schedule(EnemyDataShared.Count, 64, state.Dependency);
            }

            JobHandle allyHandle = default;
            if (AllyDataShared.IsValid)
            {
                _allyCellMap.Clear();
                
                allyHandle = new RebuildGridJob
                {
                    Positions = AllyDataShared.Positions,
                    CellSize = ArenaConfig.SpatialGridCellSize,
                    Grid = _allyCellMap.AsParallelWriter(),
                }.Schedule(AllyDataShared.Count, 64, state.Dependency);
            }

            JobHandle.CombineDependencies(enemyHandle, allyHandle).Complete();
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