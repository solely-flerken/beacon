using _Game.Components;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TimedModifierCleanupSystem))]
    [UpdateBefore(typeof(BeaconStatUpdateSystem))]
    [UpdateBefore(typeof(EnemyStatUpdateSystem))]
    [UpdateBefore(typeof(WeaponStatUpdateSystem))]
    public partial struct WaveModChangeDetectionSystem : ISystem
    {
        private EntityQuery _waveModQuery;
        private EntityQuery _globalModQuery;

        public void OnCreate(ref SystemState state)
        {
            // Wave modifiers
            _waveModQuery = SystemAPI.QueryBuilder().WithAll<WaveStatModifier>().Build();
            _waveModQuery.SetChangedVersionFilter(ComponentType.ReadOnly<WaveStatModifier>());

            // Global modifiers
            _globalModQuery = SystemAPI.QueryBuilder().WithAll<GlobalStatModifier>().Build();
            _globalModQuery.SetChangedVersionFilter(ComponentType.ReadOnly<GlobalStatModifier>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_waveModQuery.IsEmpty && _globalModQuery.IsEmpty)
                return;

            state.Dependency = new MarkBeaconDirtyJob().ScheduleParallel(state.Dependency);
            state.Dependency = new MarkEnemiesDirtyJob().ScheduleParallel(state.Dependency);
            state.Dependency = new MarkWeaponsDirtyJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)] 
        public partial struct MarkBeaconDirtyJob : IJobEntity
        {
            private void Execute(EnabledRefRW<BeaconStatUpdateRequired> updateRequired)
            {
                updateRequired.ValueRW = true;
            }
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)] 
        public partial struct MarkEnemiesDirtyJob : IJobEntity
        {
            private void Execute(EnabledRefRW<EnemyStatUpdateRequired> updateRequired)
            {
                updateRequired.ValueRW = true;
            }
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)] 
        public partial struct MarkWeaponsDirtyJob : IJobEntity
        {
            private void Execute(EnabledRefRW<WeaponStatUpdateRequired> updateRequired)
            {
                updateRequired.ValueRW = true;
            }
        }
    }
}