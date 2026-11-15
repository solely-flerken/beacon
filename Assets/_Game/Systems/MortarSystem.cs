using _Game.Components;
using _Game.Config;
using _Game.Shared;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MortarLaunchSystem))]
    [UpdateAfter(typeof(SpatialGridSystem))]
    [BurstCompile]
    public partial struct MortarSystem : ISystem
    {
        private ComponentLookup<MortarStats> _mortarStatsLookup;
        private ComponentLookup<WeaponStats> _weaponStatsLookup;
        private ComponentLookup<MortarTrajectory> _trajectoryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MortarShellTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SpatialGrids>();
            state.RequireForUpdate<DamageEvents>();

            _mortarStatsLookup = state.GetComponentLookup<MortarStats>(isReadOnly: true);
            _weaponStatsLookup = state.GetComponentLookup<WeaponStats>(isReadOnly: true);
            _trajectoryLookup = state.GetComponentLookup<MortarTrajectory>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _mortarStatsLookup.Update(ref state);
            _weaponStatsLookup.Update(ref state);
            _trajectoryLookup.Update(ref state);

            float deltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime);

            SpatialGrids grids = SystemAPI.GetSingleton<SpatialGrids>();
            NativeQueue<DamageEvent> damageQueue = SystemAPI.GetSingletonRW<DamageEvents>().ValueRO.Queue;

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new MortarUpdateJob
            {
                DeltaTime = deltaTime,
                ECB = ecb,
                
                PayloadLookup = _mortarStatsLookup,
                WeaponStatsLookup = _weaponStatsLookup,
                
                Grids = grids,

                DamageQueue = damageQueue.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new MortarShadowJob
            {
                ECB = ecb,
                TrajectoryLookup = _trajectoryLookup,
            }.ScheduleParallel(state.Dependency);
        }

        internal const float ShellGroundScale = 0.6f;
        private const float ShellApexScale = 1.4f;
        internal const float ShadowGroundScale = 0.8f;
        private const float ShadowApexScale = 2.5f;
        private const float ShadowGroundBrightness = 0.5f;
        private const float ShadowApexBrightness = 0.12f;

        /// <summary>
        /// Constant ground speed along a parabola. heightFraction is 0 at launch and impact, 1 at the apex.
        /// </summary>
        private static void EvaluateFlight(in MortarTrajectory flight, out float progress, out float2 groundPos, out float heightFraction)
        {
            progress = flight.Duration > 0f ? math.saturate(flight.Elapsed / flight.Duration) : 1f;
            groundPos = math.lerp(flight.StartPos, flight.TargetPos, progress);
            heightFraction = 4f * progress * (1f - progress);
        }

        [BurstCompile]
        public partial struct MortarUpdateJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly] public ComponentLookup<MortarStats> PayloadLookup;
            [ReadOnly] public ComponentLookup<WeaponStats> WeaponStatsLookup;

            [ReadOnly] public SpatialGrids Grids;

            [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;

            public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref LocalTransform transform, ref MortarTrajectory flight, in ProjectileSource source)
            {
                flight.Elapsed += DeltaTime;
                EvaluateFlight(flight, out float progress, out float2 groundPos, out float heightFraction);
                transform.Position = new float3(groundPos.x, groundPos.y + heightFraction * flight.ArcHeight, 0f);
                transform.Scale = math.lerp(ShellGroundScale, ShellApexScale, heightFraction);

                if (progress >= 1f)
                {
                    HandleImpact(entity, sortKey, groundPos, flight, source);
                }
            }

            private void HandleImpact(Entity entity, int sortKey, float2 impactPos, in MortarTrajectory flight, in ProjectileSource source)
            {
                ECB.DestroyEntity(sortKey, entity);

                if (!PayloadLookup.TryGetComponent(source.Weapon,out MortarStats mortarStats)) return;
                if (!WeaponStatsLookup.TryGetComponent(source.Weapon,out WeaponStats baseStats)) return;

                SpawnExplosion(sortKey, impactPos, mortarStats.ExplosionRadius, flight.ExplosionPrefab);
                
                for (int faction = 0; faction < SpatialGrids.FactionCount; faction++)
                {
                    if (!Grids.Has(source.Rules.TargetFactions, faction)) continue;

                    ApplyRadiusDamage(Grids[faction], impactPos, baseStats.Damage, mortarStats.ExplosionRadius, source);
                }
            }

            private void SpawnExplosion(int sortKey, float2 pos, float radius, Entity prefab)
            {
                float scale = radius * 2f;
                
                LocalTransform spawnTransform = LocalTransform.FromPositionRotationScale(new float3(pos, 0f), quaternion.identity, scale);
                Entity explosion = ECB.Instantiate(sortKey, prefab);
                ECB.SetComponent(sortKey, explosion, spawnTransform);
                ECB.SetComponent(sortKey, explosion, new LocalToWorld { Value = spawnTransform.ToMatrix() });
                ECB.SetComponent(sortKey, explosion, new ExplosionState
                {
                    Duration = 0.4f,
                    StartScale = scale,
                    Elapsed = 0f,
                });
            }

            private void ApplyRadiusDamage(in SpatialGrid grid, float2 center, float damage, float radius, in ProjectileSource source)
            {
                float radiusSq = radius * radius;
                
                foreach (int i in grid.Query(center - radius, center + radius))
                {
                    if (math.distancesq(grid.Positions[i], center) > radiusSq) continue;
                    if (grid.Entities[i] == source.Owner) continue;

                    DamageQueue.Enqueue(new DamageEvent
                    {
                        Target = grid.Entities[i],
                        Value = damage,
                        Source = source.Weapon,
                        ReportHit = source.Rules.ReportHits,
                        Position = new float3(center, 0f),
                    });
                }
            }
        }

        /// <summary>
        /// Places each shadow under its shell on the ground.
        /// Destroys the shadow in the shell's impact frame, or if the shell is already gone.
        /// </summary>
        [BurstCompile]
        public partial struct MortarShadowJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<MortarTrajectory> TrajectoryLookup;

            public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref LocalTransform transform, ref URPMaterialPropertyBaseColor color, in MortarShadow shadow)
            {
                if (!TrajectoryLookup.TryGetComponent(shadow.Shell, out MortarTrajectory flight))
                {
                    ECB.DestroyEntity(sortKey, entity);
                    return;
                }

                EvaluateFlight(flight, out float progress, out float2 groundPos, out float heightFraction);
                if (progress >= 1f)
                {
                    ECB.DestroyEntity(sortKey, entity);
                    return;
                }

                float shadowScale = math.lerp(ShadowGroundScale, ShadowApexScale, heightFraction);
                transform = LocalTransform.FromPositionRotationScale(new float3(groundPos, 0f), quaternion.identity, shadowScale);
                color.Value.w = math.lerp(ShadowGroundBrightness, ShadowApexBrightness, heightFraction);
            }
        }
    }
}
