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
            foreach ((RefRW<BeaconStats> stats, RefRW<Health> health, DynamicBuffer<StatModifier> localMods, DynamicBuffer<GlobalStatModifier> globalMods, Entity entity) in
                     SystemAPI.Query<RefRW<BeaconStats>, RefRW<Health>, DynamicBuffer<StatModifier>, DynamicBuffer<GlobalStatModifier>>().WithAll<BeaconStatUpdateRequired>().WithEntityAccess())
            {
                float oldMaxHp = health.ValueRO.Max;
                float newMaxHp = StatResolver.Resolve(health.ValueRO.BaseMax, localMods, globalMods, StatType.MaxHp);
                health.ValueRW.Current += newMaxHp - oldMaxHp;
                health.ValueRW.Max = newMaxHp;
                stats.ValueRW.ArrivalRadius = StatResolver.Resolve(stats.ValueRO.BaseArrivalRadius, localMods, globalMods, StatType.ArrivalRadius);

                SystemAPI.SetComponentEnabled<BeaconStatUpdateRequired>(entity, false);
            }
        }
    }
}
