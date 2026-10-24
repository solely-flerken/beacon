using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Components
{
    /// <summary>
    /// Per-frame movement intent for the player. Written by PlayerInputBridge,
    /// consumed by PlayerMovementSystem. Zero when idle; magnitude in [0,1].
    /// </summary>
    public struct PlayerMoveInput : IComponentData
    {
        public float2 Move;
    }

    /// <summary>
    /// Movement speed in world units per second.
    /// </summary>
    public struct Speed : IComponentData
    {
        public float BaseValue;
        public float Value;
    }

    /// <summary>
    /// Smoothed steering state for any moving unit (enemies, allies, ...).
    /// Velocity eases toward the desired steering each frame instead of snapping to it.
    /// Responsiveness is the per-second ease rate: higher = snappier, lower = smoother turns.
    /// </summary>
    public struct Steering : IComponentData
    {
        public float2 Velocity;
        public float Responsiveness;
        
        public void Ease(float2 desired, float deltaTime)
        {
            // 0 or unset responsiveness snaps instead of freezing the unit.
            if (Responsiveness <= 0f)
            {
                Velocity = desired;
                return;
            }

            float t = 1f - math.exp(-Responsiveness * deltaTime);
            Velocity = math.lerp(Velocity, desired, t);
        }
    }
}