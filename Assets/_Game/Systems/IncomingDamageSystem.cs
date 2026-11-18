using _Game.Components;
using _Game.Util;
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
        private ComponentLookup<Armor> _armorLookup;
        private ComponentLookup<AttackStats> _attackStatsLookup;
        private ComponentLookup<Classification> _classificationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _incomingDamageLookup = state.GetComponentLookup<EnemyIncomingDamage>(isReadOnly: false);
            _armorLookup = state.GetComponentLookup<Armor>(isReadOnly: true);
            _attackStatsLookup = state.GetComponentLookup<AttackStats>(isReadOnly: true);
            _classificationLookup = state.GetComponentLookup<Classification>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _incomingDamageLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _attackStatsLookup.Update(ref state);
            _classificationLookup.Update(ref state);

            state.Dependency = new ZeroIncomingDamageJob().ScheduleParallel(state.Dependency);

            state.Dependency = new AccumulateIncomingDamageJob
            {
                IncomingDamageLookup = _incomingDamageLookup,
                ArmorLookup = _armorLookup,
                AttackStatsLookup = _attackStatsLookup,
                ClassificationLookup = _classificationLookup,
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
        [ReadOnly] public ComponentLookup<Armor> ArmorLookup;
        [ReadOnly] public ComponentLookup<AttackStats> AttackStatsLookup;
        [ReadOnly] public ComponentLookup<Classification> ClassificationLookup;
        public double ElapsedTime;

        private Entity _lastWeapon;
        private AttackStats _attack;

        private void Execute(in ProjectileTarget target, in ProjectileDamage damage, in ProjectileSource source)
        {
            if (target.ReservationExpiry < ElapsedTime) return;
            if (!IncomingDamageLookup.HasComponent(target.Value)) return;

            if (source.Weapon != _lastWeapon)
            {
                _lastWeapon = source.Weapon;
                AttackStatsLookup.TryGetComponent(_lastWeapon, out _attack);
            }

            // Reserve what will actually land: tier bonus, then armor minus the firing weapon's penetration. Everything else is ignored.
            float value = damage.Value;
            if (ClassificationLookup.TryGetComponent(target.Value, out Classification targetClass))
            {
                value = DamageMath.ApplyTierBonus(value, targetClass.Tags.Tier(), _attack);
            }

            if (ArmorLookup.TryGetComponent(target.Value, out Armor armor))
            {
                value = DamageMath.Mitigate(value, armor.Value, _attack);
            }

            IncomingDamageLookup.GetRefRW(target.Value).ValueRW.Value += value;
        }
    }
}
