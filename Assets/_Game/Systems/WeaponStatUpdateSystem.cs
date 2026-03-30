using _Game.Components;
using _Game.Util;
using Unity.Burst;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WeaponStatUpdateSystem : ISystem
    {
        private EntityQuery _beaconQuery;
        private BufferLookup<GlobalStatModifier> _globalModsLookup;
        
        public void OnCreate(ref SystemState state)
        {
            _beaconQuery = state.EntityManager.CreateEntityQuery(typeof(BeaconStats), typeof(GlobalStatModifier));
            _globalModsLookup = state.GetBufferLookup<GlobalStatModifier>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _globalModsLookup.Update(ref state);

            foreach ((RefRW<WeaponStats> stats, DynamicBuffer<StatModifier> localMods, Entity entity) in 
                     SystemAPI.Query<RefRW<WeaponStats>, DynamicBuffer<StatModifier>>().WithAll<StatUpdateRequired>().WithEntityAccess())
            {
                if (_beaconQuery.IsEmpty) return;
           
                Entity beacon = _beaconQuery.GetSingletonEntity();
                DynamicBuffer<GlobalStatModifier> globalMods = state.EntityManager.GetBuffer<GlobalStatModifier>(beacon);

                // Every weapon has those
                stats.ValueRW.Damage = StatResolver.Resolve(stats.ValueRO.BaseDamage, localMods, globalMods, StatType.Damage);
                stats.ValueRW.FireRate = StatResolver.Resolve(stats.ValueRO.BaseFireRate, localMods, globalMods, StatType.FireRate);
                stats.ValueRW.TargetRange = StatResolver.Resolve(stats.ValueRO.BaseTargetRange, localMods, globalMods, StatType.TargetRange);
                
                if (SystemAPI.HasComponent<ProjectileStats>(entity))
                {
                    RefRW<ProjectileStats> baseProj = SystemAPI.GetComponentRW<ProjectileStats>(entity);

                    baseProj.ValueRW.Speed = StatResolver.Resolve(baseProj.ValueRO.BaseSpeed, localMods, globalMods, StatType.ProjectileSpeed);
                    baseProj.ValueRW.Pierce = (int)StatResolver.Resolve(baseProj.ValueRO.Pierce, localMods, globalMods, StatType.Pierce);
                }

                // Disable dirty flag
                SystemAPI.SetComponentEnabled<StatUpdateRequired>(entity, false);
            }
        }
    }
}