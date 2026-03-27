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
        public float ScaleLerpSpeed;
        
        // Merging
        public float MergeRadius;
        public float MergeSpeed;
        
        // Movement
        public float CollectBaseSpeed;
        public float CollectMaxSpeed;
        
        public float BeaconArrivalRadius;
    }
}