using _Game.Components;
using _Game.Config;
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
    /// Reads the FlowField (direction) and SpatialGrids (neighbors) singletons.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialGridSystem))]
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<FlowField>();
        }

        public void OnUpdate(ref SystemState state)
        {
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            FlowField flowField = SystemAPI.GetSingleton<FlowField>();
            if (!grids.EnemiesValid || !flowField.IsValid) return;

            ArenaBounds bounds = SystemAPI.GetSingleton<ArenaBounds>();

            new MoveEnemyJob
            {
                FlowFieldDirections = flowField.Directions,
                FlowFieldCellSize = flowField.CellSize,
                FlowFieldGridWidth = flowField.GridWidth,
                FlowFieldGridHeight = flowField.GridHeight,
                NearestAttractorPos = flowField.NearestAttractorPos,
                SpatialGrid = grids.Enemies,
                SeparationRadius = 1.0f,
                SeparationStrength = 6.0f,
                HalfExtents = new float2(bounds.Width * 0.5f, bounds.Height * 0.5f),
                DeltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime),
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    internal partial struct MoveEnemyJob : IJobEntity
    {
        [ReadOnly] public NativeArray<float2> FlowFieldDirections;
        [ReadOnly] public float FlowFieldCellSize;
        [ReadOnly] public int FlowFieldGridWidth;
        [ReadOnly] public int FlowFieldGridHeight;
        [ReadOnly] public NativeArray<float2> NearestAttractorPos;

        [ReadOnly] public SpatialGrid SpatialGrid;
        [ReadOnly] public float SeparationRadius;
        [ReadOnly] public float SeparationStrength;

        [ReadOnly] public float2 HalfExtents;
        [ReadOnly] public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref Steering steering, in Speed speed)
        {
            float2 pos = transform.Position.xy;
            float2 desired = FlowSteering(pos, speed.Value) + SeparationSteering(pos);

            if (speed.Value <= 0f)
            {
                steering.Velocity = desired;
            }
            else
            {
                steering.Ease(desired, DeltaTime);
            }

            float2 nextPos = math.clamp(pos + steering.Velocity * DeltaTime, -HalfExtents, HalfExtents);
            transform.Position = new float3(nextPos, 0f);
        }

        private float2 FlowSteering(float2 pos, float speed)
        {
            float2 dir = FlowFieldUtil.Sample(FlowFieldDirections, pos, FlowFieldCellSize, FlowFieldGridWidth, FlowFieldGridHeight);

            if (math.lengthsq(dir) > 0.001f) return dir * speed;

            // no direction, since already in the cell - look up the owning attractor for this cell
            int2 cell = FlowFieldUtil.WorldToCell(pos, FlowFieldCellSize, FlowFieldGridWidth, FlowFieldGridHeight);
            int idx = FlowFieldUtil.GetIndex(cell, FlowFieldGridWidth);
            
            float2 ownerPos = NearestAttractorPos[idx];
            float2 toOwner = ownerPos - pos;
            float dist = math.lengthsq(toOwner);
            return dist > 0.001f ? math.normalize(toOwner) * speed : float2.zero;
        }

        private float2 SeparationSteering(float2 pos)
        {
            float2 separation = float2.zero;
            float radiusSq = SeparationRadius * SeparationRadius;

            foreach (int i in SpatialGrid.QueryNeighbors(pos, 1))
            {
                float2 diff = pos - SpatialGrid.Positions[i];
                float distSq = math.lengthsq(diff);
                
                if (distSq < 0.0001f || distSq >= radiusSq) continue;

                float dist = math.sqrt(distSq);
                separation += (diff / dist) * (1f - dist / SeparationRadius);
            }

            return separation * SeparationStrength;
        }
    }
}