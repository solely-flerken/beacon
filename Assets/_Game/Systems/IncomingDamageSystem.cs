using _Game.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WeaponTargetingSystem))]
    public partial struct IncomingDamageSystem : ISystem
    {
        private ComponentLookup<EnemyIncomingDamage> _incomingDamageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _incomingDamageLookup = state.GetComponentLookup<EnemyIncomingDamage>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _incomingDamageLookup.Update(ref state);

            state.Dependency = new ZeroIncomingDamageJob().ScheduleParallel(state.Dependency);

            state.Dependency = new AccumulateIncomingDamageJob
            {
                IncomingDamageLookup = _incomingDamageLookup,
                ElapsedTime = SystemAPI.Time.ElapsedTime,
            }.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    internal partial struct ZeroIncomingDamageJob : IJobEntity
    {
        private void Execute(ref EnemyIncomingDamage incoming)
        {
            incoming.Value = 0f;
        }
    }

    [BurstCompile]
    internal partial struct AccumulateIncomingDamageJob : IJobEntity
    {
        [NativeDisableParallelForRestriction] public ComponentLookup<EnemyIncomingDamage> IncomingDamageLookup;
        public double ElapsedTime;

        private void Execute(in ProjectileTarget target, in ProjectileDamage damage)
        {
            if (target.ReservationExpiry < ElapsedTime) return;
            if (!IncomingDamageLookup.HasComponent(target.Value)) return;
            
            IncomingDamageLookup.GetRefRW(target.Value).ValueRW.Value += damage.Value;
        }
    }
}
