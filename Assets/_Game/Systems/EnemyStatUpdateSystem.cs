using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyStatUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRW<EnemySpeed> speed, RefRW<EnemyHealth> health, RefRW<ContactDamage> contactDamage, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<EnemySpeed>, RefRW<EnemyHealth>, RefRW<ContactDamage>, DynamicBuffer<StatModifier>>().WithAll<EnemyStatUpdateRequired>().WithEntityAccess())
            {
                speed.ValueRW.Value = StatResolver.Resolve(speed.ValueRO.BaseValue, localMods, StatType.EnemySpeed);
                health.ValueRW.Max = StatResolver.Resolve(health.ValueRO.BaseMax, localMods, StatType.EnemyMaxHealth);
                contactDamage.ValueRW.Value = StatResolver.Resolve(contactDamage.ValueRO.BaseValue, localMods, StatType.EnemyContactDamage);

                SystemAPI.SetComponentEnabled<EnemyStatUpdateRequired>(entity, false);
            }
        }
    }
}
