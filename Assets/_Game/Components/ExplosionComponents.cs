using Unity.Entities;

namespace _Game.Components
{
    public struct ExplosionTag : IComponentData { }

    public struct ExplosionState : IComponentData
    {
        public float Elapsed;
        public float Duration;
        public float StartScale;
    }
}
