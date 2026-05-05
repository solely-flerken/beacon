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
            foreach ((RefRW<BeaconStats> stats, RefRW<Health> health, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<BeaconStats>, RefRW<Health>, DynamicBuffer<StatModifier>>().WithAll<BeaconStatUpdateRequired>().WithEntityAccess())
            {
                float oldMaxHp = health.ValueRO.Max;
                float newMaxHp = StatResolver.Resolve(StatType.MaxHp, health.ValueRO.BaseMax, localMods);
                health.ValueRW.Current += newMaxHp - oldMaxHp;
                health.ValueRW.Max = newMaxHp;
                stats.ValueRW.ArrivalRadius = StatResolver.Resolve(StatType.ArrivalRadius, stats.ValueRO.BaseArrivalRadius, localMods);

                SystemAPI.SetComponentEnabled<BeaconStatUpdateRequired>(entity, false);
            }
        }
    }
}
