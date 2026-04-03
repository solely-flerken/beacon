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
            foreach ((RefRW<BeaconStats> stats, DynamicBuffer<StatModifier> localMods, DynamicBuffer<GlobalStatModifier> globalMods, Entity entity) in
                     SystemAPI.Query<RefRW<BeaconStats>, DynamicBuffer<StatModifier>, DynamicBuffer<GlobalStatModifier>>().WithAll<BeaconStatUpdateRequired>().WithEntityAccess())
            {
                float oldMaxHp = stats.ValueRO.MaxHp;
                float newMaxHp = StatResolver.Resolve(stats.ValueRO.BaseMaxHp, localMods, globalMods, StatType.MaxHp);
                stats.ValueRW.CurrentHp += newMaxHp - oldMaxHp;
                stats.ValueRW.MaxHp = newMaxHp;
                stats.ValueRW.ArrivalRadius = StatResolver.Resolve(stats.ValueRO.BaseArrivalRadius, localMods, globalMods, StatType.ArrivalRadius);

                SystemAPI.SetComponentEnabled<BeaconStatUpdateRequired>(entity, false);
            }
        }
    }
}
