using Unity.Entities;

namespace _Game.Components
{
    public struct ExpOrbTag : IComponentData { }

    /// <summary>
    /// Added when an orb's Value >= MaxValue. Never removed since a saturated orb flies to the beacon and is destroyed.
    /// </summary>
    public struct SaturatedTag : IComponentData { }
    
    public enum ExpOrbState : byte
    {
        Idle,
        Merging,
        Saturated
    }

    public struct ExpOrbData : IComponentData
    {
        public float Value;
        public float MaxValue;
        public float VisualScale;
        public ExpOrbState State;
        public Entity MergeTarget;
    }
}