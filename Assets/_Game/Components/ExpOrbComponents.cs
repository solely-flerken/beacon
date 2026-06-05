using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Components
{
    public struct ExpOrbTag : IComponentData { }

    /// <summary>
    /// Added when an orb's Value >= MaxValue. Never removed since a saturated orb flies to the beacon and is destroyed.
    /// </summary>
    public struct SaturatedTag : IComponentData { }
    
    public struct ExpOrbData : IComponentData
    {
        public float Value;
        public float MaxValue;
        public float2 Position; // spawn position; never updated after spawn (saturated orbs move via LocalTransform only)
    }
}