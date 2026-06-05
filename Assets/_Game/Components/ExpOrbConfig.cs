using Unity.Entities;

namespace _Game.Components
{
    public struct ExpOrbConfig : IComponentData
    {
        // Orb Values
        public float MaxOrbValue;
        
        // Visual
        public float BaseSize;
        public float MaxSize;

        // Accumulation — radius within which a death event merges into an existing orb
        public float MergeRadius;

        // Maximum simultaneous exp orbs in the world
        public int MaxBuckets;

        // Movement
        public float CollectBaseSpeed;
        public float CollectMaxSpeed;
        
        public float BeaconArrivalRadius;
    }
}