using _Game.Components;
using _Game.Shared;
using _Game.Util;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FlowFieldSystem : ISystem
    {
        private NativeArray<float2> _directions;
        private NativeArray<float2> _nearestAttractorPos;
        private NativeArray<float> _distances;
        private NativeArray<bool> _blocked;
        private NativeQueue<int2> _bfsQueue;

        private int _gridWidth;
        private int _gridHeight;
        private EntityQuery _attractorQuery;
        private int _lastAttractorHash;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
            _attractorQuery = state.GetEntityQuery(typeof(NavigationAttractor), typeof(LocalTransform));
            _lastAttractorHash = int.MinValue;
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
            _gridWidth = (int)math.ceil(bounds.Width / bounds.FlowFieldCellSize);
            _gridHeight = (int)math.ceil(bounds.Height / bounds.FlowFieldCellSize);
            int totalCellCount = _gridWidth * _gridHeight;

            _directions = new NativeArray<float2>(totalCellCount, Allocator.Persistent);
            _nearestAttractorPos = new NativeArray<float2>(totalCellCount, Allocator.Persistent);
            _distances = new NativeArray<float>(totalCellCount, Allocator.Persistent);
            _blocked = new NativeArray<bool>(totalCellCount, Allocator.Persistent);
            _bfsQueue = new NativeQueue<int2>(Allocator.Persistent);

            FlowFieldShared.CellSize = bounds.FlowFieldCellSize;
            FlowFieldShared.GridWidth = _gridWidth;
            FlowFieldShared.GridHeight = _gridHeight;
            FlowFieldShared.Directions = _directions;
            FlowFieldShared.NearestAttractorPos = _nearestAttractorPos;
            FlowFieldShared.Blocked = _blocked;

            Debug.Log($"[FlowFieldSystem] Initialized {_gridWidth}x{_gridHeight} grid");
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_directions.IsCreated) Initialize(ref state);

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
                int2 attractorCell = FlowFieldUtil.WorldToCell(attractorPos, FlowFieldShared.CellSize, _gridWidth, _gridHeight);
                attractorHash = (attractorHash * 397) ^ (attractorCell.x * 73856093) ^ (attractorCell.y * 19349663);

                // The cell the attractor stands in has no flow direction, so enemies there steer straight at
                // _nearestAttractorPos instead. Refresh it every frame so they chase the attractor's current position.
                _nearestAttractorPos[FlowFieldUtil.GetIndex(attractorCell, _gridWidth)] = attractorPos;
            }

            if (attractorHash == _lastAttractorHash) return;
            _lastAttractorHash = attractorHash;

            NativeArray<NavigationAttractor> attractors = _attractorQuery.ToComponentDataArray<NavigationAttractor>(state.WorldUpdateAllocator);

            // Reset every cell.
            for (int i = 0; i < _distances.Length; i++)
            {
                _distances[i] = float.MaxValue;
                _directions[i] = float2.zero;
                _nearestAttractorPos[i] = FlowFieldUtil.CellToWorld(new int2(i % _gridWidth, i / _gridWidth), FlowFieldShared.CellSize, _gridWidth, _gridHeight);
            }

            _bfsQueue.Clear();

            for (int i = 0; i < attractors.Length; i++)
            {
                int2 cell = FlowFieldUtil.WorldToCell(transforms[i].Position.xy, FlowFieldShared.CellSize, _gridWidth, _gridHeight);
                cell = math.clamp(cell, int2.zero, new int2(_gridWidth - 1, _gridHeight - 1));
                int idx = cell.y * _gridWidth + cell.x;
                _distances[idx] = 0;
                _nearestAttractorPos[idx] = transforms[i].Position.xy;
                _bfsQueue.Enqueue(cell);
            }

            new FlowFieldJob
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
                CellSize = FlowFieldShared.CellSize,
            }.Run();
        }
    }
}