using Unity.Entities;

namespace _Game.Components
{
    public struct Health : IComponentData
    {
        public float BaseMax;
        public float Max;
        public float Current;
        public bool IsAlive;
    }
    
    public struct IsDead : IComponentData, IEnableableComponent { }
}