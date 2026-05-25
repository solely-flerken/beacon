using _Game.Components;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WaveStateSyncSystem))]
    [UpdateBefore(typeof(WaveModChangeDetectionSystem))]
    [BurstCompile]
    public partial struct TimedModifierCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WaveStatModifier>();
            state.RequireForUpdate<WaveStateSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get the current wave time
            WaveStateSingleton waveState = SystemAPI.GetSingleton<WaveStateSingleton>();
            if (!waveState.IsActive) return;

            float waveElapsed = waveState.WaveElapsed;

            // Check if any modifier has expired
            DynamicBuffer<WaveStatModifier> readMods = SystemAPI.GetSingletonBuffer<WaveStatModifier>(isReadOnly: true);
            bool anyExpired = false;
            
            for (int i = 0; i < readMods.Length; i++)
            {
                if (Mathf.Approximately(readMods[i].ExpiryTime, float.MaxValue)) continue; // Is infinite
                if (!(readMods[i].ExpiryTime <= waveElapsed)) continue; // Is not expired
                
                anyExpired = true;
                break;
            }

            if (!anyExpired) return;

            // Remove the expired modifiers
            DynamicBuffer<WaveStatModifier> waveMods = SystemAPI.GetSingletonBuffer<WaveStatModifier>(isReadOnly: false);

            for (int i = waveMods.Length - 1; i >= 0; i--)
            {
                if (Mathf.Approximately(waveMods[i].ExpiryTime, float.MaxValue)) continue;

                if (waveMods[i].ExpiryTime <= waveElapsed)
                {
                    waveMods.RemoveAt(i);
                }
            }
        }
    }
}