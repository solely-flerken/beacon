using _Game.Components;
using _Game.Core;
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
    [UpdateAfter(typeof(WeaponTargetingSystem))]
    public partial struct BeamSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeamTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update beam direction and shared data for beam visuals
            foreach ((RefRW<BeamFacing> facing, RefRO<WeaponTargeting> targeting, RefRO<WeaponStats> stats, RefRO<BeamStats> beamStats, RefRO<LocalToWorld> transform, Entity entity) in
                     SystemAPI.Query<RefRW<BeamFacing>, RefRO<WeaponTargeting>, RefRO<WeaponStats>, RefRO<BeamStats>, RefRO<LocalToWorld>>().WithAll<BeamTag>().WithEntityAccess())
            {
                if (!targeting.ValueRO.HasTarget)
                {
                    SystemAPI.SetComponentEnabled<BeamActive>(entity, false);
                    continue;
                }

                float2 weaponPos = transform.ValueRO.Position.xy;
                float2 desired = math.normalize(targeting.ValueRO.TargetPosition - weaponPos);
                facing.ValueRW.Direction = RotateToward(facing.ValueRO.Direction, desired, beamStats.ValueRO.RotationSpeed * deltaTime);

                SystemAPI.SetComponentEnabled<BeamActive>(entity, true);
            }

            // Handle collisions
            if (!SpatialGridShared.EnemyGridValid) return;

            bool hasHitQueue = DotsBridge.HitQueue.IsCreated;
            NativeQueue<HitEvent>.ParallelWriter hitQueueWriter = default;
            if (hasHitQueue) hitQueueWriter = DotsBridge.HitQueue.AsParallelWriter();
            
            EntityCommandBuffer.ParallelWriter ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            state.Dependency = new BeamDamageJob
            {
                TargetPositions = EnemyDataShared.Positions,
                TargetEntities = EnemyDataShared.Entities,
                SpatialGrid = SpatialGridShared.EnemyCellMap,
                SpatialGridCellSize = SpatialGridShared.CellSize,
                HitCooldowns = SystemAPI.GetBufferLookup<HitCooldown>(true),
                ECB = ecb,
                CurrentTime = (float)SystemAPI.Time.ElapsedTime,
                HasHitQueue = hasHitQueue,
                HitQueue = hitQueueWriter
            }.ScheduleParallel(state.Dependency);

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        private static float2 RotateToward(float2 current, float2 target, float maxRadians)
        {
            float currentAngle = math.atan2(current.y, current.x);
            float targetAngle = math.atan2(target.y, target.x);
            float delta = targetAngle - currentAngle;
            delta = math.atan2(math.sin(delta), math.cos(delta));
            float rotated = currentAngle + math.clamp(delta, -maxRadians, maxRadians);
            math.sincos(rotated, out float s, out float c);
            return new float2(c, s);
        }
    }

    [BurstCompile]
    [WithAll(typeof(BeamActive))]
    internal partial struct BeamDamageJob : IJobEntity
    {
        private static readonly int2[] NeighbourOffsets =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1, 0), new(0, 0), new(1, 0),
            new(-1, 1), new(0, 1), new(1, 1),
        };

        [ReadOnly] public NativeArray<float2> TargetPositions;
        [ReadOnly] public NativeArray<Entity> TargetEntities;
        [ReadOnly] public NativeParallelMultiHashMap<int2, int> SpatialGrid;
        [ReadOnly] public float SpatialGridCellSize;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;

        public EntityCommandBuffer.ParallelWriter ECB;
        public float CurrentTime;

        public bool HasHitQueue;
        [WriteOnly] public NativeQueue<HitEvent>.ParallelWriter HitQueue;
        
        private void Execute(
            Entity beamEntity,
            [EntityIndexInQuery] int sortKey,
            in BeamFacing facing,
            in WeaponStats stats,
            in BeamStats beamStats,
            in LocalToWorld transform)
        {
            float2 start = transform.Position.xy;
            float2 dir = facing.Direction;

            if (math.lengthsq(dir) < 0.001f) return;

            float range = stats.TargetRange;
            float2 end = start + dir * range;
            float halfWidth = beamStats.Width * 0.5f;
            float halfWidthSq = halfWidth * halfWidth;
            float tickInterval = 1f / stats.FireRate;

            float stepSize = SpatialGridCellSize * 0.5f;
            int numSteps = (int)math.ceil(range / stepSize) + 1;

            NativeHashSet<int> hitSet = new(16, Allocator.Temp);

            for (int step = 0; step <= numSteps; step++)
            {
                float2 sample = start + dir * math.min(step * stepSize, range);
                int2 cell = new((int)math.floor(sample.x / SpatialGridCellSize), (int)math.floor(sample.y / SpatialGridCellSize));

                foreach (int2 offset in NeighbourOffsets)
                {
                    if (!SpatialGrid.TryGetFirstValue(cell + offset, out int idx, out NativeParallelMultiHashMapIterator<int2> it)) continue;

                    do
                    {
                        Entity target = TargetEntities[idx];
                        
                        if (hitSet.Contains(idx)) continue;
                        if (DistToSegmentSq(TargetPositions[idx], start, end) > halfWidthSq) continue;
                        if (IsOnCooldown(target, beamEntity)) continue;

                        hitSet.Add(idx);

                        ECB.AppendToBuffer(sortKey, target, new HitCooldown { Source = beamEntity, Expiry = CurrentTime + tickInterval });
                        ECB.AppendToBuffer(sortKey, target, new DamageBufferElement { Value = stats.Damage });

                        if (HasHitQueue)
                        {
                            HitQueue.Enqueue(new HitEvent
                            {
                                Position = new float3(TargetPositions[idx], 0f),
                                Damage = stats.Damage,
                            });
                        }
                    } while (SpatialGrid.TryGetNextValue(out idx, ref it));
                }
            }

            hitSet.Dispose();
        }

        private bool IsOnCooldown(Entity target, Entity source)
        {
            if (!HitCooldowns.HasBuffer(target))
            {
                return false;
            }
            
            DynamicBuffer<HitCooldown> cds = HitCooldowns[target];
            for (int i = 0; i < cds.Length; i++)
            {
                if (cds[i].Source == source) 
                    return true;    
            }
            
            return false;
        }

        /// <summary>
        /// Returns the squared distance from <paramref name="point"/> to the nearest point on the segment
        /// [<paramref name="segStart"/>, <paramref name="segEnd"/>].
        /// </summary>
        private static float DistToSegmentSq(float2 point, float2 segStart, float2 segEnd)
        {
            float2 seg = segEnd - segStart;
            float segLenSq = math.dot(seg, seg);
            if (segLenSq < 0.0001f) return math.distancesq(point, segStart);
            float t = math.clamp(math.dot(point - segStart, seg) / segLenSq, 0f, 1f);
            return math.distancesq(point, segStart + t * seg);
        }
    }
}