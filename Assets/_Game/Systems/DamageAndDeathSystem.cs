using _Game.Components;
using _Game.Core;
using _Game.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

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
        private ComponentLookup<Despawn> _despawnLookup;

        public void OnCreate(ref SystemState state)
        {
            _queue = new NativeQueue<DamageEvent>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new DamageEvents { Queue = _queue });

            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
            _despawnLookup = state.GetComponentLookup<Despawn>(isReadOnly: false);
        }

        public void OnDestroy(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            
            if (_queue.IsCreated) _queue.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _despawnLookup.Update(ref state);

            SystemAPI.GetSingletonRW<DamageEvents>();

            bool hasHitQueue = DotsBridge.HitQueue.IsCreated;

            state.Dependency = new ApplyDamageJob
            {
                Queue = _queue,
                HealthLookup = _healthLookup,
                DespawnLookup = _despawnLookup,
                HasHitQueue = hasHitQueue,
                HitQueue = hasHitQueue ? DotsBridge.HitQueue.AsParallelWriter() : default,
            }.Schedule(JobHandle.CombineDependencies(state.Dependency, DotsBridge.BridgeDependencies));

            DotsBridge.BridgeDependencies = JobHandle.CombineDependencies(DotsBridge.BridgeDependencies, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyDamageJob : IJob
        {
            public NativeQueue<DamageEvent> Queue;
            public ComponentLookup<Health> HealthLookup;
            public ComponentLookup<Despawn> DespawnLookup;

            public bool HasHitQueue;
            [WriteOnly] public NativeQueue<HitEvent>.ParallelWriter HitQueue;

            public void Execute()
            {
                while (Queue.TryDequeue(out DamageEvent damage))
                {
                    if (!HealthLookup.HasComponent(damage.Target)) continue;

                    ref Health health = ref HealthLookup.GetRefRW(damage.Target).ValueRW;

                    // Already dead or despawning: no second death, no rewards, no hit stats
                    if (health.Current <= 0f) continue;
                    bool hasDespawn = DespawnLookup.HasComponent(damage.Target);
                    if (hasDespawn && DespawnLookup.IsComponentEnabled(damage.Target)) continue;

                    // TODO: Capture damage by enemies (or enemy type), e.g. contact damage or with weapons.
                    if (HasHitQueue && damage.Weapon != Entity.Null)
                    {
                        HitQueue.Enqueue(new HitEvent
                        {
                            Position = damage.Position,
                            Damage = damage.Value,
                            WeaponEntity = damage.Weapon,
                        });
                    }

                    health.Current -= damage.Value;
                    if (health.Current > 0f || !hasDespawn) continue;

                    DespawnLookup[damage.Target] = new Despawn { Killed = true };
                    DespawnLookup.SetComponentEnabled(damage.Target, true);
                }
            }
        }
    }
}
