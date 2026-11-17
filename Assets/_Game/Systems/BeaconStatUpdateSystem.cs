using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BeaconStatUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<GlobalStatModifier> globalBuffer, isReadOnly: true))
                return;

            if (!SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<WaveStatModifier> waveBuffer, isReadOnly: true))
                return;

            foreach ((RefRW<BeaconStats> stats, RefRW<Health> health, RefRW<Armor> armor, Classification classification, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<BeaconStats>, RefRW<Health>, RefRW<Armor>, Classification, DynamicBuffer<StatModifier>>().WithAll<BeaconStatUpdateRequired, UnitFaction>().WithEntityAccess())
            {
                Tags tags = classification.ForUnit(state.EntityManager.GetSharedComponent<UnitFaction>(entity).Value);

                // HP
                float oldMaxHp = health.ValueRO.Max;
                float newMaxHp = StatResolver.Resolve(StatType.MaxHp, health.ValueRO.BaseMax, tags, globalBuffer, waveBuffer, localMods);
                health.ValueRW.Current += newMaxHp - oldMaxHp;
                health.ValueRW.Max = newMaxHp;

                // Armor
                armor.ValueRW.Value = StatResolver.Resolve(StatType.Armor, armor.ValueRO.BaseValue, tags, globalBuffer, waveBuffer, localMods);

                // Arrival radius
                stats.ValueRW.ArrivalRadius = StatResolver.Resolve(StatType.ArrivalRadius, stats.ValueRO.BaseArrivalRadius, tags, globalBuffer, waveBuffer, localMods);

                // Exp gain
                stats.ValueRW.ExpGain = StatResolver.Resolve(StatType.ExpGain, stats.ValueRO.BaseExpGain, tags, globalBuffer, waveBuffer, localMods);

                SystemAPI.SetComponentEnabled<BeaconStatUpdateRequired>(entity, false);
            }
        }
    }
}
