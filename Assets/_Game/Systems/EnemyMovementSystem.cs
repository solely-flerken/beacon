using _Game.Components;
using _Game.Shared;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    /// <summary>
    /// Moves all enemy entities each frame.
    /// Reads from EnemySimShared (positions) and SpatialGridShared (neighbors)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialGridSystem))]
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!EnemyDataShared.IsValid || !SpatialGridShared.EnemyGridValid || !FlowFieldShared.IsValid) return;

            new MoveEnemyJob
            {
                Positions = EnemyDataShared.Positions,
                FlowFieldDirections = FlowFieldShared.Directions,
                FlowFieldCellSize = FlowFieldShared.CellSize,
                FlowFieldGridWidth = FlowFieldShared.GridWidth,
                FlowFieldGridHeight = FlowFieldShared.GridHeight,
                AttractorOwner = FlowFieldShared.AttractorOwner,
                SpatialGrid = SpatialGridShared.EnemyCellMap,
                SpatialGridCellSize = SpatialGridShared.CellSize,
                SeparationRadius = 1.0f,
                SeparationStrength = 6.0f,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    internal partial struct MoveEnemyJob : IJobEntity
    {
        [ReadOnly] public NativeArray<float2> Positions;
        
        [ReadOnly] public NativeArray<float2> FlowFieldDirections;
        [ReadOnly] public float FlowFieldCellSize;
        [ReadOnly] public int FlowFieldGridWidth;
        [ReadOnly] public int FlowFieldGridHeight;
        [ReadOnly] public NativeArray<float2> AttractorOwner;

        [ReadOnly] public NativeParallelMultiHashMap<int2, int> SpatialGrid;
        [ReadOnly] public float SpatialGridCellSize;
        [ReadOnly] public float SeparationRadius;
        [ReadOnly] public float SeparationStrength;

        [ReadOnly] public float DeltaTime;

        private void Execute([EntityIndexInQuery] int index, ref LocalTransform transform, in EnemySpeed speed)
        {
            float2 pos = Positions[index];
            float2 moveVec = FlowSteering(pos, speed.Value) + SeparationSteering(pos, index);
            transform.Position = new float3(pos + moveVec * DeltaTime, 0f);
        }

        private float2 FlowSteering(float2 pos, float speed)
        {
            float2 dir = FlowFieldUtil.Sample(FlowFieldDirections, pos, FlowFieldCellSize, FlowFieldGridWidth, FlowFieldGridHeight);

            if (math.lengthsq(dir) > 0.001f) return dir * speed;

            // no direction, since already in the cell - look up the owning attractor for this cell
            int2 cell = FlowFieldUtil.WorldToCell(pos, FlowFieldCellSize, FlowFieldGridWidth, FlowFieldGridHeight);
            int idx = FlowFieldUtil.GetIndex(cell, FlowFieldGridWidth);
            
            float2 ownerPos = AttractorOwner[idx];
            float2 toOwner = ownerPos - pos;
            float dist = math.lengthsq(toOwner);
            return dist > 0.001f ? math.normalize(toOwner) * speed : float2.zero;
        }

        private static readonly int2[] NeighbourCells =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1,  0), new(0,  0), new(1,  0),
            new(-1,  1), new(0,  1), new(1,  1),
        };

        private float2 SeparationSteering(float2 pos, int index)
        {
            float2 separation = float2.zero;
            float radiusSq = SeparationRadius * SeparationRadius;
            int2 cell = new((int)math.floor(pos.x / SpatialGridCellSize), (int)math.floor(pos.y / SpatialGridCellSize));

            foreach (int2 offset in NeighbourCells)
            {
                if (!SpatialGrid.TryGetFirstValue(cell + offset, out int neighbourIdx, out NativeParallelMultiHashMapIterator<int2> it)) continue;
                do
                {
                    if (neighbourIdx == index) continue;

                    float2 diff = pos - Positions[neighbourIdx];
                    float distSq = math.lengthsq(diff);

                    if (distSq < 0.0001f || distSq >= radiusSq) continue;

                    float dist = math.sqrt(distSq);
                    separation += (diff / dist) * (1f - dist / SeparationRadius);
                } while (SpatialGrid.TryGetNextValue(out neighbourIdx, ref it));
            }

            return separation * SeparationStrength;
        }
    }
}