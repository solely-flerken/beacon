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
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeaponTargetingSystem))]
    public partial struct BeamSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeamTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<DamageEvents>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            BeamVisualShared.Beams.Clear();
            float deltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime);

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
                facing.ValueRW.PreviousDirection = facing.ValueRO.Direction;
                facing.ValueRW.Direction = RotateToward(facing.ValueRO.Direction, desired, beamStats.ValueRO.RotationSpeed * deltaTime);

                SystemAPI.SetComponentEnabled<BeamActive>(entity, true);
                
                BeamVisualShared.Beams.Add(new BeamVisualData
                {
                    Origin = transform.ValueRO.Position,
                    End = new float3(weaponPos + facing.ValueRO.Direction * stats.ValueRO.TargetRange, 0f),
                    Width = beamStats.ValueRO.Width,
                });
            }

            // Handle collisions
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;
            if (!grids.EnemiesValid) return;

            EntityCommandBuffer.ParallelWriter ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            state.Dependency = new BeamDamageJob
            {
                SpatialGrid = grids.Enemies,
                EnemyHitRadius = 0.5f,
                HitCooldowns = SystemAPI.GetBufferLookup<HitCooldown>(true),
                ECB = ecb,
                CurrentTime = (float)SystemAPI.Time.ElapsedTime,
                DamageQueue = damageQueue.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);
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
        [ReadOnly] public SpatialGrid SpatialGrid;
        [ReadOnly] public float EnemyHitRadius;
        [ReadOnly] public BufferLookup<HitCooldown> HitCooldowns;

        public EntityCommandBuffer.ParallelWriter ECB;
        public float CurrentTime;

        [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;

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
            float2 prevDir = facing.PreviousDirection;

            if (math.lengthsq(dir) < 0.001f) return;

            float range = stats.TargetRange;
            float2 end = start + dir * range;
            float2 prevEnd = start + prevDir * range;
            float halfWidth = beamStats.Width * 0.5f;
            float hitThreshold = halfWidth + EnemyHitRadius;
            float hitThresholdSq = hitThreshold * hitThreshold;
            float tickInterval = 1f / stats.FireRate;

            float stepSize = SpatialGrid.CellSize * 0.5f;
            int numSteps = (int)math.ceil(range / stepSize) + 1;

            NativeHashSet<int> hitSet = new(16, Allocator.Temp);

            for (int step = 0; step <= numSteps; step++)
            {
                float2 sample = start + dir * math.min(step * stepSize, range);

                foreach (int i in SpatialGrid.QueryNeighbors(sample, 1))
                {
                    float2 pos = SpatialGrid.Positions[i];
                    if (DistToSegmentSq(pos, start, end) > hitThresholdSq && DistToSegmentSq(pos, start, prevEnd) > hitThresholdSq) continue;
                    if (hitSet.Contains(i)) continue;

                    Entity target = SpatialGrid.Entities[i];
                    if (IsOnCooldown(target, beamEntity)) continue;

                    hitSet.Add(i);

                    ECB.AppendToBuffer(sortKey, target, new HitCooldown { Source = beamEntity, Expiry = CurrentTime + tickInterval });
                    DamageQueue.Enqueue(new DamageEvent
                    {
                        Target = target,
                        Value = stats.Damage,
                        Weapon = beamEntity,
                        Position = new float3(pos, 0f),
                    });
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
                if (cds[i].Source == source && CurrentTime < cds[i].Expiry)
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