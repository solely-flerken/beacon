using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Components
{
    public enum TargetStrategy : byte
    {
        Nearest,
        Random,
        Furthest
    }

    public struct WeaponTargeting : IComponentData
    {
        public TargetStrategy Strategy;
        public Entity CurrentTarget;
        public float2 TargetPosition;
        public bool HasTarget;
    }
}