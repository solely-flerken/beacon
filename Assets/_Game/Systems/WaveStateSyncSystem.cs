using _Game.Components;
using _Game.Gameplay.Waves;
using Unity.Entities;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WaveStateSyncSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create a dedicated standalone entity for Wave State
            Entity waveStateEntity = state.EntityManager.CreateEntity();

            state.EntityManager.AddComponentData(waveStateEntity, new WaveStateSingleton
            {
                IsActive = false,
                WaveElapsed = 0f
            });

            state.RequireForUpdate<WaveStateSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!WaveManager.Instance) return;

            SystemAPI.SetSingleton(new WaveStateSingleton
            {
                IsActive = WaveManager.Instance.IsWaveActive,
                WaveElapsed = WaveManager.Instance.WaveElapsed
            });
        }
    }
}