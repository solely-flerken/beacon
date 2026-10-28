using _Game.Components;
using _Game.Config;
using _Game.Core;
using _Game.Shared;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MortarShellTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            _mortarStatsLookup = state.GetComponentLookup<MortarStats>(isReadOnly: true);
            _weaponStatsLookup = state.GetComponentLookup<WeaponStats>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _mortarStatsLookup.Update(ref state);
            _weaponStatsLookup.Update(ref state);

            float deltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime);

            state.Dependency = JobHandle.CombineDependencies(state.Dependency, DotsBridge.BridgeDependencies, DamageShared.Writers);

            EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new MortarUpdateJob
            {
                DeltaTime = deltaTime,
                Ecb = ecb,
                
                PayloadLookup = _mortarStatsLookup,
                WeaponStatsLookup = _weaponStatsLookup,
                
                EnemyGridValid = SpatialGridShared.EnemyGridValid,
                EnemyGrid = SpatialGridShared.EnemyGrid,
                
                HitQueue = DotsBridge.HitQueue.IsCreated ? DotsBridge.HitQueue.AsParallelWriter() : default,
                DamageQueue = DamageShared.Queue.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
            DamageShared.Writers = state.Dependency;
        }

        [BurstCompile]
        public partial struct MortarUpdateJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly] public ComponentLookup<MortarStats> PayloadLookup;
            [ReadOnly] public ComponentLookup<WeaponStats> WeaponStatsLookup;

            [ReadOnly] public bool EnemyGridValid;
            [ReadOnly] public SpatialGrid EnemyGrid;

            public NativeQueue<HitEvent>.ParallelWriter HitQueue;
            [WriteOnly] public NativeQueue<DamageEvent>.ParallelWriter DamageQueue;

            public void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref LocalTransform transform, ref MortarTrajectory flight, in ProjectileWeapon weapon)
            {
                flight.Elapsed += DeltaTime;
                float progress = flight.Duration > 0f ? math.saturate(flight.Elapsed / flight.Duration) : 1f;

                float2 groundPos = math.lerp(flight.StartPos, flight.TargetPos, progress);
                float arcY = flight.ArcHeight * 4f * progress * (1f - progress);
                transform.Position = new float3(groundPos.x, groundPos.y + arcY, 0f);

                UpdateShadow(sortKey, flight, groundPos, arcY);

                if (progress >= 1f)
                {
                    HandleImpact(entity, sortKey, groundPos, flight, weapon.Value);
                }
            }

            private void UpdateShadow(int sortKey, in MortarTrajectory flight, float2 groundPos, float arcY)
            {
                float arcFraction = flight.ArcHeight > 0f ? arcY / flight.ArcHeight : 0f;
                float shadowScale = math.lerp(1f, 0.2f, arcFraction);
                
                Ecb.SetComponent(sortKey, flight.Shadow, LocalTransform.FromPositionRotationScale(new float3(groundPos, 0f), quaternion.identity, shadowScale));
            }

            private void HandleImpact(Entity entity, int sortKey, float2 impactPos, in MortarTrajectory flight, Entity weaponEntity)
            {
                Ecb.DestroyEntity(sortKey, flight.Shadow);
                Ecb.DestroyEntity(sortKey, entity);

                if (!PayloadLookup.TryGetComponent(weaponEntity, out MortarStats mortarStats)) return;
                if (!WeaponStatsLookup.TryGetComponent(weaponEntity, out WeaponStats baseStats)) return;

                SpawnExplosion(sortKey, impactPos, mortarStats.ExplosionRadius, flight.ExplosionPrefab);

                if (EnemyGridValid)
                {
                    ApplyRadiusDamage(impactPos, baseStats.Damage, mortarStats.ExplosionRadius, weaponEntity);
                }
            }

            private void SpawnExplosion(int sortKey, float2 pos, float radius, Entity prefab)
            {
                float scale = radius * 2f;
                
                LocalTransform spawnTransform = LocalTransform.FromPositionRotationScale(new float3(pos, 0f), quaternion.identity, scale);
                Entity explosion = Ecb.Instantiate(sortKey, prefab);
                Ecb.SetComponent(sortKey, explosion, spawnTransform);
                Ecb.SetComponent(sortKey, explosion, new LocalToWorld { Value = spawnTransform.ToMatrix() });
                Ecb.SetComponent(sortKey, explosion, new ExplosionState
                {
                    Duration = 0.4f,
                    StartScale = scale,
                    Elapsed = 0f,
                });
            }

            private void ApplyRadiusDamage(float2 center, float damage, float radius, Entity weaponEntity)
            {
                float radiusSq = radius * radius;
                
                foreach (int i in EnemyGrid.Query(center - radius, center + radius))
                {
                    if (math.distancesq(EnemyGrid.Positions[i], center) > radiusSq) continue;

                    // Apply Damage
                    DamageQueue.Enqueue(new DamageEvent { Target = EnemyGrid.Entities[i], Value = damage });

                    HitQueue.Enqueue(new HitEvent
                    {
                        Position = new float3(center, 0f),
                        Damage = damage,
                        WeaponEntity = weaponEntity,
                    });
                }
            }
        }
    }
}