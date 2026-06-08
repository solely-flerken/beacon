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
}