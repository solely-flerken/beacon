using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct EnemyStatUpdateSystem : ISystem
    {
        private BufferLookup<StatModifier> _modLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _modLookup = state.GetBufferLookup<StatModifier>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _modLookup.Update(ref state);

            new UpdateStatsJob { ModLookup = _modLookup }.ScheduleParallel();
        }
        
        [BurstCompile]
        public partial struct UpdateStatsJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<StatModifier> ModLookup;

            private void Execute(
                ref EnemySpeed speed, 
                ref EnemyHealth health, 
                ref ContactDamage contactDamage,
                EnabledRefRW<EnemyStatUpdateRequired> updateRequired,
                in EnemyTypeRef typeRef)
            {
                // Get the buffer for this specific enemy type
                DynamicBuffer<StatModifier> mods = ModLookup[typeRef.Value];

                // Resolve stats
                speed.Value = StatResolver.Resolve(speed.BaseValue, mods, StatType.EnemySpeed);
                health.Max = StatResolver.Resolve(health.BaseMax, mods, StatType.EnemyMaxHealth);
                contactDamage.Value = StatResolver.Resolve(contactDamage.BaseValue, mods, StatType.EnemyContactDamage);

                updateRequired.ValueRW = false;
            }
        }
    }
}
