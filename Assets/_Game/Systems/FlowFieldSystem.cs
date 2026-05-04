using _Game.Components;
using _Game.Config;
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
        private NativeArray<float2> _attractorOwner;
        private NativeArray<float> _distances;
        private NativeArray<bool> _blocked;
        private NativeQueue<int2> _bfsQueue;

        private int _gridWidth;
        private int _gridHeight;
        private EntityQuery _attractorQuery;
        private int _lastAttractorCount;

        public void OnCreate(ref SystemState state)
        {
            _attractorQuery = state.GetEntityQuery(typeof(NavigationAttractor), typeof(LocalTransform));

            _gridWidth = (int)math.ceil(ArenaConfig.Width / ArenaConfig.FlowFieldCellSize);
            _gridHeight = (int)math.ceil(ArenaConfig.Height / ArenaConfig.FlowFieldCellSize);

            int totalCellCount = _gridWidth * _gridHeight;
            _directions = new NativeArray<float2>(totalCellCount, Allocator.Persistent);
            _attractorOwner = new NativeArray<float2>(totalCellCount, Allocator.Persistent);
            _distances = new NativeArray<float>(totalCellCount, Allocator.Persistent);
            _blocked = new NativeArray<bool>(totalCellCount, Allocator.Persistent);
            _bfsQueue = new NativeQueue<int2>(Allocator.Persistent);

            FlowFieldShared.CellSize = ArenaConfig.FlowFieldCellSize;
            FlowFieldShared.GridWidth = _gridWidth;
            FlowFieldShared.GridHeight = _gridHeight;
            FlowFieldShared.Directions = _directions;
            FlowFieldShared.AttractorOwner = _attractorOwner;
            FlowFieldShared.Blocked = _blocked;

            Debug.Log($"[FlowFieldSystem] Initialized {_gridWidth}x{_gridHeight} grid");
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_directions.IsCreated) _directions.Dispose();
            if (_attractorOwner.IsCreated)  _attractorOwner.Dispose();
            if (_distances.IsCreated) _distances.Dispose();
            if (_blocked.IsCreated) _blocked.Dispose();
            if (_bfsQueue.IsCreated) _bfsQueue.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            int currentCount = _attractorQuery.CalculateEntityCount();
            if (currentCount == 0) return;
            if (currentCount == _lastAttractorCount) return;

            _lastAttractorCount = currentCount;

            for (int i = 0; i < _distances.Length; i++)
            {
                _distances[i] = float.MaxValue;
            }

            _bfsQueue.Clear();

            NativeArray<NavigationAttractor> attractors = _attractorQuery.ToComponentDataArray<NavigationAttractor>(state.WorldUpdateAllocator);
            NativeArray<LocalTransform> transforms = _attractorQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);

            for (int i = 0; i < attractors.Length; i++)
            {
                int2 cell = FlowFieldUtil.WorldToCell(transforms[i].Position.xy, ArenaConfig.FlowFieldCellSize,
                    _gridWidth, _gridHeight);
                cell = math.clamp(cell, int2.zero, new int2(_gridWidth - 1, _gridHeight - 1));
                int idx = cell.y * _gridWidth + cell.x;
                _distances[idx] = 0;
                _attractorOwner[idx] = transforms[i].Position.xy;
                _bfsQueue.Enqueue(cell);
            }

            new FlowFieldJob
            {
                Queue = _bfsQueue,
                Distances = _distances,
                Blocked = _blocked,
                Directions = _directions,
                AttractorOwner = _attractorOwner,
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
                Attractors = attractors,
                AttractorTransforms = transforms,
                CellSize = ArenaConfig.FlowFieldCellSize,
            }.Run();
        }
    }
}