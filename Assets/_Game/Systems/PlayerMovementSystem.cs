using _Game.Components;
using _Game.Config;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game.Systems
{
    /// <summary>
    /// Integrates the player's position from PlayerMoveInput each frame.
    /// Runs before FlowFieldSystem so enemy pathing rebuilds against the new position in the same frame the beacon moves.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FlowFieldSystem))]
    public partial struct PlayerMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = math.min(SystemAPI.Time.DeltaTime, SimulationConfig.MaxDeltaTime);
            float2 halfExtents = new(ArenaConfig.Width * 0.5f, ArenaConfig.Height * 0.5f);

            foreach ((RefRW<LocalTransform> transform, RefRO<PlayerMoveInput> input, RefRO<Speed> speed) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerMoveInput>, RefRO<Speed>>().WithAll<PlayerTag>())
            {
                float2 move = input.ValueRO.Move;

                // Keyboard diagonal = (1,1), magnitude 1.41 — cap at 1
                if (math.lengthsq(move) > 1f)
                {
                    move = math.normalize(move);
                }

                float3 currentPos = transform.ValueRO.Position;
                float2 nextPos = currentPos.xy + move * (speed.ValueRO.Value * deltaTime);
                
                // Clamp to arena bounds.
                nextPos = math.clamp(nextPos, -halfExtents, halfExtents);

                transform.ValueRW.Position = new float3(nextPos, currentPos.z);
            }
        }
    }
}