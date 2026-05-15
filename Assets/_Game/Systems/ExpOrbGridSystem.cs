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
    [UpdateAfter(typeof(ExpOrbSpawnSystem))]
    [UpdateBefore(typeof(ExpOrbMergeSystem))]
    public partial struct ExpOrbGridSystem : ISystem
    {
        private const int MaxOrbs = 8192;

        private NativeArray<Entity> _entities;
        private NativeArray<float2> _positions;
        private NativeArray<ExpOrbData> _orbData;
        private NativeParallelMultiHashMap<int2, int> _cellMap;

        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExpOrbConfig>();

            _entities = new NativeArray<Entity>(MaxOrbs, Allocator.Persistent);
            _positions = new NativeArray<float2>(MaxOrbs, Allocator.Persistent);
            _orbData = new NativeArray<ExpOrbData>(MaxOrbs, Allocator.Persistent);
            _cellMap = new NativeParallelMultiHashMap<int2, int>(MaxOrbs, Allocator.Persistent);

            ExpOrbGridShared.Entities = _entities;
            ExpOrbGridShared.Positions = _positions;
            ExpOrbGridShared.OrbData = _orbData;
            ExpOrbGridShared.CellMap = _cellMap;

            _query = state.GetEntityQuery(
                ComponentType.ReadOnly<ExpOrbTag>(),
                ComponentType.ReadOnly<ExpOrbData>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<SaturatedTag>());
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_entities.IsCreated) _entities.Dispose();
            if (_positions.IsCreated) _positions.Dispose();
            if (_orbData.IsCreated) _orbData.Dispose();
            if (_cellMap.IsCreated) _cellMap.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            float cellSize = SystemAPI.GetSingleton<ExpOrbConfig>().MergeRadius;

            int count = math.min(_query.CalculateEntityCount(), MaxOrbs);
            ExpOrbGridShared.Count = count;

            _cellMap.Clear();

            if (count == 0) return;

            JobHandle copyHandle = new CopyOrbDataJob
            {
                Entities = _entities,
                Positions = _positions,
                OrbData = _orbData,
            }.ScheduleParallel(_query, state.Dependency);

            JobHandle buildHandle = new RebuildOrbGridJob
            {
                Positions = _positions,
                OrbData = _orbData,
                CellSize = cellSize,
                CellMap = _cellMap.AsParallelWriter(),
            }.Schedule(count, 64, copyHandle);

            buildHandle.Complete();
        }
    }

    [BurstCompile]
    internal partial struct CopyOrbDataJob : IJobEntity
    {
        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<Entity> Entities;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float2> Positions;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<ExpOrbData> OrbData;

        private void Execute(Entity entity, [EntityIndexInQuery] int index, in LocalTransform transform, in ExpOrbData data)
        {
            Entities[index] = entity;
            Positions[index] = transform.Position.xy;
            OrbData[index] = data;
        }
    }

    [BurstCompile]
    internal struct RebuildOrbGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<ExpOrbData> OrbData;
        [ReadOnly] public float CellSize;
        [WriteOnly] public NativeParallelMultiHashMap<int2, int>.ParallelWriter CellMap;

        public void Execute(int i)
        {
            if (OrbData[i].State != ExpOrbState.Idle) return;

            float2 pos = Positions[i];
            CellMap.Add(new int2((int)math.floor(pos.x / CellSize), (int)math.floor(pos.y / CellSize)), i);
        }
    }
}