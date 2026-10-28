using _Game.Components;
using _Game.Shared;
using _Game.Util;
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
        // Starting capacities. Arrays grow on demand, so these are not limits.
        private const int InitialEnemyCapacity = 10000;
        private const int InitialAllyCapacity = 1000;

        private GridBuffers _enemyBuffers;
        private GridBuffers _allyBuffers;
        private SpatialGrid _layout;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<UnitData>();

            state.EntityManager.CreateSingleton(new SpatialGrids());
        }

        public void OnDestroy(ref SystemState state)
        {
            _enemyBuffers.Dispose();
            _allyBuffers.Dispose();
        }

        private void Initialize(ref SystemState state)
        {
            ArenaBounds bounds = SystemAPI.GetSingleton<ArenaBounds>();
            float cellSize = bounds.SpatialGridCellSize;

            _layout = new SpatialGrid
            {
                Origin = new float2(-bounds.Width * 0.5f, -bounds.Height * 0.5f),
                CellSize = cellSize,
                InvCellSize = 1f / cellSize,
                Width = (int)math.ceil(bounds.Width / cellSize),
                Height = (int)math.ceil(bounds.Height / cellSize),
            };

            int cellCount = _layout.Width * _layout.Height;
            _enemyBuffers = new GridBuffers(cellCount, InitialEnemyCapacity);
            _allyBuffers = new GridBuffers(cellCount, InitialAllyCapacity);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Both buffers are created together -> need to only check one
            if (!_enemyBuffers.IsCreated)
            {
                Initialize(ref state);
            }

            UnitData units = SystemAPI.GetSingleton<UnitData>();

            _enemyBuffers.EnsureCapacity(ref state, units.Enemies.Count);
            _allyBuffers.EnsureCapacity(ref state, units.Allies.Count);

            JobHandle enemyHandle = ScheduleBuild(_enemyBuffers, units.Enemies, state.Dependency);
            JobHandle allyHandle = ScheduleBuild(_allyBuffers, units.Allies, state.Dependency);
            state.Dependency = JobHandle.CombineDependencies(enemyHandle, allyHandle);

            // Registers this system as the writer, so systems reading SpatialGrids wait for the build jobs
            SystemAPI.GetSingletonRW<SpatialGrids>().ValueRW = new SpatialGrids
            {
                Enemies = _enemyBuffers.ToGrid(_layout, units.Enemies.Count),
                Allies = _allyBuffers.ToGrid(_layout, units.Allies.Count),
            };
        }

        private JobHandle ScheduleBuild(GridBuffers buffers, UnitSnapshot units, JobHandle dependency)
        {
            return new BuildGridJob
            {
                Positions = units.Positions,
                Entities = units.Entities,
                EntityCount = units.Count,
                Origin = _layout.Origin,
                InvCellSize = _layout.InvCellSize,
                Width = _layout.Width,
                Height = _layout.Height,
                EntityCells = buffers.EntityCells,
                CellFirstIndex = buffers.CellFirstIndex,
                SortedPositions = buffers.SortedPositions,
                SortedEntities = buffers.SortedEntities,
            }.Schedule(dependency);
        }

        private struct GridBuffers
        {
            public NativeArray<int> CellFirstIndex;
            public NativeArray<int> EntityCells;
            public NativeArray<float2> SortedPositions;
            public NativeArray<Entity> SortedEntities;

            public bool IsCreated => CellFirstIndex.IsCreated;

            public GridBuffers(int cellCount, int capacity)
            {
                CellFirstIndex = new NativeArray<int>(cellCount + 1, Allocator.Persistent);
                EntityCells = new NativeArray<int>(capacity, Allocator.Persistent);
                SortedPositions = new NativeArray<float2>(capacity, Allocator.Persistent);
                SortedEntities = new NativeArray<Entity>(capacity, Allocator.Persistent);
            }

            /// <summary>
            /// Reallocates the per-unit arrays (doubling) when count exceeds their length.
            /// </summary>
            public void EnsureCapacity(ref SystemState state, int count)
            {
                if (count <= SortedPositions.Length) return;

                state.EntityManager.CompleteAllTrackedJobs();

                EntityCells.Dispose();
                SortedPositions.Dispose();
                SortedEntities.Dispose();
                
                int capacity = math.max(count, SortedPositions.Length * 2);
                EntityCells = new NativeArray<int>(capacity, Allocator.Persistent);
                SortedPositions = new NativeArray<float2>(capacity, Allocator.Persistent);
                SortedEntities = new NativeArray<Entity>(capacity, Allocator.Persistent);
            }

            public SpatialGrid ToGrid(SpatialGrid layout, int count)
            {
                layout.CellFirstIndex = CellFirstIndex;
                layout.Positions = SortedPositions;
                layout.Entities = SortedEntities;
                layout.Count = count;
                return layout;
            }

            public void Dispose()
            {
                if (CellFirstIndex.IsCreated) CellFirstIndex.Dispose();
                if (EntityCells.IsCreated) EntityCells.Dispose();
                if (SortedPositions.IsCreated) SortedPositions.Dispose();
                if (SortedEntities.IsCreated) SortedEntities.Dispose();
            }
        }
    }

    [BurstCompile]
    internal struct BuildGridJob : IJob
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<Entity> Entities;
        public int EntityCount;
        public float2 Origin;
        public float InvCellSize;
        public int Width;
        public int Height;

        public NativeArray<int> EntityCells;
        
        public NativeArray<int> CellFirstIndex;
        [WriteOnly] public NativeArray<float2> SortedPositions;
        [WriteOnly] public NativeArray<Entity> SortedEntities;

        public void Execute()
        {
            int cellCount = CellFirstIndex.Length - 1;
            for (int c = 0; c < cellCount; c++)
            {
                CellFirstIndex[c] = 0;
            }

            // Count entities per cell
            for (int i = 0; i < EntityCount; i++)
            {
                int2 cell = math.clamp((int2)math.floor((Positions[i] - Origin) * InvCellSize), int2.zero, new int2(Width - 1, Height - 1));
                int cellIndex = cell.y * Width + cell.x;
                EntityCells[i] = cellIndex;
                CellFirstIndex[cellIndex]++;
            }

            // Prefix sum
            for (int c = 1; c < cellCount; c++)
            {
                CellFirstIndex[c] += CellFirstIndex[c - 1];
            }
            
            // Scatter
            for (int i = EntityCount - 1; i >= 0; i--)
            {
                int sortedIndex = --CellFirstIndex[EntityCells[i]];
                SortedPositions[sortedIndex] = Positions[i];
                SortedEntities[sortedIndex] = Entities[i];
            }
            
            CellFirstIndex[cellCount] = EntityCount;
        }
    }
}
