using _Game.Components;
using _Game.Core;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageAndDeathSystem))]
    public partial struct BeaconHealthEventSystem : ISystem
    {
        private float _previousHealth;

        public void OnUpdate(ref SystemState state)
        {
            foreach ((RefRO<Health> health, BeaconStats _) in SystemAPI.Query<RefRO<Health>, BeaconStats>().WithChangeFilter<Health>())
            {
                float current = health.ValueRO.Current;

                if (current < _previousHealth)
                {
                    EventBus.FireBeaconHit(current, health.ValueRO.Max);
                }
                
                _previousHealth = current;
            }
        }
    }
}