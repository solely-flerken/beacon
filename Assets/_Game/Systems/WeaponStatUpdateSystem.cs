using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WeaponStatUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRW<WeaponStats> stats, DynamicBuffer<StatModifier> localMods, Entity entity) in
                     SystemAPI.Query<RefRW<WeaponStats>, DynamicBuffer<StatModifier>>().WithAll<WeaponStatUpdateRequired>().WithEntityAccess())
            {
                DynamicBuffer<StatModifier> ownerMods = default;
                bool hasOwner = SystemAPI.HasComponent<WeaponOwner>(entity);
                if (hasOwner)
                {
                    WeaponOwner owner = SystemAPI.GetComponent<WeaponOwner>(entity);
                    if (SystemAPI.HasBuffer<StatModifier>(owner.Value))
                    {
                        ownerMods = SystemAPI.GetBuffer<StatModifier>(owner.Value);
                    }
                }
                
                // Every weapon has those
                stats.ValueRW.Damage = StatResolver.Resolve(StatType.Damage, stats.ValueRO.BaseDamage, localMods, ownerMods);
                stats.ValueRW.FireRate = StatResolver.Resolve(StatType.FireRate, stats.ValueRO.BaseFireRate, localMods, ownerMods);
                stats.ValueRW.TargetRange = StatResolver.Resolve(StatType.TargetRange, stats.ValueRO.BaseTargetRange, localMods, ownerMods);

                if (SystemAPI.HasComponent<ProjectileStats>(entity))
                {
                    RefRW<ProjectileStats> baseProj = SystemAPI.GetComponentRW<ProjectileStats>(entity);

                    baseProj.ValueRW.Speed = StatResolver.Resolve(StatType.ProjectileSpeed, baseProj.ValueRO.BaseSpeed, localMods, ownerMods);
                    baseProj.ValueRW.Pierce = (int)StatResolver.Resolve(StatType.Pierce, baseProj.ValueRO.BasePierce, localMods, ownerMods);
                }

                // Disable dirty flag
                SystemAPI.SetComponentEnabled<WeaponStatUpdateRequired>(entity, false);
            }
        }
    }
}