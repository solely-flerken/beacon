using _Game.Components;
using _Game.Core;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct BeaconHealthEventSystem : ISystem
    {
        private float _previousHealth;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeaconStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (RefRO<Health> health in SystemAPI.Query<RefRO<Health>>().WithAll<BeaconStats>().WithChangeFilter<Health>())
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