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
        public float2 TargetVelocity;
        public bool HasTarget;
        public bool StickyLock;
        public float SwapCooldown;
        public float SwapCooldownRemaining;
        public uint RandomSeed;
    }
}