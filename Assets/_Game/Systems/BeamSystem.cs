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
                    facing.ValueRW.WindowOpen = false;
                    continue;
                }

                float2 weaponPos = transform.ValueRO.Position.xy;
                float2 desired = math.normalize(targeting.ValueRO.TargetPosition - weaponPos);
                facing.ValueRW.Direction = RotateToward(facing.ValueRO.Direction, desired, beamStats.ValueRO.RotationSpeed * deltaTime);

                SystemAPI.SetComponentEnabled<BeamActive>(entity, true);
                
                BeamVisualShared.Beams.Add(new BeamVisualData
                {
                    Origin = transform.ValueRO.Position,
                    End = new float3(weaponPos + facing.ValueRO.Direction * stats.ValueRO.TargetRange, 0f),
                    Width = beamStats.ValueRO.Width,
                });
            }

            // Damage ticks
            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;
            if (!grids.EnemiesValid) return;

            state.Dependency = new BeamDamageJob
            {
                SpatialGrid = grids.Enemies,
                EnemyHitRadius = 0.5f,
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
        private const float MaxWindowSeconds = 0.08f;
        private const int MaxArcSegments = 8;

        [ReadOnly] public SpatialGrid SpatialGrid;
        [ReadOnly] public float EnemyHitRadius;

        [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;

        private void Execute(
            Entity beamEntity,
            ref BeamFacing facing,
            EnabledRefRW<ReadyToFire> readyToFire,
            in WeaponState weaponState,
            in WeaponStats stats,
            in BeamStats beamStats,
            in LocalToWorld transform)
        {
            float2 toDir = facing.Direction;
            if (math.lengthsq(toDir) < 0.001f) return;
            if (stats.FireRate <= 0f) return;

            float interval = 1f / stats.FireRate;
            float armTime = interval - math.min(MaxWindowSeconds, interval);

            if (!readyToFire.ValueRO)
            {
                // Open the window: the pulse will damage what the beam sweeps from here on
                if (!facing.WindowOpen && weaponState.Timer >= armTime)
                {
                    facing.WindowStartDirection = toDir;
                    facing.WindowOpen = true;
                }
                return;
            }

            readyToFire.ValueRW = false;

            float2 fromDir = facing.WindowOpen ? facing.WindowStartDirection : toDir;

            // Next window: opens right away when windows overlap, otherwise later
            facing.WindowOpen = weaponState.Timer >= armTime;
            facing.WindowStartDirection = toDir;

            float2 origin = transform.Position.xy;
            float range = stats.TargetRange;
            float hitRadius = beamStats.Width * 0.5f + EnemyHitRadius;
            float sweep = math.atan2(Cross(fromDir, toDir), math.dot(fromDir, toDir));
            float pulseDamage = stats.Damage;
            float hitDiameter = 2f * hitRadius;
            float absSweep = math.abs(sweep);

            Slice slice = new()
            {
                Origin = origin,
                FromDir = fromDir,
                ToDir = toDir,
                FromEnd = origin + fromDir * range,
                ToEnd = origin + toDir * range,
                CounterClockwise = sweep >= 0f,
                HitRadiusSq = hitRadius * hitRadius,
                MaxDistSq = (range + hitRadius) * (range + hitRadius),
            };

            FixedList128Bytes<float2> outline = BuildOutline(origin, fromDir, sweep, range);

            float2 boundsMin = origin;
            float2 boundsMax = origin;
            for (int k = 0; k < outline.Length; k++)
            {
                boundsMin = math.min(boundsMin, outline[k]);
                boundsMax = math.max(boundsMax, outline[k]);
            }
            SpatialGrid.GetCellBounds(boundsMin - hitRadius, boundsMax + hitRadius, out int2 minCell, out int2 maxCell);

            for (int row = minCell.y; row <= maxCell.y; row++)
            {
                // Edge rows also hold units clamped in from outside the grid, so their band reaches to infinity
                float bandMin = row == 0 ? float.NegativeInfinity : SpatialGrid.Origin.y + row * SpatialGrid.CellSize - hitRadius;
                float bandMax = row == SpatialGrid.Height - 1 ? float.PositiveInfinity : SpatialGrid.Origin.y + (row + 1) * SpatialGrid.CellSize + hitRadius;
                if (!RowSpan(outline, bandMin, bandMax, out float xMin, out float xMax)) continue;

                int x0 = math.clamp((int)math.floor((xMin - hitRadius - SpatialGrid.Origin.x) * SpatialGrid.InvCellSize), 0, SpatialGrid.Width - 1);
                int x1 = math.clamp((int)math.floor((xMax + hitRadius - SpatialGrid.Origin.x) * SpatialGrid.InvCellSize), 0, SpatialGrid.Width - 1);

                foreach (int i in SpatialGrid.Query(new int2(x0, row), new int2(x1, row)))
                {
                    float2 pos = SpatialGrid.Positions[i];
                    if (!slice.Contains(pos)) continue;

                    // Fraction of the window the beam covered this enemy: full while the beam rests on it, less when swept past.
                    float sweptArc = math.distance(pos, origin) * absSweep;
                    float coverage = math.min(1f, hitDiameter / math.max(sweptArc, 1e-4f));

                    DamageQueue.Enqueue(new DamageEvent
                    {
                        Target = SpatialGrid.Entities[i],
                        Value = pulseDamage * coverage,
                        Weapon = beamEntity,
                        Position = new float3(pos, 0f),
                    });
                }
            }
        }

        /// <summary>
        /// Convex polygon that contains the sector
        /// </summary>
        private static FixedList128Bytes<float2> BuildOutline(float2 origin, float2 fromDir, float sweep, float range)
        {
            int segments = math.clamp((int)math.ceil(math.abs(sweep) / (math.PI / MaxArcSegments)), 1, MaxArcSegments);
            float step = sweep / segments;
            float cornerRadius = range / math.cos(step * 0.5f);
            float fromAngle = math.atan2(fromDir.y, fromDir.x);

            FixedList128Bytes<float2> outline = default;
            outline.Add(origin);
            outline.Add(origin + fromDir * range);
            for (int k = 0; k < segments; k++)
            {
                math.sincos(fromAngle + (k + 0.5f) * step, out float s, out float c);
                outline.Add(origin + new float2(c, s) * cornerRadius);
            }

            math.sincos(fromAngle + sweep, out float sEnd, out float cEnd);
            outline.Add(origin + new float2(cEnd, sEnd) * range);
            return outline;
        }

        /// <summary>
        /// Finds how far left and right the slice reaches within one grid row, so the scan only visits the cells of that row the slice covers.
        /// </summary>
        private static bool RowSpan(in FixedList128Bytes<float2> outline, float yMin, float yMax, out float xMin, out float xMax)
        {
            xMin = float.MaxValue;
            xMax = float.MinValue;

            for (int k = 0; k < outline.Length; k++)
            {
                float2 a = outline[k];
                float2 b = outline[(k + 1) % outline.Length];
                float2 low = a.y <= b.y ? a : b;
                float2 high = a.y <= b.y ? b : a;
                if (high.y < yMin || low.y > yMax) continue;

                float2 p0 = low;
                float2 p1 = high;
                float dy = high.y - low.y;
                if (dy > 1e-6f)
                {
                    if (low.y < yMin) p0 = math.lerp(low, high, (yMin - low.y) / dy);
                    if (high.y > yMax) p1 = math.lerp(low, high, (yMax - low.y) / dy);
                }

                xMin = math.min(xMin, math.min(p0.x, p1.x));
                xMax = math.max(xMax, math.max(p0.x, p1.x));
            }

            return xMin <= xMax;
        }

        private static float Cross(float2 a, float2 b)
        {
            return a.x * b.y - a.y * b.x;
        } 

        /// <summary>
        /// The area a beam swept since its last tick: a sector of the beam's range from FromDir to ToDir, widened by the hit radius.
        /// </summary>
        private struct Slice
        {
            public float2 Origin;
            public float2 FromDir;
            public float2 ToDir;
            public float2 FromEnd;
            public float2 ToEnd;
            public bool CounterClockwise;
            public float HitRadiusSq;
            public float MaxDistSq;

            public bool Contains(float2 pos)
            {
                float2 offset = pos - Origin;
                if (math.lengthsq(offset) > MaxDistSq) return false;

                // Inside the sector between the two directions
                float fromSide = Cross(FromDir, offset);
                float toSide = Cross(offset, ToDir);
                bool inSector = CounterClockwise ? fromSide >= 0f && toSide >= 0f : fromSide <= 0f && toSide <= 0f;
                if (inSector) return true;

                return DistToSegmentSq(pos, Origin, FromEnd) <= HitRadiusSq || DistToSegmentSq(pos, Origin, ToEnd) <= HitRadiusSq;
            }
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
