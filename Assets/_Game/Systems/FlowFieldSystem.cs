using _Game.Components;
using _Game.Shared;
using _Game.Util;
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
    [UpdateBefore(typeof(EnemyMovementSystem))]
    public partial struct FlowFieldSystem : ISystem
    {
        private NativeArray<float2> _directions;
        private NativeArray<float2> _nearestAttractorPos;
        private NativeArray<float> _distances;
        private NativeArray<bool> _blocked;
        private NativeQueue<int2> _bfsQueue;

        private float _cellSize;
        private int _gridWidth;
        private int _gridHeight;
        private EntityQuery _attractorQuery;
        private int _lastAttractorHash;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
            _attractorQuery = state.GetEntityQuery(typeof(NavigationAttractor), typeof(LocalTransform));
            _lastAttractorHash = int.MinValue;

            state.EntityManager.CreateSingleton(new FlowField());
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_directions.IsCreated) _directions.Dispose();
            if (_nearestAttractorPos.IsCreated) _nearestAttractorPos.Dispose();
            if (_distances.IsCreated) _distances.Dispose();
            if (_blocked.IsCreated) _blocked.Dispose();
            if (_bfsQueue.IsCreated) _bfsQueue.Dispose();
        }

        private void Initialize(ref SystemState state)
        {
            ArenaBounds bounds = SystemAPI.GetSingleton<ArenaBounds>();
            _cellSize = bounds.FlowFieldCellSize;
            _gridWidth = (int)math.ceil(bounds.Width / _cellSize);
            _gridHeight = (int)math.ceil(bounds.Height / _cellSize);
            int totalCellCount = _gridWidth * _gridHeight;

            _directions = new NativeArray<float2>(totalCellCount, Allocator.Persistent);
            _nearestAttractorPos = new NativeArray<float2>(totalCellCount, Allocator.Persistent);
            _distances = new NativeArray<float>(totalCellCount, Allocator.Persistent);
            _blocked = new NativeArray<bool>(totalCellCount, Allocator.Persistent);
            _bfsQueue = new NativeQueue<int2>(Allocator.Persistent);

            Debug.Log($"[FlowFieldSystem] Initialized {_gridWidth}x{_gridHeight} grid");
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_directions.IsCreated) Initialize(ref state);

            SystemAPI.GetSingletonRW<FlowField>().ValueRW = new FlowField
            {
                CellSize = _cellSize,
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
                Directions = _directions,
                NearestAttractorPos = _nearestAttractorPos,
                Blocked = _blocked,
            };

            int currentCount = _attractorQuery.CalculateEntityCount();
            if (currentCount == 0) return;

            NativeArray<LocalTransform> transforms = _attractorQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);

            // Rebuild only when an attractor enters a new cell or the attractor count changes.
            // A moving beacon keeps the same count, so cell tracking is what triggers re-pathing;
            // while it stays within a cell the BFS is skipped entirely.
            int attractorHash = currentCount;
            for (int i = 0; i < transforms.Length; i++)
            {
                float2 attractorPos = transforms[i].Position.xy;
                int2 attractorCell = FlowFieldUtil.WorldToCell(attractorPos, _cellSize, _gridWidth, _gridHeight);
                attractorHash = (attractorHash * 397) ^ (attractorCell.x * 73856093) ^ (attractorCell.y * 19349663);
            }

            bool rebuild = attractorHash != _lastAttractorHash;
            _lastAttractorHash = attractorHash;

            state.Dependency = new PrepareFlowFieldJob
            {
                AttractorTransforms = transforms,
                Rebuild = rebuild,
                Queue = _bfsQueue,
                Distances = _distances,
                Directions = _directions,
                NearestAttractorPos = _nearestAttractorPos,
                CellSize = _cellSize,
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
            }.Schedule(state.Dependency);

            if (!rebuild) return;

            NativeArray<NavigationAttractor> attractors = _attractorQuery.ToComponentDataArray<NavigationAttractor>(state.WorldUpdateAllocator);

            state.Dependency = new FlowFieldJob
            {
                Queue = _bfsQueue,
                Distances = _distances,
                Blocked = _blocked,
                Directions = _directions,
                NearestAttractorPos = _nearestAttractorPos,
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
                Attractors = attractors,
                AttractorTransforms = transforms,
                CellSize = _cellSize,
            }.Schedule(state.Dependency);
        }
    }

    /// <summary>
    /// Points each attractor's cell at the attractor's current position and, on a rebuild, resets every cell and seeds the BFS.
    /// </summary>
    [BurstCompile]
    internal struct PrepareFlowFieldJob : IJob
    {
        [ReadOnly] public NativeArray<LocalTransform> AttractorTransforms;
        public bool Rebuild;

        public NativeQueue<int2> Queue;
        public NativeArray<float> Distances;
        public NativeArray<float2> Directions;
        public NativeArray<float2> NearestAttractorPos;

        public float CellSize;
        public int GridWidth;
        public int GridHeight;

        public void Execute()
        {
            if (Rebuild)
            {
                for (int i = 0; i < Distances.Length; i++)
                {
                    Distances[i] = float.MaxValue;
                    Directions[i] = float2.zero;
                    NearestAttractorPos[i] = FlowFieldUtil.CellToWorld(new int2(i % GridWidth, i / GridWidth), CellSize, GridWidth, GridHeight);
                }

                Queue.Clear();
            }

            for (int i = 0; i < AttractorTransforms.Length; i++)
            {
                float2 attractorPos = AttractorTransforms[i].Position.xy;
                int2 cell = math.clamp(FlowFieldUtil.WorldToCell(attractorPos, CellSize, GridWidth, GridHeight), int2.zero, new int2(GridWidth - 1, GridHeight - 1));
                int idx = FlowFieldUtil.GetIndex(cell, GridWidth);

                // The cell the attractor stands in has no flow direction, so enemies there steer straight at
                // NearestAttractorPos instead. Refresh it every frame so they chase the attractor's current position.
                NearestAttractorPos[idx] = attractorPos;

                if (!Rebuild) continue;

                Distances[idx] = 0;
                Queue.Enqueue(cell);
            }
        }
    }
}
