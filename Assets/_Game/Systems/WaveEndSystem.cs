using _Game.Components;
using Unity.Entities;

namespace _Game.Systems
{
    /// <summary>
    /// Runs only when WaveEndedTag exists
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExpOrbSpawnSystem))]
    [UpdateBefore(typeof(BeaconCollectExpSystem))]
    public partial struct WaveEndCollectSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WaveEndTag>();
            state.RequireForUpdate<ExpOrbConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);

            // Mark all non-saturated orbs as saturated
            foreach ((ExpOrbTag _, Entity entity) in SystemAPI.Query<ExpOrbTag>().WithNone<SaturatedTag>().WithEntityAccess())
            {
                ecb.AddComponent<SaturatedTag>(entity);
            }

            // Consume the wave ended signal
            ecb.DestroyEntity(SystemAPI.GetSingletonEntity<WaveEndTag>());

            ecb.Playback(state.EntityManager);
        }
    }
}