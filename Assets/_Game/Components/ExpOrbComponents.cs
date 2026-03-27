using Unity.Entities;

namespace _Game.Components
{
    public struct ExpOrbTag : IComponentData { }

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