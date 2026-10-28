using _Game.Components;
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
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<IsDead> _isDeadLookup;

        public void OnCreate(ref SystemState state)
        {
            DamageShared.Queue = new NativeQueue<DamageEvent>(Allocator.Persistent);
            DamageShared.Writers = default;

            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
            _isDeadLookup = state.GetComponentLookup<IsDead>(isReadOnly: false);
        }

        public void OnDestroy(ref SystemState state)
        {
            DamageShared.Writers.Complete();
            if (DamageShared.Queue.IsCreated) DamageShared.Queue.Dispose();

            DamageShared.Queue = default;
            DamageShared.Writers = default;
        }

        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _isDeadLookup.Update(ref state);

            state.Dependency = new ApplyDamageJob
            {
                Queue = DamageShared.Queue,
                HealthLookup = _healthLookup,
                IsDeadLookup = _isDeadLookup,
            }.Schedule(JobHandle.CombineDependencies(state.Dependency, DamageShared.Writers));

            DamageShared.Writers = state.Dependency;
        }

        [BurstCompile]
        private struct ApplyDamageJob : IJob
        {
            public NativeQueue<DamageEvent> Queue;
            public ComponentLookup<Health> HealthLookup;
            public ComponentLookup<IsDead> IsDeadLookup;

            public void Execute()
            {
                while (Queue.TryDequeue(out DamageEvent damage))
                {
                    if (!HealthLookup.HasComponent(damage.Target)) continue;

                    ref Health health = ref HealthLookup.GetRefRW(damage.Target).ValueRW;

                    // Already dead or despawning: no second death, no rewards
                    if (!health.IsAlive) continue;

                    health.Current -= damage.Value;
                    if (health.Current > 0f) continue;

                    health.IsAlive = false;
                    if (IsDeadLookup.HasComponent(damage.Target))
                    {
                        IsDeadLookup.SetComponentEnabled(damage.Target, true);
                    }
                }
            }
        }
    }
}
