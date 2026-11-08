using _Game.Components;
using _Game.Core;
using Unity.Entities;
using UnityEngine;

namespace _Game.Systems
{
    /// <summary>
    /// Publishes the beacon's health on any change.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct BeaconHealthEventSystem : ISystem
    {
        private float _previousHealth;
        private float _previousMax;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeaconStats>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (RefRO<Health> health in SystemAPI.Query<RefRO<Health>>().WithAll<BeaconStats>().WithChangeFilter<Health>())
            {
                float current = health.ValueRO.Current;
                float max = health.ValueRO.Max;

                if (Mathf.Approximately(current, _previousHealth) && Mathf.Approximately(max, _previousMax)) continue;

                if (current < _previousHealth)
                {
                    EventBus.FireBeaconHit(_previousHealth - current, current, max);
                }

                EventBus.FireBeaconHealthChanged(current, max);

                _previousHealth = current;
                _previousMax = max;
            }
        }
    }
}
