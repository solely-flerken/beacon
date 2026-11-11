using _Game.Components;
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
    [UpdateAfter(typeof(ProjectileSystem))]
    [UpdateAfter(typeof(BeamSystem))]
    [UpdateAfter(typeof(MortarSystem))]
    [UpdateAfter(typeof(BeaconArrivalSystem))]
    public partial struct DamageAndDeathSystem : ISystem
    {
        private NativeQueue<DamageEvent> _queue;
        
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<Armor> _armorLookup;
        private ComponentLookup<AttackStats> _attackStatsLookup;
        private ComponentLookup<Despawn> _despawnLookup;
        private ComponentLookup<HitFlashTime> _hitFlashLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<WeaponTypeRef> _weaponTypeLookup;

        private uint _frame;

        public void OnCreate(ref SystemState state)
        {
            _queue = new NativeQueue<DamageEvent>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new DamageEvents { Queue = _queue });

            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
            _armorLookup = state.GetComponentLookup<Armor>(isReadOnly: true);
            _attackStatsLookup = state.GetComponentLookup<AttackStats>(isReadOnly: true);
            _despawnLookup = state.GetComponentLookup<Despawn>(isReadOnly: false);
            _hitFlashLookup = state.GetComponentLookup<HitFlashTime>(isReadOnly: false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
            _weaponTypeLookup = state.GetComponentLookup<WeaponTypeRef>(isReadOnly: true);
        }

        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            
            if (_queue.IsCreated) _queue.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _attackStatsLookup.Update(ref state);
            _despawnLookup.Update(ref state);
            _hitFlashLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _weaponTypeLookup.Update(ref state);

            SystemAPI.GetSingletonRW<DamageEvents>();

            bool hasHitQueue = DotsBridge.HitQueue.IsCreated;
            bool hasSparks = DotsBridge.SparkList.IsCreated;
            _frame++;

            state.Dependency = new ApplyDamageJob
            {
                Queue = _queue,
                HealthLookup = _healthLookup,
                ArmorLookup = _armorLookup,
                AttackStatsLookup = _attackStatsLookup,
                DespawnLookup = _despawnLookup,
                HitFlashLookup = _hitFlashLookup,
                TransformLookup = _transformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                WeaponTypeLookup = _weaponTypeLookup,
                Now = (float)SystemAPI.Time.ElapsedTime,
                HasHitQueue = hasHitQueue,
                HitQueue = hasHitQueue ? DotsBridge.HitQueue.AsParallelWriter() : default,
                HasSparks = hasSparks,
                Sparks = hasSparks ? DotsBridge.SparkList : default,
                SeedBase = _frame * 65599u,
            }.Schedule(JobHandle.CombineDependencies(state.Dependency, DotsBridge.BridgeDependencies));

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyDamageJob : IJob
        {
            public NativeQueue<DamageEvent> Queue;
            public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<Armor> ArmorLookup;
            [ReadOnly] public ComponentLookup<AttackStats> AttackStatsLookup;
            public ComponentLookup<Despawn> DespawnLookup;
            public ComponentLookup<HitFlashTime> HitFlashLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<WeaponTypeRef> WeaponTypeLookup;
            public float Now;

            public bool HasHitQueue;
            [WriteOnly] public NativeQueue<HitEvent>.ParallelWriter HitQueue;

            public bool HasSparks;
            public NativeList<HitSparkData> Sparks;
            public uint SeedBase;

            public void Execute()
            {
                // Spark hits left this frame; once spent, the hot loop skips the spark lookups entirely
                int sparkBudget = HasSparks ? Sparks.Capacity - Sparks.Length : 0;

                Entity lastSource = Entity.Null;
                AttackStats attackStats = default;

                while (Queue.TryDequeue(out DamageEvent damage))
                {
                    if (!HealthLookup.HasComponent(damage.Target)) continue;

                    ref Health health = ref HealthLookup.GetRefRW(damage.Target).ValueRW;

                    // Already dead or despawning: no second death, no rewards, no hit stats
                    if (health.Current <= 0f) continue;
                    bool hasDespawn = DespawnLookup.HasComponent(damage.Target);
                    if (hasDespawn && DespawnLookup.IsComponentEnabled(damage.Target)) continue;

                    // The attacker's stats.
                    if (damage.Source != lastSource)
                    {
                        lastSource = damage.Source;
                        AttackStatsLookup.TryGetComponent(lastSource, out attackStats);
                    }

                    // Reduce damage based on armor, minus the attacker's penetration
                    if (ArmorLookup.TryGetComponent(damage.Target, out Armor armor))
                    {
                        damage.Value = DamageMath.Mitigate(damage.Value, armor.Value, attackStats);
                    }

                    if (HitFlashLookup.TryGetRefRW(damage.Target, out RefRW<HitFlashTime> flash))
                    {
                        flash.ValueRW.Value = Now;
                    }

                    // TODO: Capture damage by enemies (or enemy type), e.g. contact damage or with weapons.
                    if (damage.Weapon != Entity.Null)
                    {
                        if (HasHitQueue)
                        {
                            HitQueue.Enqueue(new HitEvent
                            {
                                Position = damage.Position,
                                Damage = damage.Value,
                                WeaponEntity = damage.Weapon,
                            });
                        }

                        if (sparkBudget > 0 && TryAddSpark(damage))
                        {
                            sparkBudget--;
                        }
                    }

                    health.Current -= damage.Value;
                    if (health.Current > 0f || !hasDespawn) continue;

                    DespawnLookup[damage.Target] = new Despawn { Killed = true };
                    DespawnLookup.SetComponentEnabled(damage.Target, true);
                }
            }

            /// <summary>
            /// Records where the hit came from. Per-weapon style decides how the sparks fly.
            /// Source of the hit:
            /// - Mortar: the blast center.
            /// - Projectile Weapons: the firing weapon's position.
            /// - Beam, Chain Lighting: hit position itself (usually enemy pos)
            /// </summary>
            private bool TryAddSpark(in DamageEvent damage)
            {
                if (!TransformLookup.TryGetComponent(damage.Target, out LocalTransform target)) return false;

                WeaponType weaponType = WeaponTypeLookup.TryGetComponent(damage.Weapon, out WeaponTypeRef typeRef)
                    ? typeRef.WeaponType
                    : WeaponType.Basic;

                float2 source = damage.Position.xy;
                if (weaponType != WeaponType.Mortar && LocalToWorldLookup.TryGetComponent(damage.Weapon, out LocalToWorld weapon))
                {
                    source = weapon.Position.xy;
                }

                uint seed = SeedBase + (uint)Sparks.Length;
                float2 center = target.Position.xy;
                float2 dir = center - source;
                float lenSq = math.lengthsq(dir);

                if (lenSq > 1e-6f)
                {
                    dir *= math.rsqrt(lenSq);
                }
                else
                {
                    math.sincos(seed * 2.3999632f, out float s, out float c);
                    dir = new float2(c, s);
                }

                Sparks.AddNoResize(new HitSparkData
                {
                    Center = center,
                    Direction = dir,
                    Radius = target.Scale * 0.5f,
                    StartTime = Now,
                    Seed = seed,
                    Style = (uint)weaponType,
                });
                
                return true;
            }
        }
    }
}
